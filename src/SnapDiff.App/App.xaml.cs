using System.Windows;

namespace SnapDiff.App;

/// <summary>程序入口：初始化语言，并兜住未处理异常（弹窗提示而不是闪退）。</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Loc.Initialize(); // 在主窗口创建之前把语言准备好

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                Loc.T("Msg.Unhandled") + "\n\n" + args.Exception.Message,
                "SnapDiff",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };
    }
}
