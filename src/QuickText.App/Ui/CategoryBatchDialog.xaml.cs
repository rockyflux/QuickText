using System;
using System.Collections.Generic;
using System.Windows;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

/// <summary>Multi-line category names (one per line) for batch create / initialize defaults.</summary>
public partial class CategoryBatchDialog : Window
{
    public CategoryBatchDialog()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.CapHeightToMonitor(this);
        WindowTheming.ApplyFlowDirection(this);
        var loc = LocalizationService.Instance;
        OkButton.Content = loc["Dialog.OK"];
        CancelButton.Content = loc["Dialog.Cancel"];
        NamesBox.TextChanged += (_, _) => Validate();
        Validate();
        Loaded += (_, _) => NamesBox.Focus();
    }

    /// <summary>Trimmed unique names the user confirmed, or null if cancelled.</summary>
    public IReadOnlyList<string>? Result { get; private set; }

    public static IReadOnlyList<string>? Show(Window owner, string title, string hint, string initialText)
    {
        var d = new CategoryBatchDialog { Owner = owner, Title = title };
        d.TitleText.Text = title;
        d.HintText.Text = hint;
        d.NamesBox.Text = initialText ?? "";
        d.Validate();
        return d.ShowDialog() == true ? d.Result : null;
    }

    /// <summary>One non-empty trimmed name per line; first wins on case-insensitive duplicates.</summary>
    internal static List<string> ParseNames(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var name = raw.Trim();
            if (name.Length == 0 || !seen.Add(name)) continue;
            list.Add(name);
        }
        return list;
    }

    private void Validate() => OkButton.IsEnabled = ParseNames(NamesBox.Text).Count > 0;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = ParseNames(NamesBox.Text);
        if (Result.Count == 0) return;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
