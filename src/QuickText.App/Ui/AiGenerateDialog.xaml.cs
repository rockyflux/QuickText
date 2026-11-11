using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickText.Core.Localization;
using QuickText.Core.Settings;

namespace QuickText.App.Ui;

/// <summary>Modal: pick a QuickText scenario, describe the snippet, generate a draft, confirm to fill the Manager.</summary>
public partial class AiGenerateDialog : Window
{
    private sealed record TemplateItem(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly AiProviderConfig _cfg;
    private string _templateId = AiSnippetTemplates.General;
    private string _lastStarter = "";
    private CancellationTokenSource? _cts;
    private AiSnippetDraft? _result;
    private bool _busy;
    private bool _loadingTemplates;

    public AiGenerateDialog() : this(new AiProviderConfig
    {
        Name = "smoke",
        Provider = AiProviders.OpenAi,
        Model = "gpt-4o-mini",
        BaseUrl = AiProviders.OpenAiBaseUrl,
    })
    {
    }

    public AiGenerateDialog(AiProviderConfig cfg)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.CapHeightToMonitor(this);
        WindowTheming.PlaceNearTop(this, topOffset: 50);   // ~50 DIP under the work-area top, not CenterOwner
        WindowTheming.ApplyFlowDirection(this);
        BuildTemplateCombo();
        _lastStarter = TemplateStarter(_templateId);
        PromptBox.Text = _lastStarter;
        PromptBox.TextChanged += (_, _) => UpdateGenerateEnabled();
        ResultName.TextChanged += (_, _) => UpdateConfirmEnabled();
        ResultAbbr.TextChanged += (_, _) => UpdateConfirmEnabled();
        ResultBody.TextChanged += (_, _) => UpdateConfirmEnabled();
        UpdateGenerateEnabled();
        Closed += (_, _) =>
        {
            _cts?.Cancel();
            _cts?.Dispose();
        };
        Loaded += (_, _) => { PromptBox.Focus(); PromptBox.SelectAll(); };
    }

    public AiSnippetDraft? Result => _result;

    /// <summary>Show the dialog. Returns the confirmed draft, or null if cancelled.</summary>
    public static AiSnippetDraft? Show(Window owner, AiProviderConfig cfg)
    {
        var d = new AiGenerateDialog(cfg) { Owner = owner };
        return d.ShowDialog() == true ? d.Result : null;
    }

    /// <summary>--shots: seed prompt + fake result so the preview panel is on film.</summary>
    internal void ShotsFill()
    {
        PromptBox.Text = "代码重构提示词";
        ApplyDraft(new AiSnippetDraft(
            "代码重构优化",
            "dmcg",
            "帮我重构下面这段代码，语言：{语言}\n目标：{优化目标|提升性能|可读性}\n代码：\n{剪贴板}\n输出：优化后代码 + 修改点简要说明",
            UseVariables: true));
        StatusText.Text = "";
    }

    private static string L(string key) => LocalizationService.Instance[key];

    private void BuildTemplateCombo()
    {
        _loadingTemplates = true;
        TemplateCombo.Items.Clear();
        TemplateItem? select = null;
        foreach (var id in AiSnippetTemplates.All)
        {
            var item = new TemplateItem(id, TemplateLabel(id));
            TemplateCombo.Items.Add(item);
            if (id == _templateId) select = item;
        }
        TemplateCombo.SelectedItem = select ?? (TemplateCombo.Items.Count > 0 ? TemplateCombo.Items[0] : null);
        _loadingTemplates = false;
    }

    private static string TemplateLabel(string id) => id switch
    {
        AiSnippetTemplates.Profile => L("Manager.AiTpl.Profile"),
        AiSnippetTemplates.Dev => L("Manager.AiTpl.Dev"),
        AiSnippetTemplates.Office => L("Manager.AiTpl.Office"),
        AiSnippetTemplates.Agent => L("Manager.AiTpl.Agent"),
        _ => L("Manager.AiTpl.General"),
    };

    private static string TemplateStarter(string id) => id switch
    {
        AiSnippetTemplates.Profile => L("Manager.AiTpl.Profile.Prompt"),
        AiSnippetTemplates.Dev => L("Manager.AiTpl.Dev.Prompt"),
        AiSnippetTemplates.Office => L("Manager.AiTpl.Office.Prompt"),
        AiSnippetTemplates.Agent => L("Manager.AiTpl.Agent.Prompt"),
        _ => L("Manager.AiTpl.General.Prompt"),
    };

    private void OnTemplateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingTemplates) return;
        if (TemplateCombo.SelectedItem is not TemplateItem item) return;
        _templateId = item.Id;
        var starter = TemplateStarter(item.Id);
        var cur = PromptBox.Text;
        if (cur.Trim().Length == 0 || cur == _lastStarter)
        {
            PromptBox.Text = starter;
            PromptBox.CaretIndex = PromptBox.Text.Length;
        }
        _lastStarter = starter;
    }

    private void UpdateGenerateEnabled() =>
        GenerateButton.IsEnabled = !_busy && PromptBox.Text.Trim().Length > 0;

    private void UpdateConfirmEnabled()
    {
        if (_busy || ResultPanel.Visibility != Visibility.Visible)
        {
            ConfirmButton.IsEnabled = false;
            return;
        }
        ConfirmButton.IsEnabled =
            ResultName.Text.Trim().Length > 0
            || ResultAbbr.Text.Trim().Length > 0
            || ResultBody.Text.Trim().Length > 0;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        PromptBox.IsEnabled = !busy;
        TemplateCombo.IsEnabled = !busy;
        GenerateButton.Content = busy ? L("Manager.AiGenerating") : L("Manager.AiGenerate");
        UpdateGenerateEnabled();
        UpdateConfirmEnabled();
    }

    private async void OnGenerate(object sender, RoutedEventArgs e)
    {
        var input = PromptBox.Text.Trim();
        if (input.Length == 0 || _busy) return;
        if (App.InSmoke) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetBusy(true);
        StatusText.Foreground = (Brush)FindResource("Brush.TextMuted");
        StatusText.Text = L("Manager.AiGenerating");

        try
        {
            var system = AiSnippetGenerator.SystemPrompt(LocalizationService.Instance.Culture.Name);
            var user = AiSnippetGenerator.UserPrompt(_templateId, input);
            var raw = await AiChatClient.CompleteAsync(_cfg, system, user, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;
            var draft = AiSnippetGenerator.ParseResponse(raw);
            ApplyDraft(draft);
            StatusText.Text = "";
        }
        catch (OperationCanceledException) { /* superseded or closed */ }
        catch (Exception ex)
        {
            StatusText.Foreground = (Brush)FindResource("Brush.Error");
            StatusText.Text = string.Format(L("Manager.AiFailed"), FriendlyError(ex));
        }
        finally
        {
            if (!ct.IsCancellationRequested) SetBusy(false);
        }
    }

    private void ApplyDraft(AiSnippetDraft draft)
    {
        ResultName.Text = draft.Name;
        ResultAbbr.Text = draft.Abbr;
        ResultBody.Text = draft.Body;
        ResultPanel.Visibility = Visibility.Visible;
        UpdateConfirmEnabled();
    }

    private static string FriendlyError(Exception ex) => ex switch
    {
        HttpRequestException http => http.Message,
        FormatException => L("Manager.AiBadResponse"),
        InvalidOperationException inv => inv.Message,
        _ => ex.Message,
    };

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!ConfirmButton.IsEnabled) return;
        var body = ResultBody.Text;
        // Match the live body: placeholders only work when UseVariables is on.
        _result = new AiSnippetDraft(
            ResultName.Text.Trim(),
            AiSnippetGenerator.SanitizeAbbr(ResultAbbr.Text),
            body,
            AiSnippetGenerator.LooksLikePlaceholders(body));
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
