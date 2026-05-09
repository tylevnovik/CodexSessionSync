using System.IO;
using System.Text;
using CodexSessionSync.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CodexSessionSync.WinUI;

public sealed class MainWindow : Window
{
    private TextBox _codexHomeBox = null!;
    private TextBox _backupDirBox = null!;
    private TextBox _sourceProviderBox = null!;
    private TextBox _targetProviderBox = null!;
    private RadioButton _modeMutual = null!;
    private RadioButton _modeOpenAi = null!;
    private RadioButton _modeMigrate = null!;
    private Button _previewBtn = null!;
    private Button _applyBtn = null!;
    private Button _defaultsBtn = null!;
    private CheckBox _confirmCheck = null!;
    private TextBox _outputBox = null!;

    private static SolidColorBrush B(byte a, byte r, byte g, byte b) => new(new Color { A = a, R = r, G = g, B = b });

    private static readonly SolidColorBrush BgDark = B(255, 26, 26, 46);
    private static readonly SolidColorBrush BgCard = B(255, 30, 30, 46);
    private static readonly SolidColorBrush BorderDim = B(255, 45, 45, 63);
    private static readonly SolidColorBrush FgWhite = B(255, 255, 255, 255);
    private static readonly SolidColorBrush FgDim = B(255, 209, 213, 219);
    private static readonly SolidColorBrush FgMuted = B(255, 156, 163, 175);
    private static readonly SolidColorBrush FgOutput = B(255, 229, 231, 235);
    private static readonly SolidColorBrush AccentBlue = B(255, 59, 130, 246);
    private static readonly SolidColorBrush AccentRed = B(255, 239, 68, 68);
    private static readonly SolidColorBrush AccentGray = B(255, 55, 65, 81);

    public MainWindow()
    {
        Title = "Codex Session Sync";
        BuildUI();
        ResetDefaults();
    }

    private void BuildUI()
    {
        var root = new Grid { Background = BgDark };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBar = new Grid { Padding = new Thickness(16, 0, 16, 0), Height = 48 };
        titleBar.Children.Add(new TextBlock
        {
            Text = "Codex Session Sync",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = FgWhite
        });
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        var scroll = new ScrollViewer { Padding = new Thickness(24, 8, 24, 24) };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var stack = new StackPanel { Spacing = 20, MaxWidth = 800 };

        stack.Children.Add(new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0), Children = {
            Tx("Sync Codex sessions across providers", "TitleTextBlockStyle", FgWhite),
            Tx("Default preview mode. Check confirm box before applying changes.", "CaptionTextBlockStyle", FgMuted)
        }});

        stack.Children.Add(Card("Config", new StackPanel { Spacing = 16, Children = {
            TwoCol(
                LblIn("Codex Home", out _codexHomeBox, "auto-detect..."),
                LblIn("Backup Directory", out _backupDirBox, "backup before write")) }}));

        _modeMutual = new RadioButton { Content = "Mutual sync", IsChecked = true, Foreground = FgWhite };
        _modeOpenAi = new RadioButton { Content = "OpenAI to all", Foreground = FgWhite };
        _modeMigrate = new RadioButton { Content = "Migrate", Foreground = FgWhite };
        stack.Children.Add(Card("Sync Mode", new StackPanel { Spacing = 16, Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _modeMutual, _modeOpenAi, _modeMigrate }},
            TwoCol(
                LblIn("Source Provider", out _sourceProviderBox, "openai"),
                LblIn("Target Provider", out _targetProviderBox, "openai")) }}));

        _previewBtn = Btn("Preview", 100, AccentBlue);
        _applyBtn = Btn("Apply", 110, AccentRed);
        _defaultsBtn = Btn("Reset", 90, AccentGray);
        _confirmCheck = new CheckBox { Content = "I have backed up", Foreground = FgWhite, VerticalAlignment = VerticalAlignment.Center };

        _previewBtn.Click += (_, _) => _ = RunSync(false);
        _applyBtn.Click += (_, _) => { if (_confirmCheck.IsChecked == true) _ = RunSync(true); else _outputBox.Text = "Please check confirm box first."; };
        _defaultsBtn.Click += (_, _) => ResetDefaults();

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actionRow.Children.Add(_previewBtn);
        actionRow.Children.Add(_applyBtn);
        actionRow.Children.Add(_defaultsBtn);
        actionRow.Children.Add(_confirmCheck);
        stack.Children.Add(actionRow);

        _outputBox = new TextBox
        {
            Background = new SolidColorBrush(new Color { A = 0, R = 0, G = 0, B = 0 }),
            Foreground = FgOutput,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Height = 300,
            Text = "Ready."
        };

        var outputSection = new StackPanel { Spacing = 8 };
        outputSection.Children.Add(Tx("Output", "BodyStrongTextBlockStyle", FgWhite, false));
        outputSection.Children.Add(_outputBox);
        stack.Children.Add(Card("", outputSection));

        scroll.Content = stack;
        this.Content = root;
    }

    private static TextBlock Tx(string text, string styleKey, Brush fg, bool styled = true)
    {
        return new TextBlock
        {
            Text = text,
            Style = styled ? (Style)Application.Current.Resources[styleKey] : null,
            Foreground = fg
        };
    }

    private static Border Card(string title, UIElement content)
    {
        var card = new Border { Background = BgCard, CornerRadius = new CornerRadius(12), BorderBrush = BorderDim, BorderThickness = new Thickness(1), Padding = new Thickness(20) };
        if (!string.IsNullOrEmpty(title))
        {
            var sp = new StackPanel { Spacing = 16 };
            sp.Children.Add(Tx(title, "BodyStrongTextBlockStyle", FgWhite));
            sp.Children.Add(content);
            card.Child = sp;
        }
        else { card.Child = content; }
        return card;
    }

    private static Grid TwoCol(StackPanel left, StackPanel right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static StackPanel LblIn(string label, out TextBox box, string defaultText)
    {
        box = new TextBox { Text = defaultText };
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(Tx(label, "CaptionTextBlockStyle", FgDim));
        sp.Children.Add(box);
        return sp;
    }

    private static Button Btn(string text, int width, SolidColorBrush bg)
    {
        return new Button { Content = text, Width = width, Height = 36, Background = bg, Foreground = FgWhite };
    }

    private void ResetDefaults()
    {
        _codexHomeBox.Text = SyncEngine.DefaultCodexHome();
        _backupDirBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup");
        _sourceProviderBox.Text = "openai";
        _targetProviderBox.Text = "openai";
    }

    private async System.Threading.Tasks.Task RunSync(bool apply)
    {
        SetBusy(true);
        _outputBox.Text = apply ? "Applying..." : "Previewing...";

        var sb = new StringBuilder();
        try
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                var codexHome = _codexHomeBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(codexHome)) codexHome = SyncEngine.DefaultCodexHome();
                codexHome = Path.GetFullPath(codexHome);
                var backupDir = _backupDirBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(backupDir)) backupDir = null;
                else backupDir = Path.GetFullPath(backupDir);
                if (apply && backupDir != null) Directory.CreateDirectory(backupDir);

                var sourceProvider = _sourceProviderBox.Text.Trim();
                var targetProvider = _targetProviderBox.Text.Trim();
                var config = SyncEngine.InspectConfig(Path.Combine(codexHome, "config.toml"), targetProvider);
                var stateDb = SyncEngine.ResolveStateDb(codexHome, config, null);
                var providers = SyncEngine.ResolveConfiguredProviders(config);

                if (_modeMutual.IsChecked == true)
                {
                    var report = new SyncReport { SourceProvider = "*", TargetProviders = providers };
                    var sessions = SyncEngine.FindMutualSourceSessions(codexHome, providers, report, out var idMap, out var allProviders);
                    report.TargetProviders = allProviders;
                    var plans = SyncEngine.BuildMutualMirrorPlans(sessions, allProviders, idMap);
                    if (apply && backupDir != null && stateDb != null) SyncEngine.BackupSqlite(stateDb, backupDir);
                    SyncEngine.SyncRolloutMirrors(plans, apply, report);
                    SyncEngine.SyncSqliteMirrors(stateDb, plans, apply, report);
                    sb.AppendLine($"Mode: mutual sync / {(apply ? "apply" : "preview")}");
                    sb.AppendLine($"Providers: {string.Join(", ", allProviders)}");
                    sb.AppendLine($"  scanned: {report.FilesScanned}  src: {report.SourceSessionsFound}");
                    sb.AppendLine($"  needed: {report.MirrorFilesNeeded}  created: {report.MirrorFilesCreated}");
                    sb.AppendLine($"  existing: {report.MirrorFilesExisting}  updated: {report.MirrorFilesUpdated}  stale: {report.MirrorFilesStale}");
                    if (!apply) sb.AppendLine("Preview only.");
                }
                else if (_modeOpenAi.IsChecked == true)
                {
                    var targetProviders = providers.Where(p => p != sourceProvider).ToList();
                    var report = new SyncReport { SourceProvider = sourceProvider, TargetProviders = targetProviders };
                    var sessions = new List<SourceSession>();
                    foreach (var path in SyncEngine.IterRolloutFiles(codexHome))
                    {
                        report.FilesScanned++;
                        var (meta, _) = SyncEngine.GetSessionMeta(path);
                        if (meta == null) continue;
                        var pk = meta.Provider ?? "<missing>";
                        report.ProviderCountsBefore[pk] = report.ProviderCountsBefore.GetValueOrDefault(pk) + 1;
                        if (meta.Provider != sourceProvider || string.IsNullOrWhiteSpace(meta.Id)) continue;
                        sessions.Add(new SourceSession(path, meta.Id, sourceProvider));
                    }
                    report.SourceSessionsFound = sessions.Count;
                    var plans = new List<MirrorPlan>();
                    var seen = new HashSet<string>();
                    foreach (var s in sessions)
                        foreach (var p in targetProviders)
                        {
                            var mid = UuidV5.Create(UuidV5.SyncNamespace, $"{s.ThreadId}:{p}").ToString();
                            if (!seen.Add(mid)) continue;
                            plans.Add(new MirrorPlan(s.Path, s.ThreadId, p, mid, SyncEngine.ComputeMirrorPath(s.Path, s.ThreadId, mid, p)));
                        }
                    if (apply && backupDir != null && stateDb != null) SyncEngine.BackupSqlite(stateDb, backupDir);
                    SyncEngine.SyncRolloutMirrors(plans, apply, report);
                    sb.AppendLine($"Mode: openai sync / {(apply ? "apply" : "preview")}");
                    sb.AppendLine($"  src: {report.SourceSessionsFound}  created: {report.MirrorFilesCreated}  existing: {report.MirrorFilesExisting}  updated: {report.MirrorFilesUpdated}");
                    if (!apply) sb.AppendLine("Preview only.");
                }
                else
                {
                    var keep = new HashSet<string> { "openai", targetProvider };
                    int c = 0;
                    foreach (var path in SyncEngine.IterRolloutFiles(codexHome))
                    {
                        var lines = File.ReadAllLines(path);
                        var rw = new List<string>(); bool ch = false;
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) { rw.Add(line); continue; }
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(line);
                                if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "session_meta"
                                    && doc.RootElement.TryGetProperty("payload", out var pl))
                                {
                                    var pr = pl.TryGetProperty("model_provider", out var p) ? p.GetString() : null;
                                    if (!string.IsNullOrWhiteSpace(pr) && !keep.Contains(pr))
                                    {
                                        var nd = System.Text.Json.Nodes.JsonNode.Parse(line);
                                        if (nd is System.Text.Json.Nodes.JsonObject o && o.TryGetPropertyValue("payload", out var pn) && pn is System.Text.Json.Nodes.JsonObject po)
                                        { po["model_provider"] = targetProvider; rw.Add(o.ToJsonString()); ch = true; continue; }
                                    }
                                }
                            }
                            catch { }
                            rw.Add(line);
                        }
                        if (!ch) continue;
                        c++;
                        if (apply) { if (backupDir != null) SyncEngine.BackupFile(path, codexHome, backupDir); File.WriteAllLines(path, rw, new UTF8Encoding(false)); }
                    }
                    sb.AppendLine($"Mode: migrate to {targetProvider}");
                    sb.AppendLine($"  files: {c}");
                    if (!apply) sb.AppendLine("Preview only.");
                }
            });
        }
        catch (Exception ex) { sb.AppendLine($"Error: {ex.Message}"); }
        finally
        {
            this.DispatcherQueue.TryEnqueue(() => { _outputBox.Text = sb.ToString(); SetBusy(false); });
        }
    }

    private void SetBusy(bool busy)
    {
        _previewBtn.IsEnabled = !busy;
        _applyBtn.IsEnabled = !busy;
        _defaultsBtn.IsEnabled = !busy;
    }
}
