using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodexSessionSync.Desktop.Shared;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CodexSessionSync.WpfUi;

public partial class MainWindow : FluentWindow
{
    private SyncMode _mode = SyncMode.Mutual;
    private bool _isLightTheme;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ResetDefaults();
            ApplyTheme(isLightTheme: false);
            UpdateModeVisuals();
            UpdateModeDescription();
        };
    }

    private void ResetDefaults()
    {
        CodexHomeBox.Text = SyncEngineDefaults.DefaultCodexHome();
        BackupDirBox.Text = SyncEngineDefaults.DefaultBackupDir();
        SourceProviderBox.Text = "openai";
        TargetProviderBox.Text = "openai";
    }

    private void OnResetDefaults(object sender, RoutedEventArgs e) => ResetDefaults();

    private void OnThemeToggleClick(object sender, RoutedEventArgs e) => ApplyTheme(!_isLightTheme);

    private void OnModeMutualClick(object sender, RoutedEventArgs e) => SetMode(SyncMode.Mutual);

    private void OnModeOpenAiClick(object sender, RoutedEventArgs e) => SetMode(SyncMode.OpenAiToAll);

    private void OnModeMigrateClick(object sender, RoutedEventArgs e) => SetMode(SyncMode.MigrateToTarget);

    private void OnPreviewClick(object sender, RoutedEventArgs e) => RunSync(false);

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (ConfirmSwitch.IsChecked != true)
        {
            AppendOutput("执行写入前，请先打开写入确认。\n");
            StatusInfo.Severity = InfoBarSeverity.Warning;
            StatusInfo.Message = "需要确认";
            return;
        }

        RunSync(true);
    }

    private async void RunSync(bool apply)
    {
        SetBusy(true);
        OutputBox.Text = apply ? "正在执行写入..." : "正在预览...";
        StatusInfo.Severity = InfoBarSeverity.Informational;
        StatusInfo.Message = "运行中";

        try
        {
            var options = SyncRunOptions.FromUi(
                CodexHomeBox.Text,
                BackupDirBox.Text,
                SourceProviderBox.Text,
                TargetProviderBox.Text,
                CurrentMode(),
                apply);

            OutputBox.Text = await SyncUiRunner.RunAsync(options);
            StatusInfo.Severity = InfoBarSeverity.Success;
            StatusInfo.Message = "完成";
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"Error: {ex.Message}\n{ex.StackTrace}";
            StatusInfo.Severity = InfoBarSeverity.Error;
            StatusInfo.Message = "失败";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private SyncMode CurrentMode() => _mode;

    private void SetBusy(bool busy)
    {
        PreviewBtn.IsEnabled = !busy;
        ApplyBtn.IsEnabled = !busy;
        DefaultsBtn.IsEnabled = !busy;
        ThemeToggleBtn.IsEnabled = !busy;
        ConfirmSwitch.IsEnabled = !busy;
        ModeMutualBtn.IsEnabled = !busy;
        ModeOpenAiBtn.IsEnabled = !busy;
        ModeMigrateBtn.IsEnabled = !busy;
    }

    private void AppendOutput(string text)
    {
        OutputBox.Text += text;
        OutputBox.ScrollToEnd();
    }

    private void SetMode(SyncMode mode)
    {
        _mode = mode;
        UpdateModeVisuals();
        UpdateModeDescription();
    }

    private void UpdateModeVisuals()
    {
        if (ModeMutualBtn == null || ModeOpenAiBtn == null || ModeMigrateBtn == null)
            return;

        ModeMutualBtn.Appearance = _mode == SyncMode.Mutual
            ? ControlAppearance.Primary
            : ControlAppearance.Secondary;
        ModeOpenAiBtn.Appearance = _mode == SyncMode.OpenAiToAll
            ? ControlAppearance.Primary
            : ControlAppearance.Secondary;
        ModeMigrateBtn.Appearance = _mode == SyncMode.MigrateToTarget
            ? ControlAppearance.Primary
            : ControlAppearance.Secondary;
    }

    private void UpdateModeDescription()
    {
        if (ModeHelpText == null)
            return;

        ModeHelpText.Text = _mode switch
        {
            SyncMode.OpenAiToAll => "以源 provider 为基准，为配置中的其它 provider 创建镜像会话。",
            SyncMode.MigrateToTarget => "保留 openai 和目标 provider，把其它 provider 的历史会话迁移到目标 provider。",
            _ => "扫描所有配置中的 provider，让每个 provider 最终看到同一批会话。"
        };
    }

    private void ApplyTheme(bool isLightTheme)
    {
        _isLightTheme = isLightTheme;
        var theme = isLightTheme ? ApplicationTheme.Light : ApplicationTheme.Dark;

        ApplicationThemeManager.Apply(theme, WindowBackdropType.Acrylic, true);
        WindowBackdropType = WindowBackdropType.Acrylic;
        AppTitleBar.ApplicationTheme = theme;

        ThemeToggleIcon.Symbol = isLightTheme ? SymbolRegular.WeatherMoon24 : SymbolRegular.WeatherSunny24;
        ToolTipService.SetToolTip(ThemeToggleBtn, isLightTheme ? "切换到深色模式" : "切换到浅色模式");

        if (isLightTheme)
        {
            SetBrush("AppBackgroundBrush", "#72F3F6FA");
            SetBrush("PanelBackgroundBrush", "#B8FFFFFF");
            SetBrush("PanelBorderBrush", "#55AEB8C4");
            SetBrush("PrimaryTextBrush", "#101828");
            SetBrush("SecondaryTextBrush", "#5F6B7A");
            SetBrush("OutputBackgroundBrush", "#AAFFFFFF");
            SetBrush("OutputForegroundBrush", "#101828");
        }
        else
        {
            SetBrush("AppBackgroundBrush", "#66101117");
            SetBrush("PanelBackgroundBrush", "#801B1F2A");
            SetBrush("PanelBorderBrush", "#2FFFFFFF");
            SetBrush("PrimaryTextBrush", "#F8FAFC");
            SetBrush("SecondaryTextBrush", "#B9C0CC");
            SetBrush("OutputBackgroundBrush", "#70111520");
            SetBrush("OutputForegroundBrush", "#F8FAFC");
        }

        Background = Brushes.Transparent;
    }

    private SolidColorBrush BrushResource(string key) => (SolidColorBrush)FindResource(key);

    private void SetBrush(string key, string color)
    {
        Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
