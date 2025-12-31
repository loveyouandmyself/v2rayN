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
    public string MachineId { get; set; } = string.Empty;

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

    public ReactiveCommand<Unit, LoginResponse?> ActivateCmd { get; }

    public LoginViewModel()
    {
        // 初始化时生成机器唯一标识
        MachineId = MachineIdUtils.GetMachineId();

        ActivateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (MachineId.IsNullOrEmpty())
            {
                ErrorMessage = "无法生成机器标识";
                return null;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var response = await AuthService.Instance.LoginAsync(MachineId);
                if (!response.Success)
                {
                    ErrorMessage = response.Msg ?? "激活失败";
                    return null;
                }

                return response;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"激活失败: {ex.Message}";
                return null;
            }
            finally
            {
                IsLoading = false;
            }
        });
    }
}

