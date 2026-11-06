using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using QuickText.Core.Localization;
using QuickText.Core.Models;
using QuickText.Core.Persistence;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>Edit one category's snippet JSON with schema validation, optional external editor,
/// and a copyable AI prompt that points at the on-disk path.</summary>
public partial class CategoryJsonWindow : Window
{
    private readonly string _categoryName;
    private string _filePath;
    private string _baseline = "";
    private bool _dirty;

    /// <summary>--smoke only: inflate the chrome without a live category.</summary>
    public CategoryJsonWindow() : this("__smoke__", "") { }

    public CategoryJsonWindow(string categoryName, string filePath)
    {
        _categoryName = categoryName ?? "";
        _filePath = filePath ?? "";
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.CapHeightToMonitor(this);
        WindowTheming.ApplyFlowDirection(this);
        if (!App.InSmoke) WindowTheming.PlaceOnActiveMonitor(this);

        var loc = LocalizationService.Instance;
        Title = string.Format(loc["CatJson.Title"], _categoryName);
        TitleText.Text = Title;
        HintText.Text = loc["CatJson.Hint"];
        PathLabel.Text = loc["CatJson.Path"];
        PathBox.Text = _filePath;
        ReloadButton.Content = loc["Manager.Reload"];
        OpenExternalButton.Content = loc["CatJson.OpenExternal"];
        CopyPromptButton.Content = loc["CatJson.CopyAiPrompt"];
        SaveButton.Content = loc["CatJson.Save"];
        CloseButton.Content = loc["Dialog.Cancel"];

        JsonBox.TextChanged += (_, _) =>
        {
            _dirty = !string.Equals(JsonBox.Text, _baseline, StringComparison.Ordinal);
            if (StatusText.Foreground != (Brush)FindResource("Brush.TextMuted"))
                ClearStatus();
        };
        Closing += OnWindowClosing;

        if (!App.InSmoke) LoadFromDisk(showStatus: false);
    }

    private static string L(string key) => LocalizationService.Instance[key];

    private void ClearStatus()
    {
        StatusText.Text = "";
        StatusText.Foreground = (Brush)FindResource("Brush.TextMuted");
    }

    private void SetStatus(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = error
            ? (Brush)FindResource("Brush.Error")
            : (Brush)FindResource("Brush.TextMuted");
    }

    private void LoadFromDisk(bool showStatus)
    {
        string text;
        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
        {
            text = CategoryJson.Format(Array.Empty<Snippet>());
            SetStatus(L("CatJson.FileMissing"), error: false);
        }
        else
        {
            text = File.ReadAllText(_filePath);
            if (showStatus) SetStatus(L("CatJson.Reloaded"), error: false);
            else ClearStatus();
        }
        _baseline = text;
        JsonBox.Text = text;
        _dirty = false;
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        if (_dirty && !ConfirmDiscardOrSave()) return;
        // External edits may have landed; refresh the resolved path in case the index moved.
        if (AppState.Current.Store.TryGetCategoryFilePath(_categoryName, out var path))
        {
            _filePath = path;
            PathBox.Text = path;
        }
        LoadFromDisk(showStatus: true);
    }

    private void OnOpenExternal(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            SetStatus(L("CatJson.FileMissing"), error: true);
            return;
        }
        try
        {
            if (!File.Exists(_filePath))
                File.WriteAllText(_filePath, CategoryJson.Format(Array.Empty<Snippet>()));
            Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
        }
        catch
        {
            SetStatus(L("CatJson.OpenFailed"), error: true);
        }
    }

    private void OnCopyPrompt(object sender, RoutedEventArgs e)
    {
        var path = string.IsNullOrEmpty(_filePath) ? _categoryName + ".json" : _filePath;
        var prompt = string.Format(L("CatJson.AiPrompt"), path);
        try
        {
            System.Windows.Clipboard.SetText(prompt);
            SetStatus(L("CatJson.PromptCopied"), error: false);
        }
        catch
        {
            SetStatus(L("CatJson.OpenFailed"), error: true);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e) => TrySave();

    private bool TrySave()
    {
        if (!CategoryJson.TryValidate(JsonBox.Text, out var snippets, out var issue))
        {
            SetStatus(FormatIssue(issue!), error: true);
            return false;
        }

        var state = AppState.Current;
        var cats = state.SnapshotCategories();
        var cat = cats.FirstOrDefault(c => c.Name == _categoryName);
        if (cat == null)
        {
            SetStatus(L("Manager.SaveFailed"), error: true);
            return false;
        }

        SnippetOrder.BackfillCreatedAt(snippets!);
        cat.Snippets = snippets!;
        state.MarkSelfWrite();
        try
        {
            state.Store.SaveCategory(cat);
            state.ReloadData();
        }
        catch
        {
            SetStatus(L("Manager.SaveFailed"), error: true);
            return false;
        }

        if (state.Store.TryGetCategoryFilePath(_categoryName, out var path))
        {
            _filePath = path;
            PathBox.Text = path;
        }

        // Re-read the canonical formatting we just wrote.
        if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
            _baseline = File.ReadAllText(_filePath);
        else
            _baseline = CategoryJson.Format(snippets!);
        JsonBox.Text = _baseline;
        _dirty = false;
        SetStatus(L("CatJson.Saved"), error: false);
        return true;
    }

    private static string FormatIssue(CategoryJson.Issue issue) => issue.Kind switch
    {
        CategoryJson.IssueKind.InvalidJson =>
            string.Format(L("CatJson.InvalidJson"), issue.Detail ?? ""),
        CategoryJson.IssueKind.NotArray => L("CatJson.NotArray"),
        CategoryJson.IssueKind.EmptyName =>
            string.Format(L("CatJson.InvalidName"), issue.Index),
        CategoryJson.IssueKind.EmptyId =>
            string.Format(L("CatJson.InvalidId"), issue.Index),
        CategoryJson.IssueKind.BadId =>
            string.Format(L("CatJson.InvalidId"), issue.Index),
        CategoryJson.IssueKind.BadOutputMode =>
            string.Format(L("CatJson.InvalidOutputMode"), issue.Index),
        _ => L("CatJson.NotArray"),
    };

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.InSmoke || !_dirty) return;
        if (!ConfirmDiscardOrSave()) e.Cancel = true;
    }

    /// <returns>false when the user cancelled and the pending action must abort.</returns>
    private bool ConfirmDiscardOrSave()
    {
        bool? choice = AppDialog.ConfirmSaveDiscard(this, L("App.Name"), L("Manager.UnsavedConfirm"),
            L("CatJson.Save"), L("Manager.DontSave"), L("Dialog.Cancel"));
        if (choice == true) return TrySave();
        if (choice == false)
        {
            _dirty = false;
            return true;
        }
        return false;
    }
}
