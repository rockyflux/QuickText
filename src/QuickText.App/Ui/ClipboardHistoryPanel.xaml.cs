using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QuickText.App.Interop;
using QuickText.Core.Clipboard;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

public partial class ClipboardHistoryPanel : Window
{
    private const int PreviewChars = 120;
    private IntPtr _target;
    private bool _settling;
    private int _summonId;
    private bool _sending;

    private sealed record Row(string Text, string Preview);

    public ClipboardHistoryPanel()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        };
    }

    public void ShowForCurrentForeground()
    {
        var captured = PasteTarget.Capture();
        if (captured != IntPtr.Zero)
            _target = captured;
        else if (!PasteTarget.IsPasteable(_target))
            _target = IntPtr.Zero;

        Reload();
        PositionAndCap();
        Show();

        _settling = true;
        int summon = ++_summonId;
        App.BringToFront(this, () =>
        {
            if (summon != _summonId) return;
            _settling = false;
        });
        Activate();
        if (ItemList.Items.Count > 0)
        {
            ItemList.SelectedIndex = 0;
            ItemList.Focus();
            ItemList.ScrollIntoView(ItemList.SelectedItem);
        }
    }

    private void Reload()
    {
        var store = (Application.Current as App)?.ClipboardHistory
                    ?? new ClipboardHistoryStore(Core.AppPaths.ClipboardHistoryPath);
        // Preview-only strings in the ListBox keep the visual tree small; full text stays on Row.
        var rows = store.Items.Select(e => new Row(e.Text, PreviewOf(e.Text))).ToList();
        ItemList.ItemsSource = rows;
        bool empty = rows.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ItemList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string PreviewOf(string text)
    {
        var oneLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (oneLine.Length <= PreviewChars) return oneLine;
        return oneLine[..PreviewChars] + "…";
    }

    private void PositionAndCap()
    {
        // Clipboard history always tracks the typing context — not the search panel's
        // window/fixed placement. Prefer the text caret; fall back to the cursor.
        Width = 420;
        double estH = Math.Min(MaxHeight > 0 && !double.IsInfinity(MaxHeight) ? MaxHeight : 360, 360);
        if (!TryPlaceAtCaret(estH) && !TryPlaceAtCursor(estH))
        {
            var wa = WindowTheming.CursorWorkArea(this);
            MaxHeight = Math.Min(480, Math.Max(160, wa.Height - 24));
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + Math.Min(80, wa.Height / 8);
        }
    }

    /// <summary>Just below the focused app's text caret (above it when there is no room).</summary>
    private bool TryPlaceAtCaret(double estHeight)
    {
        if (_target == IntPtr.Zero) return false;
        uint tid = NativeMethods.GetWindowThreadProcessId(_target, out _);
        var gti = new NativeMethods.GUITHREADINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.GUITHREADINFO>()
        };
        if (!NativeMethods.GetGUIThreadInfo(tid, ref gti) || gti.hwndCaret == IntPtr.Zero) return false;
        var pt = new NativeMethods.POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
        if (!NativeMethods.ClientToScreen(gti.hwndCaret, ref pt)) return false;

        var mon = NativeMethods.MonitorFromWindow(_target, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var wa = mon != IntPtr.Zero
            ? WindowTheming.MonitorWorkAreaFor(this, mon)
            : WindowTheming.CursorWorkArea(this);
        MaxHeight = Math.Min(480, Math.Max(160, wa.Height - 24));
        estHeight = Math.Min(estHeight, MaxHeight);

        if (!WindowTheming.TryDeviceToDip(this, pt.X, pt.Y, out var caretBottom)) return false;
        int caretTopPx = pt.Y - (gti.rcCaret.Bottom - gti.rcCaret.Top);
        if (!WindowTheming.TryDeviceToDip(this, pt.X, caretTopPx, out var caretTop)) return false;

        double left = Math.Max(wa.Left, Math.Min(caretBottom.X, wa.Right - Width));
        double top = caretBottom.Y + 8;
        if (top + estHeight > wa.Bottom)
            top = Math.Max(wa.Top, caretTop.Y - estHeight - 8);
        Left = left;
        Top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - 120));
        return true;
    }

    /// <summary>When the target exposes no caret (some Electron apps), open by the mouse cursor.</summary>
    private bool TryPlaceAtCursor(double estHeight)
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return false;
        if (!WindowTheming.TryDeviceToDip(this, pt.X, pt.Y, out var cursor)) return false;
        var wa = WindowTheming.CursorWorkArea(this);
        MaxHeight = Math.Min(480, Math.Max(160, wa.Height - 24));
        estHeight = Math.Min(estHeight, MaxHeight);

        double left = Math.Max(wa.Left, Math.Min(cursor.X, wa.Right - Width));
        double top = cursor.Y + 12;
        if (top + estHeight > wa.Bottom)
            top = Math.Max(wa.Top, cursor.Y - estHeight - 12);
        Left = left;
        Top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - 120));
        return true;
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PasteSelected();
            e.Handled = true;
        }
    }

    private void OnListClick(object sender, MouseButtonEventArgs e)
    {
        // Click selects first; paste on the same up once a row is selected.
        if (ItemList.SelectedItem != null)
            PasteSelected();
    }

    private void PasteSelected()
    {
        if (_sending || ItemList.SelectedItem is not Row row) return;
        _sending = true;
        try
        {
            Hide();
            var settings = AppState.Current.Settings;
            if (!PasteTarget.IsPasteable(_target)) _target = IntPtr.Zero;
            bool noTarget = _target == IntPtr.Zero;
            bool copyOnly = settings.CopyToClipboardOnly || noTarget;
            bool autoSend = settings.AutoSend && !copyOnly;
            bool restore = settings.RestoreClipboard;

            if (copyOnly)
            {
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    PasteEngine.CopyText(row.Text);
                    if (noTarget && !settings.CopyToClipboardOnly)
                        (Application.Current as App)?.Notify(
                            LocalizationService.Instance["Msg.NoTarget"], warning: true);
                }), DispatcherPriority.Background);
                return;
            }

            void CopyInstead()
            {
                PasteEngine.CopyText(row.Text);
                (Application.Current as App)?.Notify(
                    LocalizationService.Instance["Msg.TargetLost"], warning: true);
            }

            ForegroundTarget.WhenForeground(_target, () =>
            {
                if (!PasteEngine.Paste(row.Text, restore, autoSend))
                    CopyInstead();
            }, CopyInstead);
        }
        finally { _sending = false; }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (App.InSmoke || _settling) return;
        Hide();
    }
}
