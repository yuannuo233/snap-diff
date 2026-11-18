using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32; // 注意：这里的 SaveFileDialog/OpenFolderDialog 来自 WPF，不是注册表
using SnapDiff.Core;

namespace SnapDiff.App;

/// <summary>
/// 主窗口：左侧步骤导航 + 右侧内容卡片，四步向导（拍快照 A → 去装软件 → 拍快照 B → 看对比结果）。
/// 所有的扫描/对比都跑在后台线程（Task.Run）里，
/// 界面只通过 Progress&lt;T&gt; 接收进度通知——所以窗口不会卡死。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>结果树每个分组节点最多显示多少个结果条目（太多会拖慢界面；配合搜索框缩小范围）。</summary>
    private const int MaxItemsPerGroup = 300;

    // ---------- 结果树的画笔（冻结的纯色画笔，代码里直接用） ----------
    private static readonly Brush MonoRowBrush = Freeze(0x1F, 0x23, 0x28);   // 树叶子行文字（路径，等宽）
    private static readonly Brush HeaderTextBrush = Freeze(0x6B, 0x72, 0x80); // 分组表头文字
    private static readonly Brush HeaderBgBrush = Freeze(0xF8, 0xF9, 0xFB);  // 分组表头底色
    private static readonly Brush NoteTextBrush = Freeze(0x9A, 0xA1, 0xA9);  // 提示行文字

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private SnapshotOptions? _optionsA;     // 快照 A 的拍摄范围（快照 B 必须用同一份）
    private string? _pathA;                 // 快照 A 文件路径
    private string? _pathB;                 // 快照 B 文件路径
    private DiffResult? _diff;              // 对比结果
    private CancellationTokenSource? _cts;  // 取消扫描用
    private int _currentStep = 1;           // 当前向导步骤
    private int _maxReachedStep = 1;        // 已经到达过的最远步骤（导航只允许往回点）
    private readonly HashSet<int> _completedSteps = new(); // 已完成的步骤（导航项右侧打勾）
    private bool _navSyncing;               // 代码设置导航选中态时抑制事件
    private bool _langSyncing;              // 代码设置语言下拉时抑制事件

    public MainWindow()
    {
        InitializeComponent();
        ExclusionBox.Text = string.Join(Environment.NewLine, SnapshotOptions.DefaultExcludedRegistryPrefixes);
        ResetFolderList();
        if (!IsRunningAsAdmin()) AdminHint.Visibility = Visibility.Visible;

        Loc.LanguageChanged += ApplyLocalization;
        ApplyLocalization();
        ShowStep(1);
    }

    protected override void OnClosed(EventArgs e)
    {
        Loc.LanguageChanged -= ApplyLocalization;
        base.OnClosed(e);
    }

    // ====================== 多语言 ======================

    /// <summary>把界面上所有文字按当前语言设置一遍（语言切换时整个界面即时刷新）。</summary>
    private void ApplyLocalization()
    {
        Title = Loc.T("App.Title");
        TitleText.Text = Loc.T("App.Name");
        NavText1.Text = Loc.T("Nav.Step1");
        NavText2.Text = Loc.T("Nav.Step2");
        NavText3.Text = Loc.T("Nav.Step3");
        NavText4.Text = Loc.T("Nav.Step4");
        SidebarReadonly.Text = Loc.T("Sidebar.Readonly");
        AboutBtnText.Text = Loc.T("Menu.About");

        // 语言下拉框（语言名用各自语言的“本名”，选择时同步当前语言）
        _langSyncing = true;
        LangZhItem.Content = Loc.T("Menu.LangZh");
        LangEnItem.Content = Loc.T("Menu.LangEn");
        LangBox.SelectedIndex = Loc.CurrentLanguage == Loc.EnUs ? 1 : 0;
        _langSyncing = false;

        GroupRegistry.Text = Loc.T("Group.Registry");
        HkcuCheck.Content = Loc.T("Check.Hkcu");
        HklmCheck.Content = Loc.T("Check.Hklm");
        AdminHint.Text = Loc.T("Label.AdminHint");
        ExclusionLabel.Text = Loc.T("Label.Exclusions");
        BtnResetExclusions.Content = Loc.T("Btn.ResetExclusions");
        ExclusionHint.Text = Loc.T("Label.ExclusionHint");

        GroupFolders.Text = Loc.T("Group.Folders");
        BtnAddFolder.Content = Loc.T("Btn.AddFolder");
        BtnRemoveFolder.Content = Loc.T("Btn.RemoveFolder");
        BtnResetFolders.Content = Loc.T("Btn.ResetFolders");

        GroupAdvanced.Text = Loc.T("Group.Advanced");
        HashCheck.Content = Loc.T("Check.Hash");

        BtnStartA.Content = Loc.T("Btn.StartA");
        BtnCancelA.Content = Loc.T("Btn.Cancel");

        Step2Header.Text = Loc.T("Step2.Header");
        Step2Body.Text = Loc.T("Step2.Body");
        Step2SavedTo.Text = Loc.T("Msg.SavedTo");
        BtnGoStep3.Content = Loc.T("Btn.GoStep3");
        BtnBackStep1.Content = Loc.T("Btn.BackStep1");

        Step3Header.Text = Loc.T("Step3.Header");
        ScopeLabel.Text = Loc.T("Label.Scope");
        BtnStartB.Content = Loc.T("Btn.StartB");
        BtnCancelB.Content = Loc.T("Btn.Cancel");

        StatAddedLabel.Text = Loc.T("Tree.Added");
        StatRemovedLabel.Text = Loc.T("Tree.Removed");
        StatModifiedLabel.Text = Loc.T("Tree.Modified");
        SearchLabel.Text = Loc.T("Label.Search");
        BtnClearSearch.Content = Loc.T("Btn.Clear");
        BtnExportMd.Content = Loc.T("Btn.ExportMd");
        BtnExportCsv.Content = Loc.T("Btn.ExportCsv");
        BtnRestart.Content = Loc.T("Btn.Restart");

        if (_diff is not null) RefreshSummaryText();
    }

    private void OnLangSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_langSyncing || LangBox.SelectedIndex < 0) return;
        Loc.SetLanguage(LangBox.SelectedIndex == 0 ? Loc.ZhCn : Loc.EnUs);
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, Loc.T("About.Text"), Loc.T("About.Title"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ====================== 步骤导航 ======================

    /// <summary>切换到某一步：更新面板可见性、导航选中态和完成勾选。</summary>
    private void ShowStep(int step)
    {
        _currentStep = step;
        Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

        _navSyncing = true;
        Nav.SelectedIndex = step - 1;
        _navSyncing = false;

        UpdateNavChecks();
    }

    /// <summary>导航点击：只允许跳到已经到达过的步骤，否则弹回当前步骤。</summary>
    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navSyncing) return;
        int target = Nav.SelectedIndex + 1;
        if (target == _currentStep) return;

        if (target >= 1 && target <= _maxReachedStep)
        {
            ShowStep(target);
        }
        else
        {
            // 还没走到的步骤不能点：把选中态弹回当前步骤
            _navSyncing = true;
            Nav.SelectedIndex = _currentStep - 1;
            _navSyncing = false;
        }
    }

    /// <summary>已完成的步骤在导航项右侧打绿色勾。</summary>
    private void UpdateNavChecks()
    {
        StepCheck1.Visibility = _completedSteps.Contains(1) ? Visibility.Visible : Visibility.Collapsed;
        StepCheck2.Visibility = _completedSteps.Contains(2) ? Visibility.Visible : Visibility.Collapsed;
        StepCheck3.Visibility = _completedSteps.Contains(3) ? Visibility.Visible : Visibility.Collapsed;
        StepCheck4.Visibility = _completedSteps.Contains(4) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ====================== 第 1 步：拍快照 A ======================

    private void OnResetExclusions(object sender, RoutedEventArgs e) =>
        ExclusionBox.Text = string.Join(Environment.NewLine, SnapshotOptions.DefaultExcludedRegistryPrefixes);

    private void ResetFolderList()
    {
        DirList.Items.Clear();
        foreach (var dir in SnapshotOptions.GetDefaultFileSystemRoots()) DirList.Items.Add(dir);
    }

    private void OnResetDirs(object sender, RoutedEventArgs e) => ResetFolderList();

    private void OnAddDir(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("Dialog.ChooseFolder") };
        if (dialog.ShowDialog(this) == true) DirList.Items.Add(dialog.FolderName);
    }

    private void OnRemoveDir(object sender, RoutedEventArgs e)
    {
        if (DirList.SelectedIndex >= 0) DirList.Items.RemoveAt(DirList.SelectedIndex);
    }

    /// <summary>把界面上的选择收集成 SnapshotOptions。</summary>
    private SnapshotOptions CollectOptions()
    {
        var options = new SnapshotOptions();
        if (HkcuCheck.IsChecked == true) options.RegistryRoots.Add("HKEY_CURRENT_USER");
        if (HklmCheck.IsChecked == true) options.RegistryRoots.Add("HKEY_LOCAL_MACHINE");

        foreach (var line in ExclusionBox.Text.Split('\n'))
        {
            var prefix = line.Trim();
            if (prefix.Length > 0) options.ExcludedRegistryPrefixes.Add(prefix);
        }

        foreach (var item in DirList.Items)
        {
            if (item is string dir && !string.IsNullOrWhiteSpace(dir))
                options.FileSystemRoots.Add(dir.Trim());
        }

        options.ComputeSha256 = HashCheck.IsChecked == true;
        return options;
    }

    private async void OnStartSnapshotA(object sender, RoutedEventArgs e)
    {
        var options = CollectOptions();
        if (options.RegistryRoots.Count == 0 && options.FileSystemRoots.Count == 0)
        {
            MessageBox.Show(this, Loc.T("Msg.NeedScope"), "SnapDiff",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = AskSavePath(
            Loc.T("Dialog.SaveA"),
            string.Format(CultureInfo.InvariantCulture, Loc.T("Dialog.NameA"),
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)),
            SnapshotFile.FileExtension);
        if (path is null) return;

        _optionsA = options;
        var ok = await CaptureSnapshotAsync(path, options, ProgressTextA, ProgressPanelA, BtnStartA, Step1Input);
        if (!ok) return;

        _pathA = path;
        SnapshotAPathText.Text = path;
        ScopeSummaryText.Text = DescribeOptions(options);
        _completedSteps.Add(1);
        _maxReachedStep = Math.Max(_maxReachedStep, 2);
        ShowStep(2);
    }

    /// <summary>
    /// 通用的“拍快照”流程：扫描 → 边扫边写文件 → 统计落盘。
    /// 全程在后台线程执行；界面控件只通过 Progress&lt;T&gt; 收进度。
    /// 返回 true 表示成功，false 表示取消或出错。
    /// </summary>
    private async Task<bool> CaptureSnapshotAsync(
        string path,
        SnapshotOptions options,
        TextBlock progressText,
        StackPanel progressPanel,
        Button startButton,
        UIElement? inputPanel)
    {
        var metadata = new SnapshotMetadata
        {
            CreatedAtUtc = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            AppVersion = "0.1",
            Options = options,
        };

        // Progress<T> 必须在 UI 线程上创建：它的回调会自动调度回界面线程
        // （声明成 IProgress<T>：Report 是显式接口实现，Progress<T> 类型的变量上调不到）
        IProgress<ScanProgress> progress = new Progress<ScanProgress>(p =>
        {
            progressText.Text = string.Format(CultureInfo.CurrentCulture, Loc.T("Progress.Format"),
                PhaseName(p.Phase), p.ProcessedCount, p.SkippedCount, p.Message ?? "");
        });

        _cts = new CancellationTokenSource();
        startButton.IsEnabled = false;
        if (inputPanel is not null) inputPanel.IsEnabled = false;
        progressPanel.Visibility = Visibility.Visible;

        try
        {
            await Task.Run(() =>
            {
                var counts = new SnapshotCounts();
                using var writer = new SnapshotWriter(path, metadata);
                using var registryReader = new WindowsRegistryReader();

                progress.Report(new ScanProgress { Phase = ScanPhase.Registry, Message = Loc.T("Progress.StartRegistry") });
                new RegistrySnapshotter(registryReader)
                    .Capture(options, counts, writer.WriteRegistry, progress, _cts.Token);

                progress.Report(new ScanProgress { Phase = ScanPhase.FileSystem, Message = Loc.T("Progress.StartFiles") });
                new FileSnapshotter()
                    .Capture(options, counts, writer.WriteFile, progress, _cts.Token);

                progress.Report(new ScanProgress { Phase = ScanPhase.Saving, Message = Loc.T("Progress.Saving") });
                writer.Complete(counts);
            });

            progressText.Text = Loc.T("Progress.Done");
            return true;
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(path);
            MessageBox.Show(this, Loc.T("Msg.Cancelled"), "SnapDiff",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        catch (Exception ex)
        {
            DeleteQuietly(path);
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Loc.T("Msg.SnapshotError"), ex.Message),
                "SnapDiff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            startButton.IsEnabled = true;
            if (inputPanel is not null) inputPanel.IsEnabled = true;
            progressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnCancelScanA(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void OnCancelScanB(object sender, RoutedEventArgs e) => _cts?.Cancel();

    // ====================== 第 2 / 3 步 ======================

    private void OnGoToStep3(object sender, RoutedEventArgs e)
    {
        _completedSteps.Add(2);
        _maxReachedStep = Math.Max(_maxReachedStep, 3);
        ShowStep(3);
    }

    private void OnRestartFromStep2(object sender, RoutedEventArgs e) => ResetAll();

    private async void OnStartSnapshotB(object sender, RoutedEventArgs e)
    {
        if (_optionsA is null) return;
        var path = AskSavePath(
            Loc.T("Dialog.SaveB"),
            string.Format(CultureInfo.InvariantCulture, Loc.T("Dialog.NameB"),
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)),
            SnapshotFile.FileExtension);
        if (path is null) return;

        // 关键：快照 B 必须使用与快照 A 完全相同的范围（_optionsA）
        var ok = await CaptureSnapshotAsync(path, _optionsA, ProgressTextB, ProgressPanelB, BtnStartB, null);
        if (!ok) return;

        _pathB = path;
        await RunDiffAsync();
    }

    /// <summary>对比两张快照文件，完成后进入第 4 步。</summary>
    private async Task RunDiffAsync()
    {
        if (_pathA is null || _pathB is null) return;

        BtnStartB.IsEnabled = false;
        ProgressPanelB.Visibility = Visibility.Visible;

        IProgress<ScanProgress> progress = new Progress<ScanProgress>(p =>
            ProgressTextB.Text = string.Format(CultureInfo.CurrentCulture, Loc.T("Progress.DiffFormat"),
                PhaseName(p.Phase), p.ProcessedCount));

        try
        {
            _diff = await Task.Run(() => SnapshotComparer.DiffFiles(_pathA, _pathB, progress, CancellationToken.None));
            _completedSteps.Add(3);
            _maxReachedStep = Math.Max(_maxReachedStep, 4);
            RefreshSummaryText();
            BuildResultTree(SearchBox.Text);
            ShowStep(4);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Loc.T("Msg.DiffError"), ex.Message),
                "SnapDiff", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            ProgressPanelB.Visibility = Visibility.Collapsed;
            BtnStartB.IsEnabled = true;
        }
    }

    // ====================== 第 4 步：结果展示 ======================

    /// <summary>刷新顶部三张统计卡（新增/删除/修改）和“注册表 X · 文件 Y”明细。</summary>
    private void RefreshSummaryText()
    {
        var s = _diff!.Summary;
        StatAddedValue.Text = (s.AddedRegistry + s.AddedFiles).ToString("N0", CultureInfo.CurrentCulture);
        StatRemovedValue.Text = (s.RemovedRegistry + s.RemovedFiles).ToString("N0", CultureInfo.CurrentCulture);
        StatModifiedValue.Text = (s.ModifiedRegistry + s.ModifiedFiles).ToString("N0", CultureInfo.CurrentCulture);
        SummaryDetail.Text = string.Format(CultureInfo.CurrentCulture, Loc.T("Summary.Detail"),
            s.AddedRegistry + s.RemovedRegistry + s.ModifiedRegistry,
            s.AddedFiles + s.RemovedFiles + s.ModifiedFiles);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => BuildResultTree(SearchBox.Text);

    private void OnClearSearch(object sender, RoutedEventArgs e) => SearchBox.Text = "";

    /// <summary>
    /// 构建结果树：按 变化类型 → 分类（文件/注册表）→ 根 → 目录/父键 分组。
    /// 有搜索词时只显示匹配的条目（匹配范围包括路径、值名、数据）。
    /// </summary>
    private void BuildResultTree(string filter)
    {
        ResultTree.Items.Clear();
        ResultEmpty.Visibility = Visibility.Collapsed;
        if (_diff is null) return;

        filter = filter.Trim();
        bool Matches(string text) => filter.Length == 0 || text.Contains(filter, StringComparison.OrdinalIgnoreCase);

        AddKindNodes(ChangeKind.Added, Loc.T("Tree.Added"), Matches);
        AddKindNodes(ChangeKind.Removed, Loc.T("Tree.Removed"), Matches);
        AddKindNodes(ChangeKind.Modified, Loc.T("Tree.Modified"), Matches);

        if (ResultTree.Items.Count == 0)
        {
            // 空状态提示：完全没变化 / 或搜索无结果
            ResultEmpty.Text = filter.Length > 0 ? Loc.T("Tree.NoMatch") : Loc.T("Tree.NoChanges");
            ResultEmpty.Visibility = Visibility.Visible;
        }
    }

    /// <summary>一个变化类型（新增/删除/修改）下面挂“文件”和“注册表”两个分类节点。</summary>
    private void AddKindNodes(ChangeKind kind, string kindName, Func<string, bool> matches)
    {
        var regChanges = _diff!.RegistryChanges
            .Where(c => c.Kind == kind && matches(RegSearchText(c))).ToList();
        var fileChanges = _diff.FileChanges
            .Where(c => c.Kind == kind && matches(FileSearchText(c))).ToList();
        if (regChanges.Count == 0 && fileChanges.Count == 0) return;

        var kindNode = NewHeaderNode(string.Format(CultureInfo.CurrentCulture,
            Loc.T("Tree.KindNode"), kindName, regChanges.Count, fileChanges.Count), expanded: true);
        if (fileChanges.Count > 0) AddFileCategory(kindNode, fileChanges);
        if (regChanges.Count > 0) AddRegistryCategory(kindNode, regChanges);
        ResultTree.Items.Add(kindNode);
    }

    /// <summary>文件分类：按根目录 → 父目录分组，条目挂在最内层。</summary>
    private void AddFileCategory(TreeViewItem kindNode, List<FileChange> changes)
    {
        var categoryNode = NewHeaderNode(string.Format(CultureInfo.CurrentCulture, Loc.T("Tree.FilesNode"), changes.Count));

        foreach (var rootGroup in changes
                     .GroupBy(c => c.Main.RootPath, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var rootNode = NewHeaderNode(rootGroup.Key);
            foreach (var dirGroup in rootGroup
                         .GroupBy(c => ParentDirectory(c.Main.RelativePath), StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var label = dirGroup.Key.Length == 0 ? Loc.T("Tree.RootDir") : dirGroup.Key;
                var items = dirGroup
                    .OrderBy(c => c.Main.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var dirNode = NewHeaderNode(string.Format(CultureInfo.CurrentCulture, Loc.T("Tree.GroupNode"), label, items.Count));
                AddLeavesWithCap(dirNode, items.Count, i => RenderFileChange(items[i]));
                if (dirNode.Items.Count > 0) rootNode.Items.Add(dirNode);
            }
            if (rootNode.Items.Count > 0) categoryNode.Items.Add(rootNode);
        }
        if (categoryNode.Items.Count > 0) kindNode.Items.Add(categoryNode);
    }

    /// <summary>注册表分类：按根键（hive）→ 父键路径分组。</summary>
    private void AddRegistryCategory(TreeViewItem kindNode, List<RegistryChange> changes)
    {
        var categoryNode = NewHeaderNode(string.Format(CultureInfo.CurrentCulture, Loc.T("Tree.RegistryNode"), changes.Count));

        foreach (var hiveGroup in changes
                     .GroupBy(c => HiveOf(c.Main.KeyPath), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var hiveNode = NewHeaderNode(hiveGroup.Key);
            foreach (var keyGroup in hiveGroup
                         .GroupBy(ParentKeyLabel, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var items = keyGroup
                    .OrderBy(c => c.Main.ValueName ?? "", StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var keyNode = NewHeaderNode(string.Format(CultureInfo.CurrentCulture, Loc.T("Tree.GroupNode"), keyGroup.Key, items.Count));
                AddLeavesWithCap(keyNode, items.Count, i => RenderRegistryChange(items[i]));
                if (keyNode.Items.Count > 0) hiveNode.Items.Add(keyNode);
            }
            if (hiveNode.Items.Count > 0) categoryNode.Items.Add(hiveNode);
        }
        if (categoryNode.Items.Count > 0) kindNode.Items.Add(categoryNode);
    }

    /// <summary>把一个组的条目挂到节点下；超过上限时追加一条“已省略”提示行。</summary>
    private void AddLeavesWithCap(TreeViewItem parent, int total, Func<int, string> render)
    {
        int shown = Math.Min(total, MaxItemsPerGroup);
        for (int i = 0; i < shown; i++) parent.Items.Add(NewRowNode(render(i)));
        if (total > shown)
        {
            parent.Items.Add(NewNoteNode(string.Format(CultureInfo.CurrentCulture,
                Loc.T("Tree.Truncated"), total, shown)));
        }
    }

    // ---------- 结果树的节点工厂（表头行 / 数据行 / 提示行） ----------

    /// <summary>分组表头节点：浅灰底、灰字 11.5 SemiBold（相当于表格的表头行）。</summary>
    private static TreeViewItem NewHeaderNode(string text, bool expanded = false)
    {
        return new TreeViewItem
        {
            Header = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = HeaderTextBrush,
            },
            Background = HeaderBgBrush,
            IsExpanded = expanded,
        };
    }

    /// <summary>数据行节点：等宽字体（路径/数字），行高与悬停由 TreeViewItem 隐式模板负责。</summary>
    private static TreeViewItem NewRowNode(string text)
    {
        return new TreeViewItem
        {
            Header = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                Foreground = MonoRowBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
    }

    /// <summary>提示行节点（如“已省略 N 项”）：灰色小字。</summary>
    private static TreeViewItem NewNoteNode(string text)
    {
        return new TreeViewItem
        {
            Header = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                Foreground = NoteTextBrush,
            },
        };
    }

    // ---------- 结果树的显示文案 ----------

    private string RenderFileChange(FileChange c) => c.Kind switch
    {
        ChangeKind.Added => string.Format(CultureInfo.CurrentCulture,
            "{0}   ({1}, {2}, {3})",
            c.After!.RelativePath, Loc.T("Tree.Added"),
            FormatUtils.HumanSize(c.After.Size), FormatUtils.FormatUtcTime(c.After.ModifiedTimeUtc)),
        ChangeKind.Removed => string.Format(CultureInfo.CurrentCulture,
            "{0}   ({1}, {2})",
            c.Before!.RelativePath, Loc.T("Tree.Removed"), FormatUtils.HumanSize(c.Before.Size)),
        _ => string.Format(CultureInfo.CurrentCulture,
            "{0}   ({1}: {2} → {3}, {4} → {5})",
            c.After!.RelativePath, Loc.T("Tree.Size"),
            FormatUtils.HumanSize(c.Before!.Size), FormatUtils.HumanSize(c.After.Size),
            FormatUtils.FormatUtcTime(c.Before.ModifiedTimeUtc), FormatUtils.FormatUtcTime(c.After.ModifiedTimeUtc)),
    };

    private string RenderRegistryChange(RegistryChange c)
    {
        var main = c.Main;
        if (main.ValueName is null)
        {
            // “键本身”记录：显示键路径 + 新建/删除提示
            return string.Format(CultureInfo.CurrentCulture, "{0}   ({1})",
                main.KeyPath, c.Kind == ChangeKind.Added ? Loc.T("Tree.NewKey") : Loc.T("Tree.RemovedKey"));
        }

        var name = main.ValueName.Length == 0 ? Loc.T("Tree.DefaultValue") : main.ValueName;
        return c.Kind switch
        {
            ChangeKind.Added => $"{name} = {Short(main.Data)}",
            ChangeKind.Removed => $"{name} = {Short(c.Before!.Data)}",
            _ => $"{name}: {Short(c.Before!.Data)} → {Short(main.Data)}",
        };
    }

    // ---------- 分组辅助 ----------

    /// <summary>取相对路径的父目录；没有父目录返回空串。</summary>
    private static string ParentDirectory(string relativePath)
    {
        int idx = relativePath.LastIndexOf('\\');
        return idx > 0 ? relativePath[..idx] : "";
    }

    /// <summary>变化分组用的“父键”：值变化挂它所在的键；新建/删除的键挂它的父键。</summary>
    private string ParentKeyLabel(RegistryChange c)
    {
        var path = c.Main.KeyPath;
        if (c.Main.ValueName is not null) return path;
        int idx = path.LastIndexOf('\\');
        return idx > 0 ? path[..idx] : Loc.T("Tree.HiveRoot");
    }

    /// <summary>取键路径的根键部分（第一个 \ 之前），如 HKEY_CURRENT_USER。</summary>
    private static string HiveOf(string keyPath)
    {
        int idx = keyPath.IndexOf('\\');
        return idx > 0 ? keyPath[..idx] : keyPath;
    }

    /// <summary>注册表变化的搜索文本（含路径、值名、类型、数据）。</summary>
    private static string RegSearchText(RegistryChange c)
    {
        return string.Join(" ", new[]
        {
            c.Before?.KeyPath, c.After?.KeyPath,
            c.Before?.ValueName, c.After?.ValueName,
            c.Before?.TypeName, c.After?.TypeName,
            c.Before?.Data, c.After?.Data,
        }.Where(p => !string.IsNullOrEmpty(p)));
    }

    /// <summary>文件变化的搜索文本（含根、相对路径、哈希）。</summary>
    private static string FileSearchText(FileChange c)
    {
        return string.Join(" ", new[]
        {
            c.Before?.RootPath, c.After?.RootPath,
            c.Before?.RelativePath, c.After?.RelativePath,
            c.Before?.Sha256, c.After?.Sha256,
        }.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static string Short(string text)
    {
        var oneLine = text.Replace("\r", " ").Replace("\n", " ");
        return oneLine.Length <= 160 ? oneLine : oneLine[..160] + "…";
    }

    // ====================== 导出 / 重新开始 ======================

    private void OnExportMarkdown(object sender, RoutedEventArgs e) =>
        ExportWith(MarkdownExporter.Export, Loc.T("Dialog.ExportMd"), "md");

    private void OnExportCsv(object sender, RoutedEventArgs e) =>
        ExportWith(CsvExporter.Export, Loc.T("Dialog.ExportCsv"), "csv");

    private void ExportWith(Action<DiffResult, string> exporter, string title, string extension)
    {
        if (_diff is null) return;
        var path = AskSavePath(
            title,
            string.Format(CultureInfo.InvariantCulture, Loc.T("Dialog.NameReport"),
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)),
            "." + extension,
            filter: (extension == "csv" ? "CSV (*.csv)|*.csv|" : "Markdown (*.md)|*.md|") + Loc.T("Dialog.AllFiles"));
        if (path is null) return;

        try
        {
            exporter(_diff, path);
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Loc.T("Msg.Exported"), path),
                "SnapDiff", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Loc.T("Msg.ExportFailed"), ex.Message),
                "SnapDiff", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRestart(object sender, RoutedEventArgs e) => ResetAll();

    private void ResetAll()
    {
        _diff = null;
        _pathA = null;
        _pathB = null;
        _optionsA = null;
        _completedSteps.Clear();
        _maxReachedStep = 1;
        ResultTree.Items.Clear();
        ResultEmpty.Visibility = Visibility.Collapsed;
        SearchBox.Text = "";
        StatAddedValue.Text = "0";
        StatRemovedValue.Text = "0";
        StatModifiedValue.Text = "0";
        SummaryDetail.Text = "";
        ShowStep(1);
    }

    // ====================== 通用小工具 ======================

    /// <summary>弹出保存对话框，返回选中的路径；用户取消返回 null。</summary>
    private static string? AskSavePath(string title, string defaultName, string defaultExt, string? filter = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter ?? Loc.T("Dialog.SnapFilter"),
            FileName = defaultName,
            DefaultExt = defaultExt,
            AddExtension = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 删不掉就算了（比如被占用），不影响继续使用
        }
    }

    private string PhaseName(ScanPhase phase) => phase switch
    {
        ScanPhase.Registry => Loc.T("Phase.Registry"),
        ScanPhase.FileSystem => Loc.T("Phase.Files"),
        ScanPhase.Saving => Loc.T("Phase.Save"),
        ScanPhase.Diffing => Loc.T("Phase.Diff"),
        ScanPhase.Completed => Loc.T("Phase.Done"),
        _ => Loc.T("Phase.Prepare"),
    };

    private static bool IsRunningAsAdmin()
    {
        try
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>把拍摄范围整理成一段人能读懂的描述（第 3 步展示用）。</summary>
    private static string DescribeOptions(SnapshotOptions? o)
    {
        if (o is null) return "";
        var registry = o.RegistryRoots.Count == 0 ? Loc.T("Scope.None") : string.Join(", ", o.RegistryRoots);
        var folders = o.FileSystemRoots.Count == 0 ? Loc.T("Scope.None") : string.Join(" ; ", o.FileSystemRoots);
        var hash = o.ComputeSha256 ? Loc.T("Scope.HashOn") : Loc.T("Scope.HashOff");
        return Loc.T("Scope.RegistryPrefix") + registry
             + string.Format(CultureInfo.CurrentCulture, Loc.T("Scope.Exclusions"), o.ExcludedRegistryPrefixes.Count)
             + Loc.T("Scope.FoldersPrefix") + folders + hash;
    }
}
