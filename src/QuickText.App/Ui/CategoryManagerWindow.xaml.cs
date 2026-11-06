using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickText.Core.Localization;
using QuickText.Core.Models;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>Dedicated category admin: rename / delete / color / snippet-sort / drag reorder,
/// plus batch add and initialize-from-defaults. Snippet bodies stay in the Manager.</summary>
public partial class CategoryManagerWindow : Window
{
    private static readonly (string Mode, string Key)[] SortModeItems =
    {
        (SnippetOrder.Manual, "Manager.SortManual"),
        (SnippetOrder.Created, "Manager.SortCreated"),
        (SnippetOrder.Name, "Manager.SortName"),
        (SnippetOrder.Usage, "Manager.SortUsage"),
        (SnippetOrder.LastUsed, "Manager.SortLastUsed"),
    };

    private List<Category> _cats = new();
    private Point _dragStart;
    private object? _dragItem;
    private bool _syncingSort;

    public CategoryManagerWindow()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
        WindowTheming.PlaceOnActiveMonitor(this);
        var loc = LocalizationService.Instance;
        TitleText.Text = loc["CatMgr.Title"];
        HintText.Text = loc["CatMgr.Hint"] + " " + loc["CatMgr.DoubleClickRename"];
        EmptyText.Text = loc["CatMgr.Empty"];
        BatchButton.Content = loc["CatMgr.BatchAdd"];
        InitButton.Content = loc["CatMgr.Initialize"];
        CloseButton.Content = loc["Dialog.OK"];
        foreach (var (mode, key) in SortModeItems)
            SortCombo.Items.Add(new ComboBoxItem { Content = loc[key], Tag = mode });
        AppState.Current.DataReloaded += OnDataReloaded;
        Closed += (_, _) => AppState.Current.DataReloaded -= OnDataReloaded;
        Reload();
    }

    private Category? Selected => Categories.SelectedItem as Category;

    private static string L(string key) => LocalizationService.Instance[key];

    private void OnDataReloaded()
    {
        if (App.InSmoke || !IsVisible) return;
        Dispatcher.BeginInvoke(Reload);
    }

    private void Reload(string? selectName = null)
    {
        var keep = selectName ?? Selected?.Name;
        // Snapshot so in-window edits don't mutate AppState until we write + ReloadData.
        _cats = AppState.Current.SnapshotCategories().ToList();
        Categories.ItemsSource = _cats;
        EmptyText.Visibility = _cats.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (keep != null) SelectByName(keep);
        else if (_cats.Count > 0) Categories.SelectedIndex = 0;
        Categories.Items.Refresh();
        SyncSortCombo();
        OpenJsonButton.IsEnabled = Selected != null;
    }

    private void SyncIndexes(string? selectName = null)
    {
        try { AppState.Current.ReloadData(); }
        catch { AppDialog.Alert(this, L("App.Name"), L("Manager.SaveFailed")); }
        Reload(selectName);
    }

    private void AlertSaveFailed() =>
        AppDialog.Alert(this, L("App.Name"), L("Manager.SaveFailed"));

    private void OnCategorySelected(object s, SelectionChangedEventArgs e)
    {
        SyncSortCombo();
        OpenJsonButton.IsEnabled = Selected != null;
    }

    private void OnOpenJson(object s, RoutedEventArgs e)
    {
        if (Selected is not { } c) return;
        if (!AppState.Current.Store.TryGetCategoryFilePath(c.Name, out var path))
        {
            // Brand-new category: ensure the snippet file exists so the editor / AI prompt have a path.
            AppState.Current.MarkSelfWrite();
            try { AppState.Current.Store.SaveCategory(c); }
            catch { AlertSaveFailed(); return; }
            if (!AppState.Current.Store.TryGetCategoryFilePath(c.Name, out path))
            {
                AlertSaveFailed();
                return;
            }
        }
        var w = new CategoryJsonWindow(c.Name, path) { Owner = this };
        w.ShowDialog();
        Reload(c.Name);
    }

    private void OnCategoryDoubleClick(object s, MouseButtonEventArgs e)
    {
        // Only rename when the double-click landed on a row (not empty list padding).
        if (e.OriginalSource is DependencyObject d && FindAncestor<ListBoxItem>(d) != null)
            OnRename(s, e);
    }

    private void SyncSortCombo()
    {
        _syncingSort = true;
        try
        {
            if (Selected is { } c)
            {
                SortCombo.IsEnabled = true;
                SortCombo.SelectedValue = SnippetOrder.Normalize(c.SortMode);
            }
            else
            {
                SortCombo.IsEnabled = false;
                SortCombo.SelectedIndex = -1;
            }
        }
        finally { _syncingSort = false; }
    }

    private void OnSortModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSort) return;
        if (Selected is not { } c) return;
        if (SortCombo.SelectedValue is not string mode) return;
        mode = SnippetOrder.Normalize(mode);
        var stored = mode == SnippetOrder.Manual ? "" : mode;
        if ((c.SortMode ?? "") == stored) return;
        var prev = c.SortMode ?? "";
        c.SortMode = stored;
        AppState.Current.MarkSelfWrite();
        try { AppState.Current.Store.SaveCategory(c); }
        catch
        {
            c.SortMode = prev;
            SyncSortCombo();
            AlertSaveFailed();
            return;
        }
        SyncIndexes(c.Name);
    }

    private void OnAddOne(object s, RoutedEventArgs e)
    {
        var name = AppDialog.Prompt(this, L("Manager.NewCategory"), L("Manager.CategoryName"), UniqueName());
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_cats.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            AppDialog.Alert(this, L("App.Name"), L("Manager.DuplicateCategory"));
            return;
        }
        AddEmptyCategories(new[] { name });
    }

    private string UniqueName()
    {
        var baseName = L("Manager.NewCategoryName");
        if (_cats.All(c => c.Name != baseName)) return baseName;
        for (int i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (_cats.All(c => c.Name != candidate)) return candidate;
        }
    }

    private void OnRename(object s, RoutedEventArgs e)
    {
        if (Selected is not { } c) return;
        var name = AppDialog.Prompt(this, L("Manager.RenameCategory"), L("Manager.CategoryName"), c.Name);
        if (string.IsNullOrWhiteSpace(name) || name == c.Name) return;
        if (_cats.Any(x => x != c && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            AppDialog.Alert(this, L("App.Name"), L("Manager.DuplicateCategory"));
            return;
        }
        var old = c.Name;
        c.Name = name;
        AppState.Current.MarkSelfWrite();
        try
        {
            AppState.Current.Store.SaveCategory(c);
            AppState.Current.MarkSelfWrite();
            AppState.Current.Store.DeleteCategory(old);
        }
        catch
        {
            c.Name = old;
            AlertSaveFailed();
            return;
        }
        SyncIndexes(name);
    }

    private void OnDelete(object s, RoutedEventArgs e)
    {
        if (Selected is not { } c) return;
        if (!AppDialog.Confirm(this, L("App.Name"),
                string.Format(L("Manager.DeleteCategoryConfirm"), c.Name), L("Manager.Delete"))) return;
        try
        {
            if (c.Snippets.Count > 0) MoveToTrash(c.Name, c.Snippets);
            AppState.Current.MarkSelfWrite();
            AppState.Current.Store.DeleteCategory(c.Name);
        }
        catch { AlertSaveFailed(); return; }
        SyncIndexes();
    }

    private static void MoveToTrash(string category, IEnumerable<Snippet> snippets)
    {
        var state = AppState.Current;
        state.MarkSelfWrite();
        var trash = state.Store.LoadTrash();
        foreach (var sn in snippets)
            trash.Add(new TrashEntry { Snippet = sn, Category = category });
        state.MarkSelfWrite();
        state.Store.SaveTrash(trash);
    }

    private void OnColor(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } c || sender is not Button b) return;
        var prev = c.Color;
        c.Color = (b.Tag as string) ?? "";
        AppState.Current.MarkSelfWrite();
        try { AppState.Current.Store.SaveCategory(c); }
        catch { c.Color = prev; AlertSaveFailed(); return; }
        SyncIndexes(c.Name);
    }

    private void OnBatchAdd(object s, RoutedEventArgs e) =>
        RunBatch(L("CatMgr.BatchAdd"), L("CatMgr.BatchHint"), "");

    private void OnInitialize(object s, RoutedEventArgs e)
    {
        // Full categories + sample snippets from embedded JSON (not empty name stubs).
        int added, skipped;
        try { (added, skipped) = AppState.Current.SeedBuiltinCategories(); }
        catch { AlertSaveFailed(); return; }

        // SeedBuiltinCategories already ReloadData'd when anything was written.
        Reload();
        AppDialog.Alert(this, L("App.Name"),
            string.Format(L("CatMgr.BatchResult"), added, skipped));
    }

    private void RunBatch(string title, string hint, string initial)
    {
        var names = CategoryBatchDialog.Show(this, title, hint, initial);
        if (names == null || names.Count == 0) return;
        AddEmptyCategories(names);
    }

    private void AddEmptyCategories(IEnumerable<string> names)
    {
        var nameList = names as IList<string> ?? names.ToList();
        var known = new HashSet<string>(_cats.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        int added = 0, skipped = 0;
        try
        {
            foreach (var name in nameList)
            {
                if (!known.Add(name))
                {
                    skipped++;
                    continue;
                }
                var cat = new Category { Name = name };
                AppState.Current.MarkSelfWrite();
                AppState.Current.Store.SaveCategory(cat);
                added++;
            }
        }
        catch { AlertSaveFailed(); return; }

        if (added > 0) SyncIndexes();
        else Reload();

        if (skipped > 0 || (added > 0 && nameList.Count > 1))
            AppDialog.Alert(this, L("App.Name"),
                string.Format(L("CatMgr.BatchResult"), added, skipped));
    }

    private void SelectByName(string name)
    {
        if (_cats.FirstOrDefault(c => c.Name == name) is { } c)
            Categories.SelectedItem = c;
    }

    private void OnClose(object s, RoutedEventArgs e) => Close();

    // ---------- drag reorder ----------
    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject =>
        TreeSearch.FindAncestor<T>(d);

    private void OnListPressed(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = e.OriginalSource is DependencyObject d && FindAncestor<ListBoxItem>(d) is { } item
            ? item.DataContext : null;
    }

    private void OnListMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop((DependencyObject)sender, item, DragDropEffects.Move);
    }

    private void OnCategoriesDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Category)) is not Category dragged) return;
        var target = e.OriginalSource is DependencyObject d && FindAncestor<ListBoxItem>(d) is { } it
            ? it.DataContext as Category : null;
        if (target == null || ReferenceEquals(dragged, target)) return;
        int from = _cats.IndexOf(dragged);
        int to = _cats.IndexOf(target);
        if (from < 0 || to < 0) return;
        _cats.RemoveAt(from);
        _cats.Insert(to, dragged);
        AppState.Current.MarkSelfWrite();
        try { AppState.Current.Store.ReorderCategories(_cats.Select(x => x.Name).ToList()); }
        catch { AlertSaveFailed(); Reload(); return; }
        SyncIndexes(dragged.Name);
    }
}
