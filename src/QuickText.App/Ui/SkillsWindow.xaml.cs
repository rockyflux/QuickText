using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuickText.Core;
using QuickText.Core.Localization;
using QuickText.Core.Skills;

namespace QuickText.App.Ui;

/// <summary>Browse local Agent Skills (tree + favorites pane), or pick favorites to import.</summary>
public partial class SkillsWindow : Window
{
    private readonly bool _importMode;
    private readonly SkillFavoriteStore _favorites;
    private string _rootKey;
    private string _skillsRoot;
    private IReadOnlyList<AgentSkillInfo> _skills = Array.Empty<AgentSkillInfo>();
    private readonly HashSet<string> _treeSelected = new(StringComparer.OrdinalIgnoreCase);
    private string? _treeAnchor;
    private readonly DispatcherTimer _filterDebounce;
    private int _scanGeneration;
    private bool _busy;
    private bool _rootComboReady;

    public IReadOnlyList<SkillFavoriteItem> Imported { get; private set; } = Array.Empty<SkillFavoriteItem>();

    public SkillsWindow() : this(importMode: false) { }

    public SkillsWindow(bool importMode)
    {
        _importMode = importMode;
        _favorites = new SkillFavoriteStore(AppPaths.SkillsFavoritesPath);
        _rootKey = _favorites.ActiveRoot;
        _skillsRoot = SkillRoots.Resolve(_rootKey);
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);

        _filterDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _filterDebounce.Tick += (_, _) =>
        {
            _filterDebounce.Stop();
            RebuildLists(rebuildTree: !_importMode);
        };

        var loc = LocalizationService.Instance;
        Title = loc["Skills.Title"];
        TitleText.Text = loc["Skills.Title"];
        PathText.Text = _skillsRoot;
        FilterPlaceholder.Text = loc["Manager.Filter"];
        FavoriteSelectedButton.Content = loc["Skills.Favorite"];
        FavHeader.Text = loc["Skills.FavoritesHeader"];
        TreeLoadingText.Text = loc["Skills.Loading"];
        ImportButton.Content = loc["Skills.ImportSelected"];
        CtxFavorite.Header = loc["Skills.Favorite"];
        CtxOpenTree.Header = loc["Skills.OpenLocal"];
        CtxOpenRoot.Header = loc["Skills.OpenFolder"];
        CtxOpenFav.Header = loc["Skills.OpenLocal"];
        CtxRenameFav.Header = loc["Manager.Rename"];
        CtxRemoveFav.Header = loc["Skills.RemoveFavorite"];

        foreach (var key in SkillRoots.RelativeKeys)
            RootCombo.Items.Add(key);
        RootCombo.SelectedItem = SkillRoots.RelativeKeys.FirstOrDefault(k =>
            string.Equals(k, _rootKey, StringComparison.OrdinalIgnoreCase)) ?? SkillRoots.DefaultKey;
        _rootComboReady = true;

        if (importMode)
        {
            WindowTheming.PlaceOnActiveMonitor(this);
            MaxHeight = 900;
            TreePane.Visibility = Visibility.Collapsed;
            TreeColumn.Width = new GridLength(0);
            SplitColumn.Width = new GridLength(0);
            FavColumn.Width = new GridLength(1, GridUnitType.Star);
            FavoriteSelectedButton.Visibility = Visibility.Collapsed;
            RefreshButton.Visibility = Visibility.Collapsed;
            FavToolbar.Visibility = Visibility.Collapsed;
            RootCombo.Visibility = Visibility.Collapsed;
            PathText.Visibility = Visibility.Collapsed;
            OpenRootButton.Visibility = Visibility.Collapsed;
            ImportButton.Visibility = Visibility.Visible;
            RebuildFavoritesOnly();
        }
        else
        {
            WindowTheming.MaximizeOnActiveMonitor(this);
            RebuildFavoritesOnly();
            _ = ReloadTreeAsync();
        }

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
    }

    private static string L(string key) => LocalizationService.Instance[key];

    private void OnRootChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_rootComboReady || _importMode) return;
        if (RootCombo.SelectedItem is not string key) return;
        key = SkillRoots.NormalizeKey(key);
        if (string.Equals(key, _rootKey, StringComparison.OrdinalIgnoreCase)) return;
        _rootKey = key;
        _skillsRoot = SkillRoots.Resolve(key);
        PathText.Text = _skillsRoot;
        _favorites.ActiveRoot = key;
        _treeSelected.Clear();
        _treeAnchor = null;
        _ = ReloadTreeAsync();
    }

    private async Task ReloadTreeAsync()
    {
        int gen = Interlocked.Increment(ref _scanGeneration);
        SetBusy(true);
        TreeEmptyText.Visibility = Visibility.Collapsed;
        TreeLoadingText.Visibility = Visibility.Visible;
        SkillTree.Items.Clear();

        string root = _skillsRoot;
        IReadOnlyList<AgentSkillInfo> skills;
        try
        {
            skills = await Task.Run(() => AgentSkillScanner.Scan(root)).ConfigureAwait(true);
        }
        catch
        {
            skills = Array.Empty<AgentSkillInfo>();
        }

        if (gen != _scanGeneration) return;

        _skills = skills;
        TreeLoadingText.Visibility = Visibility.Collapsed;
        RebuildTree(FilterBox.Text.Trim());
        SetBusy(false);
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        _favorites.Reload();
        RebuildFavoritesOnly();
        if (!_importMode) _ = ReloadTreeAsync();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        FavoriteSelectedButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        RootCombo.IsEnabled = !busy;
        FilterBox.IsEnabled = !busy || _importMode;
    }

    private void RebuildLists(bool rebuildTree)
    {
        if (rebuildTree && !_importMode)
            RebuildTree(FilterBox.Text.Trim());
        RebuildFavoritesOnly();
    }

    private void RebuildFavoritesOnly()
    {
        var filter = FilterBox.Text.Trim();
        var favRows = _favorites.Items
            .Where(i => FilterMatch(FavoriteLabel(i), i.Body, i.Description, filter))
            .OrderBy(FavoriteLabel, StringComparer.CurrentCultureIgnoreCase)
            .Select(i => new FavRow(i, FavoriteLabel(i),
                string.IsNullOrWhiteSpace(i.Description) ? i.Body : i.Description!))
            .ToList();
        FavoriteList.ItemsSource = favRows;
        FavEmptyText.Text = favRows.Count == 0
            ? (string.IsNullOrEmpty(filter) ? L("Skills.NoFavorites") : L("Skills.Empty"))
            : "";
        FavEmptyText.Visibility = favRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RebuildTree(string filter)
    {
        SkillTree.Items.Clear();
        var roots = SkillTreeBuilder.Build(_skills,
            s => FilterMatch(s.SkillName, s.SkillFilePath, s.Description, filter));

        if (!Directory.Exists(_skillsRoot) && _skills.Count == 0)
        {
            TreeEmptyText.Text = L("Skills.MissingFolder");
            TreeEmptyText.Visibility = Visibility.Visible;
        }
        else if (roots.Count == 0)
        {
            TreeEmptyText.Text = _busy ? "" : L("Skills.Empty");
            TreeEmptyText.Visibility = !_busy ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            TreeEmptyText.Visibility = Visibility.Collapsed;
            foreach (var node in roots)
                SkillTree.Items.Add(MakeTreeItem(node));
        }
        SyncTreeSelectionVisuals();
    }

    private TreeViewItem MakeTreeItem(SkillTreeNode node)
    {
        var item = new TreeViewItem { Tag = node, IsExpanded = false };
        if (node.Skill is { } skill)
        {
            item.Header = SkillHeader(skill);
            AutomationProperties.SetName(item, skill.SkillName);
            item.ToolTip = string.IsNullOrWhiteSpace(skill.Description)
                ? skill.SkillFilePath
                : skill.Description + "\n" + skill.SkillFilePath;
        }
        else
        {
            item.Header = node.Label;
            AutomationProperties.SetName(item, node.Label);
        }

        foreach (var child in node.Children)
            item.Items.Add(MakeTreeItem(child));
        return item;
    }

    private string SkillHeader(AgentSkillInfo skill) =>
        (_favorites.IsFavorite(skill.SkillFilePath) ? "★ " : "") + skill.SkillName;

    private static string FavoriteLabel(SkillFavoriteItem item) =>
        string.IsNullOrWhiteSpace(item.Alias) ? item.Name : item.Alias!;

    private static bool FilterMatch(string label, string path, string? description, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        return label.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
               || path.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || (!string.IsNullOrEmpty(description)
                   && description.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        _filterDebounce.Stop();
        _filterDebounce.Start();
    }

    private void OnTreeLeftDown(object sender, MouseButtonEventArgs e)
    {
        // Never steal the expander chevron — that was why some nodes refused to expand.
        if (FindAncestor<ToggleButton>(e.OriginalSource as DependencyObject) is not null)
            return;

        var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (item?.Tag is not SkillTreeNode node) return;

        if (node.Skill is null)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                _treeSelected.Clear();
                _treeAnchor = null;
                SyncTreeSelectionVisuals();
            }
            return;   // let TreeView expand/collapse folder rows
        }

        var path = node.Skill.SkillFilePath;
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control))
        {
            if (!_treeSelected.Add(path)) _treeSelected.Remove(path);
            _treeAnchor = path;
        }
        else if (mods.HasFlag(ModifierKeys.Shift) && _treeAnchor is not null)
        {
            var ordered = FlattenSkills(SkillTree.Items).ToList();
            int a = ordered.FindIndex(p => string.Equals(p, _treeAnchor, StringComparison.OrdinalIgnoreCase));
            int b = ordered.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (a >= 0 && b >= 0)
            {
                _treeSelected.Clear();
                int lo = Math.Min(a, b), hi = Math.Max(a, b);
                for (int i = lo; i <= hi; i++) _treeSelected.Add(ordered[i]);
            }
        }
        else
        {
            _treeSelected.Clear();
            _treeSelected.Add(path);
            _treeAnchor = path;
        }

        item.IsSelected = true;
        SyncTreeSelectionVisuals();
        // Skill rows that also have children must still expand — don't mark Handled.
        if (!item.HasItems)
            e.Handled = true;
    }

    private void OnTreeRightDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ToggleButton>(e.OriginalSource as DependencyObject) is not null)
            return;
        var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (item?.Tag is not SkillTreeNode { Skill: { } skill }) return;
        if (!_treeSelected.Contains(skill.SkillFilePath))
        {
            _treeSelected.Clear();
            _treeSelected.Add(skill.SkillFilePath);
            _treeAnchor = skill.SkillFilePath;
            SyncTreeSelectionVisuals();
        }
        item.IsSelected = true;
    }

    private IEnumerable<string> FlattenSkills(ItemCollection items)
    {
        foreach (TreeViewItem item in items)
        {
            if (item.Tag is SkillTreeNode { Skill: { } skill })
                yield return skill.SkillFilePath;
            foreach (var child in FlattenSkills(item.Items))
                yield return child;
        }
    }

    private void SyncTreeSelectionVisuals()
    {
        foreach (TreeViewItem root in SkillTree.Items)
            ApplyTreeVisual(root);
    }

    private void ApplyTreeVisual(TreeViewItem item)
    {
        if (item.Tag is SkillTreeNode { Skill: { } skill })
        {
            bool selected = _treeSelected.Contains(skill.SkillFilePath);
            item.Background = selected
                ? (Brush)FindResource("Brush.RowSelected")
                : Brushes.Transparent;
            item.Header = SkillHeader(skill);
        }
        foreach (TreeViewItem child in item.Items)
            ApplyTreeVisual(child);
    }

    private IReadOnlyList<AgentSkillInfo> SelectedTreeSkills() =>
        _skills.Where(s => _treeSelected.Contains(s.SkillFilePath)).ToList();

    private void OnTreeMenuOpened(object sender, RoutedEventArgs e)
    {
        bool anySkill = SelectedTreeSkills().Count > 0;
        CtxFavorite.IsEnabled = !_busy && anySkill;
        CtxOpenTree.IsEnabled = anySkill || Directory.Exists(_skillsRoot);
        CtxOpenRoot.IsEnabled = true;
    }

    private void OnCtxFavorite(object sender, RoutedEventArgs e) => FavoriteSkills(SelectedTreeSkills());
    private void OnFavoriteSelected(object sender, RoutedEventArgs e) => FavoriteSkills(SelectedTreeSkills());

    private void OnOpenRoot(object sender, RoutedEventArgs e) => OpenInExplorer(_skillsRoot, createIfMissing: true);

    private void OnOpenTreeLocal(object sender, RoutedEventArgs e)
    {
        var skills = SelectedTreeSkills();
        if (skills.Count > 0)
        {
            OpenInExplorer(skills[0].FolderPath, createIfMissing: false);
            return;
        }
        // Folder-only selection: open that relative folder under the root when possible.
        if (SkillTree.SelectedItem is TreeViewItem { Tag: SkillTreeNode node }
            && !string.IsNullOrEmpty(node.RelativeFolder))
        {
            var folder = Path.Combine(_skillsRoot, node.RelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            OpenInExplorer(folder, createIfMissing: false);
            return;
        }
        OpenInExplorer(_skillsRoot, createIfMissing: true);
    }

    private void OnOpenFavoriteLocal(object sender, RoutedEventArgs e)
    {
        if (FavoriteList.SelectedItem is not FavRow row)
        {
            FlashTip(L("Skills.SelectHint"));
            return;
        }
        var folder = Path.GetDirectoryName(row.Item.Body);
        OpenInExplorer(string.IsNullOrEmpty(folder) ? row.Item.Body : folder, createIfMissing: false);
    }

    private void OpenInExplorer(string path, bool createIfMissing)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                FlashTip(L("Skills.OpenFailed"));
                return;
            }
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }
            if (!Directory.Exists(path))
            {
                if (!createIfMissing)
                {
                    FlashTip(L("Skills.OpenFailed"));
                    return;
                }
                Directory.CreateDirectory(path);
            }
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            FlashTip(L("Skills.OpenFailed"));
        }
    }

    private void FavoriteSkills(IReadOnlyList<AgentSkillInfo> skills)
    {
        if (skills.Count == 0)
        {
            FlashTip(L("Skills.SelectHint"));
            return;
        }

        var payload = skills.Select(s => (s.SkillName, s.SkillFilePath, (string?)s.Description)).ToList();
        FavoriteSelectedButton.IsEnabled = false;
        _ = Task.Run(() =>
        {
            var store = new SkillFavoriteStore(AppPaths.SkillsFavoritesPath);
            return store.FavoriteMany(payload);
        }).ContinueWith(t =>
        {
            int added = t.IsCompletedSuccessfully ? t.Result : skills.Count;
            _favorites.Reload();
            RebuildFavoritesOnly();
            SyncTreeSelectionVisuals();
            FavoriteSelectedButton.IsEnabled = !_busy;
            FlashTip(string.Format(L("Skills.FavoritedTip"), Math.Max(added, skills.Count)));
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private IEnumerable<FavRow> SelectedFavRows() => FavoriteList.SelectedItems.Cast<FavRow>();

    private void OnFavMenuOpened(object sender, RoutedEventArgs e)
    {
        bool any = FavoriteList.SelectedItems.Count > 0;
        CtxOpenFav.IsEnabled = FavoriteList.SelectedItems.Count >= 1;
        CtxRenameFav.IsEnabled = FavoriteList.SelectedItems.Count == 1;
        CtxRemoveFav.IsEnabled = any;
    }

    private void OnFavoriteDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FavoriteList.SelectedItems.Count == 1) OnRenameFavorite(sender, e);
    }

    private void OnRenameFavorite(object sender, RoutedEventArgs e)
    {
        if (FavoriteList.SelectedItem is not FavRow row) return;
        var next = AppDialog.Prompt(this, L("Manager.Rename"), L("Skills.RenamePrompt"), row.Item.Name);
        if (next is null) return;
        if (!_favorites.SetName(row.Item.Body, next))
        {
            FlashTip(L("Skills.RenameFailed"));
            return;
        }
        RebuildFavoritesOnly();
        FlashTip(L("Skills.RenamedTip"));
    }

    private void OnRemoveFavorites(object sender, RoutedEventArgs e)
    {
        var bodies = SelectedFavRows().Select(r => r.Item.Body).ToList();
        if (bodies.Count == 0) return;
        _favorites.UnfavoriteMany(bodies);
        foreach (var b in bodies) _treeSelected.Remove(b);
        RebuildFavoritesOnly();
        SyncTreeSelectionVisuals();
        FlashTip(string.Format(L("Skills.RemovedTip"), bodies.Count));
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var selected = SelectedFavRows().Select(r => r.Item).ToList();
        if (selected.Count == 0)
        {
            AppDialog.Alert(this, L("Skills.Title"), L("Skills.ImportNone"));
            return;
        }
        Imported = selected;
        DialogResult = true;
        Close();
    }

    private void FlashTip(string text)
    {
        TipText.Text = text;
        TipText.BeginAnimation(UIElement.OpacityProperty, null);
        TipText.Opacity = 1;
        TipText.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(1.6)))
            {
                BeginTime = TimeSpan.FromSeconds(0.9),
            });
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var d = start; d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is T match) return match;
        return null;
    }

    private sealed record FavRow(SkillFavoriteItem Item, string Label, string Subtitle);
}
