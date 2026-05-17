using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodexSessionSync.Desktop.Shared;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;

namespace CodexSessionSync.Wpf.Inkore;

public partial class MainWindow : Window
{
    private const string SwitchToLightGlyph = "\uE706";
    private const string SwitchToDarkGlyph = "\uE708";
    private bool _isLightTheme;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ModeSelector.ItemsSource = new[] { "全供应商互同步", "OpenAI 同步到全部", "单目标迁移" };
            ModeSelector.SelectedIndex = 0;
            ResetDefaults();
            ApplyTheme(isLightTheme: false);
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

    private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateModeDescription();

    private void OnPreviewClick(object sender, RoutedEventArgs e) => RunSync(false);

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmSwitch.IsOn)
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

    private SyncMode CurrentMode()
    {
        return ModeSelector.SelectedIndex switch
        {
            1 => SyncMode.OpenAiToAll,
            2 => SyncMode.MigrateToTarget,
            _ => SyncMode.Mutual
        };
    }

    private void SetBusy(bool busy)
    {
        PreviewBtn.IsEnabled = !busy;
        ApplyBtn.IsEnabled = !busy;
        DefaultsBtn.IsEnabled = !busy;
        ThemeToggleBtn.IsEnabled = !busy;
        ConfirmSwitch.IsEnabled = !busy;
        ModeSelector.IsEnabled = !busy;
    }

    private void AppendOutput(string text)
    {
        OutputBox.Text += text;
        OutputBox.ScrollToEnd();
    }

    private void UpdateModeDescription()
    {
        if (ModeHelpText == null)
            return;

        ModeHelpText.Text = CurrentMode() switch
        {
            SyncMode.OpenAiToAll => "以源 provider 为基准，为配置中的其它 provider 创建镜像会话。",
            SyncMode.MigrateToTarget => "保留 openai 和目标 provider，把其它 provider 的历史会话迁移到目标 provider。",
            _ => "扫描所有配置中的 provider，让每个 provider 最终看到同一批会话。"
        };
    }

    private void ApplyTheme(bool isLightTheme)
    {
        _isLightTheme = isLightTheme;
        var appTheme = isLightTheme ? ApplicationTheme.Light : ApplicationTheme.Dark;
        var elementTheme = isLightTheme ? ElementTheme.Light : ElementTheme.Dark;

        ThemeResources.Current.RequestedTheme = appTheme;
        ThemeManager.Current.ApplicationTheme = appTheme;
        ThemeManager.SetRequestedTheme(RootGrid, elementTheme);

        ThemeToggleIcon.Glyph = isLightTheme ? SwitchToDarkGlyph : SwitchToLightGlyph;
        ToolTipService.SetToolTip(ThemeToggleBtn, isLightTheme ? "切换到深色模式" : "切换到浅色模式");

        if (isLightTheme)
        {
            SetBrush("AppBackgroundBrush", "#F6F3F6FA");
            SetBrush("PrimaryTextBrush", "#101828");
            SetBrush("SecondaryTextBrush", "#5F6B7A");
            SetBrush("OutputBackgroundBrush", "#FFFFFFFF");
            SetBrush("OutputForegroundBrush", "#101828");
        }
        else
        {
            SetBrush("AppBackgroundBrush", "#E6101117");
            SetBrush("PrimaryTextBrush", "#F8FAFC");
            SetBrush("SecondaryTextBrush", "#B9C0CC");
            SetBrush("OutputBackgroundBrush", "#D9111520");
            SetBrush("OutputForegroundBrush", "#F8FAFC");
        }

        Background = BrushResource("AppBackgroundBrush");
        RootGrid.Background = BrushResource("AppBackgroundBrush");
    }

    private SolidColorBrush BrushResource(string key) => (SolidColorBrush)FindResource(key);

    private void SetBrush(string key, string color)
    {
        Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
