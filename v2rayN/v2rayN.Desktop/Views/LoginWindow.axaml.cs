using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using v2rayN.Desktop.Base;
using v2rayN.Desktop.ViewModels;

namespace v2rayN.Desktop.Views;

public partial class LoginWindow : WindowBase<LoginViewModel>
{
    public LoginWindow()
    {
        InitializeComponent();
        ViewModel = new LoginViewModel();

        ViewModel.LoginCmd
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(response =>
        {
            if (response != null && response.Success)
            {
                // 登录成功：切换到主窗口
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var main = new MainWindow();
                    desktop.MainWindow = main;
                    main.Show();
                }
                Close();
            }
        });

        // 用户直接关闭登录窗则退出程序
        Closing += (_, __) =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow == this)
            {
                desktop.Shutdown();
            }
        };
    }

    private void KeyDown_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel != null && !ViewModel.IsLoading)
        {
            ViewModel.LoginCmd.Execute().Subscribe(_ => { }, _ => { });
        }
    }
}

