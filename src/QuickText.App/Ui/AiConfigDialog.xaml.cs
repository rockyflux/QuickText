using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QuickText.Core.Localization;
using QuickText.Core.Settings;

namespace QuickText.App.Ui;

public partial class AiConfigDialog : Window
{
    private sealed record ProviderItem(string Id, string Name);

    private string _id;
    private AiProviderConfig? _result;
    private string? _prevProvider;
    private bool _loading;
    private CancellationTokenSource? _fetchCts;
    private int _fetchSeq;
    private readonly DispatcherTimer _fetchDebounce;

    public AiConfigDialog()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.CapHeightToMonitor(this);
        WindowTheming.ApplyFlowDirection(this);
        _id = Guid.NewGuid().ToString("N");
        _fetchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _fetchDebounce.Tick += (_, _) => { _fetchDebounce.Stop(); _ = RefreshModelsAsync(); };
        ProviderCombo.ItemsSource = ProviderChoices();
        // Selecting fires OnProviderChanged → fills defaults + schedules a live /models fetch.
        ProviderCombo.SelectedValue = AiProviders.OpenAi;
        NameBox.TextChanged += (_, _) => Validate();
        ApiKeyBox.PasswordChanged += (_, _) => ScheduleModelFetch();
        BaseUrlBox.LostKeyboardFocus += (_, _) => ScheduleModelFetch();
        Validate();
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _fetchDebounce.Stop();
            _fetchCts?.Cancel();
            _fetchCts?.Dispose();
        };
    }

    public AiProviderConfig? Result => _result;

    /// <summary>Edit an existing config (or null to add). Returns the confirmed config, or null if cancelled.</summary>
    public static AiProviderConfig? Show(Window owner, AiProviderConfig? existing)
    {
        var d = new AiConfigDialog { Owner = owner };
        if (existing != null) d.Load(existing);
        return d.ShowDialog() == true ? d.Result : null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NameBox.Focus();
        ScheduleModelFetch();
    }

    private void Load(AiProviderConfig src)
    {
        _loading = true;
        try
        {
            _id = src.Id;
            NameBox.Text = src.Name ?? "";
            var provider = AiProviders.Normalize(src.Provider);
            ProviderCombo.SelectedValue = provider;
            _prevProvider = provider;
            ApiKeyBox.Password = src.ApiKey ?? "";
            var storedUrl = (src.BaseUrl ?? "").Trim();
            BaseUrlBox.Text = storedUrl.Length > 0 ? storedUrl : AiProviders.DefaultBaseUrl(provider);
            ApplyModelList(AiProviders.KnownModels(provider), src.Model, provider);
            UpdateBaseUrlPlaceholder(provider);
        }
        finally { _loading = false; }
        Validate();
    }

    private static ProviderItem[] ProviderChoices()
    {
        var loc = LocalizationService.Instance;
        return
        [
            new(AiProviders.Magpie, loc["Settings.AiProviderMagpie"]),
            new(AiProviders.OpenAi, loc["Settings.AiProviderOpenAi"]),
            new(AiProviders.Anthropic, loc["Settings.AiProviderAnthropic"]),
            new(AiProviders.Azure, loc["Settings.AiProviderAzure"]),
            new(AiProviders.DeepSeek, loc["Settings.AiProviderDeepSeek"]),
            new(AiProviders.Custom, loc["Settings.AiProviderCustom"]),
        ];
    }

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderCombo.SelectedValue is not string provider) { Validate(); return; }
        provider = AiProviders.Normalize(provider);
        if (!_loading)
        {
            var curUrl = BaseUrlBox.Text.Trim();
            if (curUrl.Length == 0 || AiProviders.IsDefaultBaseUrl(_prevProvider, curUrl))
                BaseUrlBox.Text = AiProviders.DefaultBaseUrl(provider);

            // Magpie's product name is the natural config name when the box is still empty.
            if (provider == AiProviders.Magpie && NameBox.Text.Trim().Length == 0)
                NameBox.Text = "magpie";

            var curModel = (ModelCombo.Text ?? "").Trim();
            var prevDefault = AiProviders.DefaultModel(_prevProvider);
            var replaceModel = curModel.Length == 0
                || string.Equals(curModel, prevDefault, StringComparison.OrdinalIgnoreCase);
            ApplyModelList(
                AiProviders.KnownModels(provider),
                replaceModel ? AiProviders.DefaultModel(provider) : curModel,
                provider);
            ScheduleModelFetch();
        }
        UpdateBaseUrlPlaceholder(provider);
        _prevProvider = provider;
        Validate();
    }

    private void ApplyModelList(IReadOnlyList<string> models, string? preferred, string provider)
    {
        var list = models.Count > 0 ? models.ToList() : AiProviders.KnownModels(provider).ToList();
        var pick = (preferred ?? "").Trim();
        if (pick.Length == 0) pick = list.Count > 0 ? list[0] : AiProviders.DefaultModel(provider);
        // Keep a hand-typed id visible even when it isn't in the fetched set.
        if (pick.Length > 0 && !list.Any(m => string.Equals(m, pick, StringComparison.OrdinalIgnoreCase)))
            list.Insert(0, pick);
        ModelCombo.ItemsSource = list;
        ModelCombo.Text = pick;
    }

    private void UpdateBaseUrlPlaceholder(string provider) =>
        BaseUrlPlaceholder.Text = AiProviders.BaseUrlPlaceholder(provider);

    private void OnBaseUrlTextChanged(object sender, TextChangedEventArgs e)
    {
        Validate();
        if (!_loading) ScheduleModelFetch();
    }

    private void ScheduleModelFetch()
    {
        if (_loading || App.InSmoke) return;
        _fetchDebounce.Stop();
        _fetchDebounce.Start();
    }

    private async Task RefreshModelsAsync()
    {
        if (App.InSmoke) return;
        if (ProviderCombo.SelectedValue is not string providerRaw) return;
        var provider = AiProviders.Normalize(providerRaw);
        var baseUrl = BaseUrlBox.Text.Trim();
        if (baseUrl.Length == 0) baseUrl = AiProviders.DefaultBaseUrl(provider);
        if (baseUrl.Length == 0)
        {
            ModelStatus.Text = LocalizationService.Instance["Settings.AiModelsNeedUrl"];
            return;
        }

        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var ct = _fetchCts.Token;
        var seq = ++_fetchSeq;
        var preferred = (ModelCombo.Text ?? "").Trim();
        ModelStatus.Text = LocalizationService.Instance["Settings.AiModelsLoading"];

        try
        {
            var models = await AiModelListClient.FetchAsync(provider, baseUrl, ApiKeyBox.Password, ct)
                .ConfigureAwait(true);
            if (seq != _fetchSeq || ct.IsCancellationRequested) return;
            if (models.Count == 0)
            {
                ApplyModelList(AiProviders.KnownModels(provider), preferred, provider);
                ModelStatus.Text = LocalizationService.Instance["Settings.AiModelsEmpty"];
                return;
            }
            ApplyModelList(models, preferred, provider);
            ModelStatus.Text = string.Format(
                LocalizationService.Instance["Settings.AiModelsLoaded"], models.Count);
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception)
        {
            if (seq != _fetchSeq) return;
            ApplyModelList(AiProviders.KnownModels(provider), preferred, provider);
            ModelStatus.Text = LocalizationService.Instance["Settings.AiModelsFailed"];
        }
    }

    private void Validate()
    {
        bool ok = NameBox.Text.Trim().Length > 0 && ProviderCombo.SelectedValue is string;
        OkButton.IsEnabled = ok;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) return;
        var provider = AiProviders.Normalize(ProviderCombo.SelectedValue as string);
        _result = new AiProviderConfig
        {
            Id = _id,
            Name = name,
            Provider = provider,
            ApiKey = ApiKeyBox.Password ?? "",
            BaseUrl = BaseUrlBox.Text.Trim(),
            Model = (ModelCombo.Text ?? "").Trim(),
        };
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
