using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using CodexSessionSync.Core;

namespace CodexSessionSync.MewUI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.DispatcherUnhandledException += e =>
        {
            try
            {
                NativeMessageBox.Show(e.Exception.ToString(), "Codex Session Sync");
            }
            catch
            {
                // Last-chance UI error handling should never crash the dispatcher.
            }

            e.Handled = true;
        };

        var ui = new MainUi();
        Application
            .Create()
            .UseWin32()
            .UseDirect2D()
            .UseAccent(Accent.Green)
            .UseTheme(ThemeVariant.Dark)
            .Run(ui.BuildWindow());
    }
}

internal sealed class MainUi
{
    private readonly ObservableValue<string> _codexHome = new(SyncEngine.DefaultCodexHome());
    private readonly ObservableValue<string> _backupDir = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup"));
    private readonly ObservableValue<string> _sourceProvider = new("openai");
    private readonly ObservableValue<string> _targetProvider = new("openai");
    private readonly ObservableValue<string> _output = new("等待运行。");
    private readonly ObservableValue<string> _status = new("准备就绪");
    private readonly ObservableValue<bool> _confirmed = new(false);

    private SyncMode _mode = SyncMode.Mutual;
    private Window _window = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _defaultsButton = null!;

    public Window BuildWindow()
    {
        return new Window()
            .Ref(out _window)
            .Title("Codex 会话同步工具 - MewUI AOT")
            .Resizable(1080, 760, minWidth: 900, minHeight: 620)
            .StartCenterScreen()
            .Content(
                new DockPanel()
                    .LastChildFill()
                    .Margin(16)
                    .Spacing(12)
                    .Children(
                        Header().DockTop(),
                        Actions().DockBottom(),
                        new Grid()
                            .Columns("300,*")
                            .Spacing(12)
                            .Children(
                                Sidebar().Column(0),
                                MainPanel().Column(1)
                            )
                    )
            );
    }

    private Element Header()
    {
        return new Border()
            .Padding(14, 12)
            .CornerRadius(6)
            .BorderThickness(1)
            .WithTheme((t, c) =>
            {
                c.Background(t.Palette.ContainerBackground);
                c.BorderBrush(t.Palette.ControlBorder);
            })
            .Child(
                new DockPanel()
                    .LastChildFill()
                    .Spacing(12)
                    .Children(
                        new Border()
                            .DockRight()
                            .Padding(12, 8)
                            .CornerRadius(6)
                            .BorderThickness(1)
                            .WithTheme((t, c) =>
                            {
                                c.Background(t.Palette.ControlBackground);
                                c.BorderBrush(t.Palette.ControlBorder);
                            })
                            .Child(
                                new TextBlock()
                                    .BindText(_status)
                                    .SemiBold()
                                    .Center()
                            ),

                        new StackPanel()
                            .DockRight()
                            .Horizontal()
                            .Spacing(8)
                            .CenterVertical()
                            .Children(
                                new RadioButton()
                                    .Content("系统")
                                    .GroupName("ThemeMode")
                                    .OnChecked(() => Application.Current.SetTheme(ThemeVariant.System)),
                                new RadioButton()
                                    .Content("浅色")
                                    .GroupName("ThemeMode")
                                    .OnChecked(() => Application.Current.SetTheme(ThemeVariant.Light)),
                                new RadioButton()
                                    .Content("深色")
                                    .GroupName("ThemeMode")
                                    .IsChecked()
                                    .OnChecked(() => Application.Current.SetTheme(ThemeVariant.Dark))
                            ),

                        new StackPanel()
                            .Vertical()
                            .Spacing(3)
                            .Children(
                                new TextBlock()
                                    .Text("Codex 会话同步工具")
                                    .FontSize(22)
                                    .SemiBold(),
                                new TextBlock()
                                    .Text("MewUI / NativeAOT / full trim")
                                    .FontSize(12)
                                    .WithTheme((t, c) => c.Foreground(t.Palette.Accent))
                            )
                    )
            );
    }

    private Element Sidebar()
    {
        return new GroupBox()
            .Header("同步模式")
            .Content(
                new StackPanel()
                    .Vertical()
                    .Spacing(10)
                    .Children(
                        ModeButton("全供应商互同步", SyncMode.Mutual, true),
                        ModeButton("OpenAI 同步到全部", SyncMode.OpenAi, false),
                        ModeButton("单目标迁移", SyncMode.Migrate, false),
                        new Border()
                            .Margin(0, 6, 0, 0)
                            .Padding(10)
                            .CornerRadius(6)
                            .BorderThickness(1)
                            .WithTheme((t, c) =>
                            {
                                c.Background(t.Palette.ControlBackground);
                                c.BorderBrush(t.Palette.ControlBorder);
                            })
                            .Child(
                                new StackPanel()
                                    .Vertical()
                                    .Spacing(8)
                                    .Children(
                                        new TextBlock().Text("状态").SemiBold(),
                                        new TextBlock()
                                            .BindText(_status)
                                            .TextWrapping(TextWrapping.Wrap)
                                    )
                            )
                    )
            );
    }

    private RadioButton ModeButton(string text, SyncMode mode, bool selected)
    {
        return new RadioButton()
            .Content(text)
            .GroupName("SyncMode")
            .IsChecked(selected)
            .OnChecked(() => _mode = mode);
    }

    private Element MainPanel()
    {
        return new DockPanel()
            .LastChildFill()
            .Spacing(12)
            .Children(
                Settings().DockTop(),
                OutputPanel()
            );
    }

    private Element Settings()
    {
        return new GroupBox()
            .Header("参数")
            .Content(
                new Grid()
                    .Rows("Auto,Auto")
                    .Columns("*,*")
                    .Spacing(12)
                    .Children(
                        Field("Codex Home", _codexHome, browse: true).Column(0).Row(0),
                        Field("备份目录", _backupDir, browse: true).Column(1).Row(0),
                        Field("源 provider", _sourceProvider, browse: false).Column(0).Row(1),
                        Field("目标 provider", _targetProvider, browse: false).Column(1).Row(1)
                    )
            );
    }

    private Element Field(string label, ObservableValue<string> value, bool browse)
    {
        var input = new TextBox()
            .BindText(value)
            .MinHeight(34);

        Element editor = browse
            ? new Grid()
                .Columns("*,Auto")
                .Spacing(6)
                .Children(
                    input.Column(0),
                    new Button()
                        .Column(1)
                        .Content("...")
                        .Width(42)
                        .OnClick(() => BrowseFolder(value))
                )
            : input;

        return new StackPanel()
            .Vertical()
            .Spacing(6)
            .Children(
                new TextBlock().Text(label).SemiBold(),
                editor
            );
    }

    private Element OutputPanel()
    {
        return new GroupBox()
            .Header("运行输出")
            .Content(
                new MultiLineTextBox()
                    .BindText(_output)
                    .IsReadOnly()
                    .Wrap(false)
                    .FontFamily("Consolas")
                    .FontSize(13)
                    .Padding(10)
            );
    }

    private Element Actions()
    {
        return new Border()
            .Padding(12)
            .CornerRadius(6)
            .BorderThickness(1)
            .WithTheme((t, c) =>
            {
                c.Background(t.Palette.ContainerBackground);
                c.BorderBrush(t.Palette.ControlBorder);
            })
            .Child(
                new DockPanel()
                    .LastChildFill()
                    .Children(
                        new CheckBox()
                            .DockRight()
                            .Content("我已备份或确认可以写入")
                            .BindIsChecked(_confirmed)
                            .CenterVertical(),
                        new StackPanel()
                            .Horizontal()
                            .Spacing(8)
                            .Children(
                                new Button()
                                    .Ref(out _previewButton)
                                    .Content("预览")
                                    .Width(92)
                                    .OnClick(async () => await RunAsync(apply: false)),
                                new Button()
                                    .Ref(out _applyButton)
                                    .Content("执行写入")
                                    .Width(104)
                                    .Background(Color.FromRgb(196, 43, 28))
                                    .Foreground(Color.White)
                                    .OnClick(async () => await RunAsync(apply: true)),
                                new Button()
                                    .Ref(out _defaultsButton)
                                    .Content("恢复默认")
                                    .Width(104)
                                    .OnClick(ResetDefaults)
                            )
                    )
            );
    }

    private void BrowseFolder(ObservableValue<string> target)
    {
        var selected = FileDialog.SelectFolder(new FolderDialogOptions
        {
            Owner = _window.Handle,
            InitialDirectory = Directory.Exists(target.Value) ? target.Value : null
        });

        if (!string.IsNullOrWhiteSpace(selected))
        {
            target.Value = selected;
        }
    }

    private void ResetDefaults()
    {
        _codexHome.Value = SyncEngine.DefaultCodexHome();
        _backupDir.Value = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup");
        _sourceProvider.Value = "openai";
        _targetProvider.Value = "openai";
        _status.Value = "准备就绪";
    }

    private async Task RunAsync(bool apply)
    {
        if (apply && _confirmed.Value != true)
        {
            _status.Value = "等待确认";
            _output.Value = "执行写入前，请先勾选确认。";
            return;
        }

        var options = BuildOptions(apply);
        SetBusy(true);
        _status.Value = apply ? "写入中" : "预览中";
        _output.Value = apply ? "正在执行写入..." : "正在预览...";

        try
        {
            var result = await Task.Run(() => SyncRunner.Run(options));
            RunOnUi(() =>
            {
                _output.Value = result;
                _status.Value = "完成";
            });
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                _output.Value = $"Error: {ex.Message}{Environment.NewLine}{ex}";
                _status.Value = "失败";
            });
        }
        finally
        {
            RunOnUi(() => SetBusy(false));
        }
    }

    private SyncOptions BuildOptions(bool apply)
    {
        var codexHome = _codexHome.Value.Trim();
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = SyncEngine.DefaultCodexHome();
        }

        var backupDir = _backupDir.Value.Trim();
        var sourceProvider = _sourceProvider.Value.Trim();
        var targetProvider = _targetProvider.Value.Trim();

        return new SyncOptions(
            _mode,
            Path.GetFullPath(codexHome),
            string.IsNullOrWhiteSpace(backupDir) ? null : Path.GetFullPath(backupDir),
            string.IsNullOrWhiteSpace(sourceProvider) ? "openai" : sourceProvider,
            string.IsNullOrWhiteSpace(targetProvider) ? "openai" : targetProvider,
            apply);
    }

    private void SetBusy(bool busy)
    {
        _previewButton.IsEnabled = !busy;
        _applyButton.IsEnabled = !busy;
        _defaultsButton.IsEnabled = !busy;
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher is { IsOnUIThread: false })
        {
            dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
