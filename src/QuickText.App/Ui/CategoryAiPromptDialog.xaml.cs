using System.Windows;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

/// <summary>Shows the library-level AI prompt (index.json + category file rules) so the user can
/// copy it into Codex / Cursor / etc. The box stays editable so they can append their request.</summary>
public partial class CategoryAiPromptDialog : Window
{
    public CategoryAiPromptDialog()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
        WindowTheming.PlaceOnActiveMonitor(this);
        var loc = LocalizationService.Instance;
        Title = loc["CatMgr.AiCreate"];
        TitleText.Text = loc["CatMgr.AiCreate"];
        HintText.Text = loc["CatMgr.AiCreateHint"];
        CopyButton.Content = loc["CatJson.CopyAiPrompt"];
        CloseButton.Content = loc["Dialog.Cancel"];
        Loaded += (_, _) =>
        {
            PromptBox.Focus();
            PromptBox.CaretIndex = PromptBox.Text.Length;
        };
    }

    public static void Show(Window owner, string prompt)
    {
        var d = new CategoryAiPromptDialog { Owner = owner };
        d.PromptBox.Text = prompt ?? "";
        d.ShowDialog();
    }

    private static string L(string key) => LocalizationService.Instance[key];

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(PromptBox.Text ?? "");
            StatusText.Text = L("CatJson.PromptCopied");
            StatusText.Visibility = Visibility.Visible;
        }
        catch
        {
            StatusText.Text = L("CatJson.OpenFailed");
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Error");
            StatusText.Visibility = Visibility.Visible;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
