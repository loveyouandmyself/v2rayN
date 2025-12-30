using ServiceLib.Common;
using ServiceLib.Helper;
using ServiceLib.Models;

namespace ServiceLib.Services;

public class AuthService
{
    private static readonly Lazy<AuthService> _instance = new(() => new AuthService());
    public static AuthService Instance => _instance.Value;

    private const string LoginUrlTemplate = "https://api-dev.eyesat.cn/dy/platform/manage/system/staff/vpn/{0}";

    private string? _savedKey;
    private string? _userName;

    public string? UserName => _userName;

    public bool IsLoggedIn => _savedKey != null;

    /// <summary>
    /// 从配置文件加载保存的登录态
    /// </summary>
    public void LoadSavedAuth()
    {
        try
        {
            var authFile = Utils.GetConfigPath("auth.json");
            if (File.Exists(authFile))
            {
                var content = File.ReadAllText(authFile);
                var auth = JsonUtils.Deserialize<Dictionary<string, string>>(content);
                if (auth != null && auth.ContainsKey("key"))
                {
                    _savedKey = auth["key"];
                    _userName = auth.ContainsKey("name") ? auth["name"] : null;
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AuthService", ex);
        }
    }

    /// <summary>
    /// 保存登录态到配置文件
    /// </summary>
    private void SaveAuth(string key, string? name)
    {
        try
        {
            _savedKey = key;
            _userName = name;
            var authFile = Utils.GetConfigPath("auth.json");
            var auth = new Dictionary<string, string?>
            {
                { "key", key },
                { "name", name }
            };
            File.WriteAllText(authFile, JsonUtils.Serialize(auth));
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AuthService", ex);
        }
    }

    /// <summary>
    /// 清除登录态
    /// </summary>
    public void ClearAuth()
    {
        try
        {
            _savedKey = null;
            _userName = null;
            var authFile = Utils.GetConfigPath("auth.json");
            if (File.Exists(authFile))
            {
                File.Delete(authFile);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AuthService", ex);
        }
    }

    /// <summary>
    /// 验证当前保存的授权码是否有效（仅验证，不修改登录态）
    /// </summary>
    public async Task<bool> ValidateAuthAsync()
    {
        if (_savedKey == null)
        {
            Logging.SaveLog("ValidateAuthAsync: No saved key");
            return false;
        }

        try
        {
            var url = string.Format(LoginUrlTemplate, Uri.EscapeDataString(_savedKey));
            Logging.SaveLog($"ValidateAuthAsync: Validating auth, url = {url}");
            var result = await HttpClientHelper.Instance.TryGetAsync(url);

            if (result == null)
            {
                // 网络请求失败，不认为是授权码失效，返回 true（可能是网络问题）
                Logging.SaveLog("ValidateAuthAsync: Network request failed, assume valid (network issue)");
                return true;
            }

            var loginResponse = JsonUtils.Deserialize<LoginResponse>(result);
            if (loginResponse == null)
            {
                // 响应格式错误，不认为是授权码失效，返回 true
                Logging.SaveLog("ValidateAuthAsync: Response format error, assume valid");
                return true;
            }

            Logging.SaveLog($"ValidateAuthAsync: Response Success = {loginResponse.Success}, Code = {loginResponse.Code}, Msg = {loginResponse.Msg}");

            // 如果 Code 是认证相关的错误码（如 401, 403, 400），认为是授权码失效
            if (loginResponse.Code == "401" || loginResponse.Code == "403" || loginResponse.Code == "400")
            {
                Logging.SaveLog("ValidateAuthAsync: Auth failed (authentication error code), key is invalid");
                return false;
            }

            // 如果 Success 为 false，检查是否是网络相关错误
            if (!loginResponse.Success)
            {
                // 如果是网络相关错误，不认为是授权码失效
                if (loginResponse.Msg != null &&
                    (loginResponse.Msg.Contains("网络") || loginResponse.Msg.Contains("连接") || loginResponse.Msg.Contains("服务器")))
                {
                    Logging.SaveLog("ValidateAuthAsync: Network related error, assume valid");
                    return true;
                }
                // 其他错误认为是授权码失效
                Logging.SaveLog("ValidateAuthAsync: Auth failed, key is invalid");
                return false;
            }

            // 验证成功，更新用户名（如果返回了新的用户名）
            if (loginResponse.Name != null && loginResponse.Name != _userName)
            {
                _userName = loginResponse.Name;
                var authFile = Utils.GetConfigPath("auth.json");
                if (File.Exists(authFile))
                {
                    var auth = new Dictionary<string, string?>
                    {
                        { "key", _savedKey },
                        { "name", _userName }
                    };
                    File.WriteAllText(authFile, JsonUtils.Serialize(auth));
                }
            }

            Logging.SaveLog("ValidateAuthAsync: Auth validation passed");
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AuthService.ValidateAuthAsync", ex);
            // 异常情况下不认为是授权码失效，可能是网络问题
            Logging.SaveLog("ValidateAuthAsync: Exception occurred, assume valid (network issue)");
            return true;
        }
    }

    /// <summary>
    /// 登录
    /// </summary>
    public async Task<LoginResponse> LoginAsync(string key)
    {
        try
        {
            var url = string.Format(LoginUrlTemplate, Uri.EscapeDataString(key));
            var result = await HttpClientHelper.Instance.TryGetAsync(url);

            if (result == null)
            {
                // 接口访问不通，清除登录信息
                ClearAuth();
                return new LoginResponse
                {
                    Code = "500",
                    Msg = "网络请求失败，请检查网络连接"
                };
            }

            var loginResponse = JsonUtils.Deserialize<LoginResponse>(result);
            if (loginResponse == null)
            {
                // 接口访问不通，清除登录信息
                ClearAuth();
                return new LoginResponse
                {
                    Code = "500",
                    Msg = "服务器响应格式错误"
                };
            }

            // 如果校验失败（code 为 401 或其他错误码），清除登录信息
            if (!loginResponse.Success)
            {
                ClearAuth();
                // 如果错误信息为空或不是网络相关错误，设置为"授权码不正确"
                if (loginResponse.Msg.IsNullOrEmpty() ||
                    (!loginResponse.Msg.Contains("网络") && !loginResponse.Msg.Contains("连接") && !loginResponse.Msg.Contains("服务器")))
                {
                    loginResponse.Msg = "授权码不正确";
                }
            }
            else
            {
                // 登录成功，保存登录信息
                SaveAuth(key, loginResponse.Name);
            }

            return loginResponse;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AuthService", ex);
            // 接口访问不通，清除登录信息
            ClearAuth();
            return new LoginResponse
            {
                Code = "500",
                Msg = $"登录失败: {ex.Message}"
            };
        }
    }

}

