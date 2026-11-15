using System.IO;
using System.Net;
using System.Net.Http;
using QuickText.Core;
using QuickText.Core.Updates;

namespace QuickText.App;

/// <summary>What an update check came back with.</summary>
public enum UpdateStatus
{
    /// <summary>No newer release — there is nothing to do.</summary>
    UpToDate,
    /// <summary>A newer release was downloaded, verified and unpacked. <see cref="UpdateOutcome.ExePath"/>
    /// says where the new program is.</summary>
    Ready,
    /// <summary>The download did not match the checksum the release published, so it was thrown
    /// away. Nothing was unpacked.</summary>
    VerifyFailed,
    /// <summary>The check itself failed: no network, or a release that does not carry the files we
    /// need.</summary>
    Failed,
}

/// <summary>The result of one update check.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Version">The version that was found, without its leading 'v'.</param>
/// <param name="ExePath">The unpacked program, when <paramref name="Status"/> is
/// <see cref="UpdateStatus.Ready"/>.</param>
/// <param name="ReleaseUrl">The release page, so a balloon can link to it.</param>
/// <param name="Error">Why it failed, for the log and the message.</param>
public sealed record UpdateOutcome(UpdateStatus Status, string? Version, string? ExePath,
                                   string? ReleaseUrl, string? Error);

/// <summary>
/// The opt-in update check: resolve the latest GitHub release, and if it is newer than the
/// running build, download the self-contained zip, check it against the checksum the same release
/// published, and unpack it where the user can find it.
/// <para>Resolves the tag via the public <c>/releases/latest</c> redirect and downloads assets from
/// the releases CDN — not the REST API. Unauthenticated API calls are capped at ~60/hour per IP
/// and return 403 once exhausted; the HTML/CDN path is what users hit when browsing releases and
/// does not share that quota.</para>
/// <para><b>It never replaces the running program.</b> These releases are single-file
/// self-contained executables, and Windows locks a running executable: overwriting it in place
/// fails with a sharing violation and leaves a half-written install behind. So the new build is
/// unpacked into a folder of its own and the user is told where it is — replacing the old one is
/// their call, made with the old one closed.</para>
/// </summary>
public static class UpdateService
{
    public const string ProjectUrl = "https://github.com/rockyflux/QuickText";
    /// <summary>Public latest-release redirect (302 → <c>/releases/tag/v…</c>). Not the REST API.</summary>
    public const string LatestUrl = ProjectUrl + "/releases/latest";
    public const string ReleasePage = ProjectUrl + "/releases";
    private const string ChecksumsAsset = "SHA256SUMS.txt";

    /// <summary>Where an unpacked update is put: LocalAppData in installed mode, Data\Updates in
    /// portable mode (so the portable "nothing left on the host" promise holds). The folder is
    /// outside the program's own folder, so it survives the program being moved or replaced.</summary>
    public static string UpdatesRoot => AppPaths.UpdatesDir;

    // Redirect probe must NOT follow: we only need the Location header that names the tag.
    // Downloads follow redirects (GitHub → release-assets CDN).
    private static readonly HttpClient Latest = CreateClient(allowAutoRedirect: false, TimeSpan.FromSeconds(15));
    private static readonly HttpClient Downloads = CreateClient(allowAutoRedirect: true, TimeSpan.FromMinutes(10));

    // Two checks (startup + "Check now", or a double click) must not extract into the same folder
    // at the same time. The second waits, then finds the completed first result.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static HttpClient CreateClient(bool allowAutoRedirect, TimeSpan timeout)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = allowAutoRedirect };
        var client = new HttpClient(handler) { Timeout = timeout };
        // GitHub rejects requests without a User-Agent (API and site alike).
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuickText-update-check");
        return client;
    }

    /// <summary>
    /// Check for a newer release and, if there is one, fetch it. Never throws: every failure comes
    /// back as <see cref="UpdateStatus.Failed"/>, because a check that cannot reach the network is
    /// an ordinary outcome, not a reason to take the app down.
    /// </summary>
    public static async Task<UpdateOutcome> RunAsync(string currentVersion, CancellationToken ct = default)
    {
        try
        {
            await Gate.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            return new(UpdateStatus.Failed, null, null, null, ex.Message);
        }
        try
        {
            return await RunCoreAsync(currentVersion, ct).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }

    private static async Task<UpdateOutcome> RunCoreAsync(string currentVersion, CancellationToken ct)
    {
        string? releaseUrl = null;
        string tempDir = "";
        try
        {
            string? tag = await ResolveLatestTagAsync(ct).ConfigureAwait(false);
            // No published release yet (or an unexpected landing page) is "up to date", not a failure.
            if (tag is null)
                return new(UpdateStatus.UpToDate, null, null, null, null);

            releaseUrl = ReleasePage + "/tag/" + Uri.EscapeDataString(tag);
            if (!UpdateCheck.IsNewer(tag, currentVersion))
                return new(UpdateStatus.UpToDate, UpdateCheck.NormalizeVersion(tag), null, releaseUrl, null);

            string zipName = UpdateCheck.SelfContainedZipName(tag);
            if (zipName.Length == 0)
                return new(UpdateStatus.Failed, null, null, releaseUrl, "empty release tag");

            string zipUrl = UpdateCheck.ReleaseAssetUrl(ProjectUrl, tag, zipName);
            string sumsUrl = UpdateCheck.ReleaseAssetUrl(ProjectUrl, tag, ChecksumsAsset);

            string version = UpdateCheck.NormalizeVersion(tag);
            string destDir = Path.Combine(UpdatesRoot, version);

            // Already fully installed: the completion marker is only written after extraction
            // succeeded, so a partial folder from an interrupted run is not treated as ready.
            // Fetching it again would re-download tens of megabytes on every single start.
            string? already = UpdateInstaller.FindInstalledExecutable(destDir);
            if (already is not null)
                return new(UpdateStatus.Ready, version, already, releaseUrl, null);

            tempDir = Path.Combine(Path.GetTempPath(), "QuickText-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string zipPath = Path.Combine(tempDir, zipName);
            await DownloadFileAsync(zipUrl, zipPath, ct).ConfigureAwait(false);

            string sums = await ReadTextAsync(sumsUrl, ct).ConfigureAwait(false);
            var checksums = UpdateCheck.ParseChecksums(sums);
            if (!checksums.TryGetValue(zipName, out var expected))
                return new(UpdateStatus.Failed, version, null, releaseUrl, ChecksumsAsset + " does not list " + zipName);
            if (!UpdateCheck.MatchesChecksum(expected, UpdateCheck.ComputeSha256Hex(zipPath)))
                return new(UpdateStatus.VerifyFailed, version, null, releaseUrl, "the download did not match " + expected);

            string exe = UpdateInstaller.InstallFromZip(zipPath, destDir);
            return new(UpdateStatus.Ready, version, exe, releaseUrl, null);
        }
        catch (Exception ex)
        {
            return new(UpdateStatus.Failed, null, null, releaseUrl, ex.Message);
        }
        finally
        {
            // The download is a scratch copy: it is unpacked by now, and leaving tens of megabytes
            // in %TEMP% for every version ever fetched is not worth it.
            if (tempDir.Length > 0)
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    /// <summary>
    /// Read the tag from the <c>Location</c> of a 302 to <c>/releases/tag/…</c>. A 404 (no release
    /// yet) returns null. Any other status is an error worth surfacing.
    /// </summary>
    private static async Task<string?> ResolveLatestTagAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
        using var resp = await Latest.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        // 301/302/303/307/308 all carry Location for "latest → tag".
        if ((int)resp.StatusCode is >= 300 and < 400)
        {
            var loc = resp.Headers.Location;
            if (loc is null) return null;
            var absolute = loc.IsAbsoluteUri ? loc.AbsoluteUri : new Uri(new Uri(LatestUrl), loc).AbsoluteUri;
            return UpdateCheck.TagFromLatestUrl(absolute);
        }

        resp.EnsureSuccessStatusCode();
        // Rare: some proxies collapse the redirect and return 200 on the tag page. Prefer the
        // final request URI when present.
        var final = resp.RequestMessage?.RequestUri?.AbsoluteUri;
        return UpdateCheck.TagFromLatestUrl(final);
    }

    private static async Task DownloadFileAsync(string url, string path, CancellationToken ct)
    {
        using var resp = await Downloads.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dst, ct).ConfigureAwait(false);
    }

    private static async Task<string> ReadTextAsync(string url, CancellationToken ct)
    {
        using var resp = await Downloads.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }
}
