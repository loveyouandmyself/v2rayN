using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ServiceLib.Common;

/// <summary>
/// 机器唯一标识生成工具类（跨平台支持：Windows、Mac、Linux）
/// </summary>
public static class MachineIdUtils
{
    private static readonly string _tag = "MachineIdUtils";
    private static string? _cachedMachineId;

    /// <summary>
    /// 获取机器唯一标识（同步版本，使用后台线程避免死锁）
    /// </summary>
    /// <returns>机器唯一标识字符串</returns>
    public static string GetMachineId()
    {
        // 如果已经生成过，直接返回缓存的
        if (!string.IsNullOrEmpty(_cachedMachineId))
        {
            return _cachedMachineId;
        }

        // 使用 Task.Run 在后台线程执行，避免在 UI 线程上阻塞
        try
        {
            _cachedMachineId = Task.Run(async () => await GetMachineIdAsync()).GetAwaiter().GetResult();
            return _cachedMachineId;
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error generating machine ID", ex);
            // 如果生成失败，使用备用方案（仅使用基本信息，不调用命令行）
            _cachedMachineId = GenerateHash($"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}");
            return _cachedMachineId;
        }
    }

    /// <summary>
    /// 异步获取机器唯一标识
    /// </summary>
    /// <returns>机器唯一标识字符串</returns>
    public static async Task<string> GetMachineIdAsync()
    {
        // 如果已经生成过，直接返回缓存的
        if (!string.IsNullOrEmpty(_cachedMachineId))
        {
            return _cachedMachineId;
        }

        try
        {
            var components = new List<string>();

            // 1. 获取操作系统信息
            components.Add(Environment.OSVersion.Platform.ToString());
            components.Add(Environment.OSVersion.Version.ToString());

            // 2. 获取机器名
            components.Add(Environment.MachineName);

            // 3. 获取用户名（作为辅助标识）
            components.Add(Environment.UserName);

            // 4. 获取MAC地址（跨平台，同步操作，很快）
            var macAddress = GetMacAddress();
            if (!string.IsNullOrEmpty(macAddress))
            {
                components.Add(macAddress);
            }

            // 5. 获取CPU信息（跨平台，异步执行命令）
            var cpuId = await GetCpuIdAsync();
            if (!string.IsNullOrEmpty(cpuId))
            {
                components.Add(cpuId);
            }

            // 6. 获取磁盘序列号（跨平台，异步执行命令）
            var diskId = await GetDiskIdAsync();
            if (!string.IsNullOrEmpty(diskId))
            {
                components.Add(diskId);
            }

            // 7. 对于Windows，尝试获取主板序列号
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var motherboardId = await GetWindowsMotherboardIdAsync();
                if (!string.IsNullOrEmpty(motherboardId))
                {
                    components.Add(motherboardId);
                }
            }
            // 对于Mac，尝试获取硬件UUID
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var hardwareUuid = await GetMacHardwareUuidAsync();
                if (!string.IsNullOrEmpty(hardwareUuid))
                {
                    components.Add(hardwareUuid);
                }
            }
            // 对于Linux，尝试获取机器ID（文件读取，同步但很快）
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var machineId = GetLinuxMachineId();
                if (!string.IsNullOrEmpty(machineId))
                {
                    components.Add(machineId);
                }
            }

            // 将所有组件组合并生成哈希
            var combined = string.Join("|", components);
            _cachedMachineId = GenerateHash(combined);

            Logging.SaveLog($"{_tag}: Machine ID generated successfully");
            return _cachedMachineId;
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error generating machine ID", ex);
            // 如果生成失败，使用备用方案
            _cachedMachineId = GenerateHash($"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}");
            return _cachedMachineId;
        }
    }

    /// <summary>
    /// 获取MAC地址（跨平台）
    /// </summary>
    private static string GetMacAddress()
    {
        try
        {
            var macAddresses = new List<string>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                              && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var nic in interfaces)
            {
                var address = nic.GetPhysicalAddress();
                if (address != null && address.ToString().Length > 0)
                {
                    macAddresses.Add(address.ToString());
                }
            }

            if (macAddresses.Count > 0)
            {
                // 排序以确保一致性，然后取第一个
                macAddresses.Sort();
                return macAddresses[0];
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting MAC address", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// 获取CPU ID（跨平台，异步版本）
    /// </summary>
    private static async Task<string> GetCpuIdAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: 使用WMI获取CPU ID
                return await GetWindowsCpuIdAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Mac: 使用system_profiler获取CPU信息
                return await GetMacCpuIdAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: 从/proc/cpuinfo读取（文件读取，同步但很快）
                return GetLinuxCpuId();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting CPU ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Windows: 获取CPU ID（异步版本）
    /// </summary>
    private static async Task<string> GetWindowsCpuIdAsync()
    {
        try
        {
            var result = await Utils.GetCliWrapOutput("wmic", "cpu get ProcessorId /value").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("ProcessorId=", StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring("ProcessorId=".Length).Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Windows CPU ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Mac: 获取CPU信息（异步版本）
    /// </summary>
    private static async Task<string> GetMacCpuIdAsync()
    {
        try
        {
            var result = await Utils.GetCliWrapOutput("sysctl", "-n machdep.cpu.brand_string").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                return result.Trim();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Mac CPU ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Linux: 获取CPU信息
    /// </summary>
    private static string GetLinuxCpuId()
    {
        try
        {
            if (File.Exists("/proc/cpuinfo"))
            {
                var content = File.ReadAllText("/proc/cpuinfo");
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("Serial", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Linux CPU ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// 获取磁盘ID（跨平台，异步版本）
    /// </summary>
    private static async Task<string> GetDiskIdAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await GetWindowsDiskIdAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await GetMacDiskIdAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await GetLinuxDiskIdAsync();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting disk ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Windows: 获取磁盘序列号（异步版本）
    /// </summary>
    private static async Task<string> GetWindowsDiskIdAsync()
    {
        try
        {
            var result = await Utils.GetCliWrapOutput("wmic", "diskdrive get serialnumber /value").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("SerialNumber=", StringComparison.OrdinalIgnoreCase))
                    {
                        var serial = line.Substring("SerialNumber=".Length).Trim();
                        if (!string.IsNullOrEmpty(serial))
                        {
                            return serial;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Windows disk ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Mac: 获取磁盘UUID（异步版本）
    /// </summary>
    private static async Task<string> GetMacDiskIdAsync()
    {
        try
        {
            // 使用diskutil info获取根卷信息，然后解析UUID
            var result = await Utils.GetCliWrapOutput("diskutil", "info /").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Volume UUID:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length >= 2)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Mac disk ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Linux: 获取磁盘UUID（异步版本）
    /// </summary>
    private static async Task<string> GetLinuxDiskIdAsync()
    {
        try
        {
            // 尝试从 /etc/fstab 或 blkid 获取
            var result = await Utils.GetCliWrapOutput("blkid", "-s UUID -o value /").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                return result.Trim();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Linux disk ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Windows: 获取主板序列号（异步版本）
    /// </summary>
    private static async Task<string> GetWindowsMotherboardIdAsync()
    {
        try
        {
            var result = await Utils.GetCliWrapOutput("wmic", "baseboard get serialnumber /value").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("SerialNumber=", StringComparison.OrdinalIgnoreCase))
                    {
                        var serial = line.Substring("SerialNumber=".Length).Trim();
                        if (!string.IsNullOrEmpty(serial) && serial != "To be filled by O.E.M.")
                        {
                            return serial;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Windows motherboard ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Mac: 获取硬件UUID（异步版本）
    /// </summary>
    private static async Task<string> GetMacHardwareUuidAsync()
    {
        try
        {
            var result = await Utils.GetCliWrapOutput("system_profiler", "SPHardwareDataType").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Hardware UUID:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length >= 2)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Mac hardware UUID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Linux: 获取机器ID
    /// </summary>
    private static string GetLinuxMachineId()
    {
        try
        {
            // /etc/machine-id 是 systemd 系统的标准机器ID文件
            if (File.Exists("/etc/machine-id"))
            {
                var machineId = File.ReadAllText("/etc/machine-id").Trim();
                if (!string.IsNullOrEmpty(machineId))
                {
                    return machineId;
                }
            }
            // 如果没有 systemd，尝试 /var/lib/dbus/machine-id
            else if (File.Exists("/var/lib/dbus/machine-id"))
            {
                var machineId = File.ReadAllText("/var/lib/dbus/machine-id").Trim();
                if (!string.IsNullOrEmpty(machineId))
                {
                    return machineId;
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error getting Linux machine ID", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// 生成哈希值作为最终的唯一标识
    /// </summary>
    private static string GenerateHash(string input)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = SHA256.HashData(bytes);
            // 转换为十六进制字符串，并取前32位作为标识
            var hex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            // 格式化为8-4-4-4-12的格式，类似UUID但更短
            return $"{hex.Substring(0, 8)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20, 12)}";
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: Error generating hash", ex);
            // 备用方案：使用简单的哈希
            return input.GetHashCode().ToString("X8");
        }
    }
}

