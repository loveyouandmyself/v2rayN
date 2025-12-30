using ServiceLib.Events;
using ServiceLib.Services;
using v2rayN.Desktop.Views;

namespace v2rayN.Desktop;

public partial class App : Application
{
    private bool _isSwitchingToLoginWindow;
    private bool _isHandlingShowHide;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!Design.IsDesignMode)
            {
                AppManager.Instance.InitComponents();
                DataContext = StatusBarViewModel.Instance;

                // 先加载保存的授权码
                AuthService.Instance.LoadSavedAuth();

                // 如果有保存的授权码，先显示主窗口（可能在后台验证），否则显示登录窗口
                if (AuthService.Instance.IsLoggedIn)
                {
                    // 先创建主窗口并显示
                    var mainWindow = new MainWindow();
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    // 在后台验证，如果验证失败则切换到登录窗口
                    _ = TryAutoLoginAsync(desktop);
                }
                else
                {
                    // 没有保存的授权码，直接显示登录窗口
                    var loginWindow = new LoginWindow();
                    desktop.MainWindow = loginWindow;
                    loginWindow.Show();
                }
            }
            else
            {
                desktop.MainWindow = new MainWindow();
            }

            desktop.Exit += OnExit;

            // 订阅登出事件，确保无论当前窗口类型都能正确处理
            AppEvents.LogoutRequested
                .AsObservable()
                .Subscribe(_ => 
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleLogoutRequested(desktop));
                });

            // 统一处理托盘“显示/隐藏/切换”请求：未登录操作 LoginWindow，已登录操作 MainWindow
            AppEvents.ShowHideWindowRequested
                .AsObservable()
                .Subscribe(blShow =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleShowHideWindowRequested(desktop, blShow));
                });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void HandleShowHideWindowRequested(IClassicDesktopStyleApplicationLifetime desktop, bool? blShow)
    {
        if (_isHandlingShowHide)
        {
            return;
        }
        _isHandlingShowHide = true;
        try
        {
            var target = EnsureWindowForCurrentAuth(desktop);
            if (target is null)
            {
                return;
            }

            var shouldShow = blShow ?? (!target.IsVisible || target.WindowState == WindowState.Minimized);

            if (shouldShow)
            {
                if (!target.IsVisible)
                {
                    target.Show();
                }
                if (target.WindowState == WindowState.Minimized)
                {
                    target.WindowState = WindowState.Normal;
                }
                target.Activate();
                target.Focus();
            }
            else
            {
                target.Hide();
            }
        }
        finally
        {
            _isHandlingShowHide = false;
        }
    }

    private Window? EnsureWindowForCurrentAuth(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // 目标窗口：未登录 -> LoginWindow；已登录 -> MainWindow
        if (AuthService.Instance.IsLoggedIn)
        {
            if (desktop.MainWindow is MainWindow mw)
            {
                return mw;
            }

            var oldWindow = desktop.MainWindow;
            var newWindow = new MainWindow();
            desktop.MainWindow = newWindow;

            // 关闭旧窗口（先切换 MainWindow，再关闭，避免触发退出）
            if (oldWindow is MainWindow oldMain)
            {
                oldMain.CloseByApp();
            }
            else
            {
                oldWindow?.Close();
            }
            return newWindow;
        }
        else
        {
            if (desktop.MainWindow is LoginWindow lw)
            {
                return lw;
            }

            var oldWindow = desktop.MainWindow;
            var newWindow = new LoginWindow();
            desktop.MainWindow = newWindow;

            if (oldWindow is MainWindow oldMain)
            {
                oldMain.CloseByApp();
            }
            else
            {
                oldWindow?.Close();
            }
            return newWindow;
        }
    }

    private void HandleLogoutRequested(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_isSwitchingToLoginWindow)
        {
            return;
        }
        _isSwitchingToLoginWindow = true;
        // 如果当前窗口不是登录窗口，切换到登录窗口
        // 注意：Avalonia 默认在关闭 MainWindow 时会触发应用退出，因此必须先切换 MainWindow，再关闭旧窗口
        if (desktop.MainWindow is not LoginWindow)
        {
            var oldWindow = desktop.MainWindow;
            var loginWindow = new LoginWindow();
            desktop.MainWindow = loginWindow;
            loginWindow.Show();

            if (oldWindow is MainWindow mainWindow)
            {
                mainWindow.CloseByApp();
            }
            else
            {
                oldWindow?.Close();
            }
        }
        _isSwitchingToLoginWindow = false;
    }

    private async Task TryAutoLoginAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            // 验证授权码是否有效
            var isValid = await AuthService.Instance.ValidateAuthAsync();
            if (!isValid)
            {
                // 验证失败，清除无效的授权码并切换到登录窗口
                AuthService.Instance.ClearAuth();
                
                // 在 UI 线程上切换窗口
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var oldWindow = desktop.MainWindow;
                    var loginWindow = new LoginWindow();
                    desktop.MainWindow = loginWindow;
                    loginWindow.Show();

                    if (oldWindow is MainWindow mainWindow)
                    {
                        mainWindow.CloseByApp();
                    }
                    else
                    {
                        oldWindow?.Close();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("TryAutoLoginAsync", ex);
            // 发生异常时，切换到登录窗口
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var oldWindow = desktop.MainWindow;
                var loginWindow = new LoginWindow();
                desktop.MainWindow = loginWindow;
                loginWindow.Show();

                if (oldWindow is MainWindow mainWindow)
                {
                    mainWindow.CloseByApp();
                }
                else
                {
                    oldWindow?.Close();
                }
            });
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject != null)
        {
            Logging.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logging.SaveLog("TaskScheduler_UnobservedTaskException", e.Exception);
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
    }

    private async void MenuAddServerViaClipboardClick(object? sender, EventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                AppEvents.AddServerViaClipboardRequested.Publish();
                await Task.Delay(1000);
            }
        }
    }

    private async void MenuExit_Click(object? sender, EventArgs e)
    {
        await AppManager.Instance.AppExitAsync(false);
        AppManager.Instance.Shutdown(true);
    }
}
