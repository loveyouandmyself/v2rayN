using System;
using System.Reactive;
using ReactiveUI;
using ServiceLib.Base;
using ServiceLib.Common;
using ServiceLib.Models;
using ServiceLib.Services;

namespace v2rayN.Desktop.ViewModels;

public class LoginViewModel : MyReactiveObject
{
    [Reactive]
    public string Key { get; set; } = string.Empty;

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(HasErrorMessage));
        }
    }

    [Reactive]
    public bool IsLoading { get; set; }

    public bool IsNotLoading => !IsLoading;

    public bool HasErrorMessage => !ErrorMessage.IsNullOrEmpty();

    public ReactiveCommand<Unit, LoginResponse?> LoginCmd { get; }

    public LoginViewModel()
    {
        LoginCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (Key.IsNullOrEmpty())
            {
                ErrorMessage = "请输入授权码";
                return null;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var response = await AuthService.Instance.LoginAsync(Key);
                if (!response.Success)
                {
                    ErrorMessage = response.Msg ?? "登录失败";
                    return null;
                }

                return response;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"登录失败: {ex.Message}";
                return null;
            }
            finally
            {
                IsLoading = false;
            }
        });
    }
}

