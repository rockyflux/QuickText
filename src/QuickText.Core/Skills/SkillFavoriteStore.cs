using System.Text.Json;
using QuickText.Core.Persistence;

namespace QuickText.Core.Skills;

/// <summary>Load/save favorited Agent Skills next to machine settings.</summary>
public sealed class SkillFavoriteStore
{
    private readonly string _path;
    private SkillFavoritesFile _file;

    public SkillFavoriteStore(string path)
    {
        _path = path;
        _file = Load();
    }

    public static string DefaultPath =>
        Path.Combine(AppPaths.MachineStateDir, AppPaths.SkillsFavoritesFileName);

    public IReadOnlyList<SkillFavoriteItem> Items => _file.Items;

    public string ActiveRoot
    {
        get => SkillRoots.NormalizeKey(_file.ActiveRoot);
        set
        {
            var key = SkillRoots.NormalizeKey(value);
            if (string.Equals(_file.ActiveRoot, key, StringComparison.OrdinalIgnoreCase)) return;
            _file.ActiveRoot = key;
            Save();
        }
    }

    public SkillFavoriteItem? FindByBody(string body) =>
        _file.Items.FirstOrDefault(i =>
            string.Equals(i.Body, body, StringComparison.OrdinalIgnoreCase));

    public bool IsFavorite(string body) => FindByBody(body) is not null;

    /// <summary>Add or refresh a favorite keyed by absolute skill path. Returns the stored item.</summary>
    public SkillFavoriteItem Favorite(string name, string body, string? description = null, string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("body required", nameof(body));
        Upsert(name, body, description, alias, touchAlias: alias is not null);
        Save();
        return FindByBody(body)!;
    }

    public int FavoriteMany(IEnumerable<(string Name, string Body, string? Description)> items)
    {
        int added = 0;
        bool any = false;
        foreach (var (name, body, description) in items)
        {
            if (string.IsNullOrWhiteSpace(body)) continue;
            any = true;
            if (!IsFavorite(body)) added++;
            Upsert(name, body, description, alias: null, touchAlias: false);
        }
        if (any) Save();
        return added;
    }

    public int UnfavoriteMany(IEnumerable<string> bodies)
    {
        int n = 0;
        foreach (var body in bodies.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            n += _file.Items.RemoveAll(i =>
                string.Equals(i.Body, body, StringComparison.OrdinalIgnoreCase));
        }
        if (n > 0) Save();
        return n;
    }

    public bool Unfavorite(string body)
    {
        var n = _file.Items.RemoveAll(i =>
            string.Equals(i.Body, body, StringComparison.OrdinalIgnoreCase));
        if (n == 0) return false;
        Save();
        return true;
    }

    private void Upsert(string name, string body, string? description, string? alias, bool touchAlias)
    {
        var existing = FindByBody(body);
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(name)) existing.Name = name.Trim();
            if (description is not null)
                existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            if (touchAlias) existing.Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
            return;
        }
        _file.Items.Add(new SkillFavoriteItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(Path.GetDirectoryName(body)!) ?? body : name.Trim(),
            Body = body,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(),
        });
    }

    public void SetAlias(string body, string? alias)
    {
        var item = FindByBody(body);
        if (item is null) return;
        item.Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        Save();
    }

    /// <summary>Rename the stored import name and clear any display alias so the list shows it.</summary>
    public bool SetName(string body, string name)
    {
        var item = FindByBody(body);
        if (item is null) return false;
        var trimmed = (name ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        item.Name = trimmed;
        item.Alias = null;
        Save();
        return true;
    }

    public void Reload() => _file = Load();

    private SkillFavoritesFile Load()
    {
        try
        {
            if (!File.Exists(_path)) return new SkillFavoritesFile();
            return JsonSerializer.Deserialize<SkillFavoritesFile>(File.ReadAllText(_path), JsonConfig.Read)
                   ?? new SkillFavoritesFile();
        }
        catch { return new SkillFavoritesFile(); }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_file, JsonConfig.Write);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);
    }
}
