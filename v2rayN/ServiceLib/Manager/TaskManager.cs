using ServiceLib.Events;
using ServiceLib.Handler.SysProxy;
using ServiceLib.Services;

namespace ServiceLib.Manager;

public class TaskManager
{
    private static readonly Lazy<TaskManager> _instance = new(() => new());
    public static TaskManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;

    public void RegUpdateTask(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        Task.Run(ScheduledTasks);
    }

    private async Task ScheduledTasks()
    {
        Logging.SaveLog("Setup Scheduled Tasks");

        var numOfExecuted = 1;
        while (true)
        {
            //1 minute
            await Task.Delay(1000 * 60);

            //Execute once 1 minute
            try
            {
                await UpdateTaskRunSubscription();
            }
            catch (Exception ex)
            {
                Logging.SaveLog("ScheduledTasks - UpdateTaskRunSubscription", ex);
            }

            //Execute once 5 minute - 验证授权码
            if (numOfExecuted % 5 == 0)
            {
                try
                {
                    await UpdateTaskValidateAuth();
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - UpdateTaskValidateAuth", ex);
                }
            }

            //Execute once 20 minute
            if (numOfExecuted % 20 == 0)
            {
                //Logging.SaveLog("Execute save config");

                try
                {
                    await ConfigHandler.SaveConfig(_config);
                    await ProfileExManager.Instance.SaveTo();
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - SaveConfig", ex);
                }
            }

            //Execute once 1 hour
            if (numOfExecuted % 60 == 0)
            {
                //Logging.SaveLog("Execute delete expired files");

                FileUtils.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1));
                FileUtils.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddMonths(-1));
                FileUtils.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddMonths(-1));

                try
                {
                    await UpdateTaskRunGeo(numOfExecuted / 60);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - UpdateTaskRunGeo", ex);
                }
            }

            numOfExecuted++;
        }
    }

    private async Task UpdateTaskRunSubscription()
    {
        var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        var lstSubs = (await AppManager.Instance.SubItems())?
            .Where(t => t.AutoUpdateInterval > 0)
            .Where(t => updateTime - t.UpdateTime >= t.AutoUpdateInterval * 60)
            .ToList();

        if (lstSubs is not { Count: > 0 })
        {
            return;
        }

        Logging.SaveLog("Execute update subscription");

        foreach (var item in lstSubs)
        {
            await SubscriptionHandler.UpdateProcess(_config, item.Id, true, async (success, msg) =>
            {
                await _updateFunc?.Invoke(success, msg);
                if (success)
                {
                    Logging.SaveLog($"Update subscription end. {msg}");
                }
            });
            item.UpdateTime = updateTime;
            await ConfigHandler.AddSubItem(_config, item);
            await Task.Delay(1000);
        }
    }

    private async Task UpdateTaskRunGeo(int hours)
    {
        if (_config.GuiItem.AutoUpdateInterval > 0 && hours > 0 && hours % _config.GuiItem.AutoUpdateInterval == 0)
        {
            Logging.SaveLog("Execute update geo files");

            await new UpdateService(_config, async (success, msg) =>
            {
                await _updateFunc?.Invoke(false, msg);
            }).UpdateGeoFileAll();
        }
    }

    /// <summary>
    /// 验证授权码是否有效
    /// </summary>
    private async Task UpdateTaskValidateAuth()
    {
        if (!AuthService.Instance.IsLoggedIn)
        {
            Logging.SaveLog("UpdateTaskValidateAuth: User not logged in, skip validation");
            return;
        }

        Logging.SaveLog("UpdateTaskValidateAuth: Start validating auth");

        var isValid = await AuthService.Instance.ValidateAuthAsync();
        Logging.SaveLog($"UpdateTaskValidateAuth: Validation result = {isValid}");
        
        if (!isValid)
        {
            Logging.SaveLog("UpdateTaskValidateAuth: Auth validation failed, stopping VPN and clearing auth");
            
            // 停止VPN
            await CoreManager.Instance.CoreStop();
            Logging.SaveLog("UpdateTaskValidateAuth: VPN stopped");
            
            // 清除系统代理
            await SysProxyHandler.UpdateSysProxy(_config, true);
            Logging.SaveLog("UpdateTaskValidateAuth: System proxy cleared");
            
            // 清除登录信息
            AuthService.Instance.ClearAuth();
            Logging.SaveLog("UpdateTaskValidateAuth: Auth cleared");
            
            // 触发登出事件，切换到登录窗口
            AppEvents.LogoutRequested.Publish();
            AppEvents.SendSnackMsgRequested.Publish("授权码已失效，请重新登录");
        }
        else
        {
            Logging.SaveLog("UpdateTaskValidateAuth: Auth validation passed");
        }
    }
}
