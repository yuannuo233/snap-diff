namespace SnapDiff.Core;

/// <summary>
/// 一次快照的“范围配置”：拍哪些注册表根、排除哪些前缀、拍哪些文件夹、要不要算哈希。
/// 这份配置会写进快照文件的头部（元数据），将来随时能查到两张快照是在什么范围下拍的。
/// </summary>
public sealed class SnapshotOptions
{
    /// <summary>要扫描的注册表根键名，如 "HKEY_CURRENT_USER"（也接受 HKCU 这样的缩写）。</summary>
    public List<string> RegistryRoots { get; set; } = new();

    /// <summary>注册表排除清单：相对根键的路径前缀（大小写不敏感），命中的整个子树都不扫。</summary>
    public List<string> ExcludedRegistryPrefixes { get; set; } = new();

    /// <summary>要扫描的文件夹（绝对路径）。</summary>
    public List<string> FileSystemRoots { get; set; } = new();

    /// <summary>是否计算文件 SHA256（只对小文件算，见 <see cref="MaxHashFileSizeBytes"/>）。</summary>
    public bool ComputeSha256 { get; set; }

    /// <summary>只对小于这个大小（8 MB）的文件计算 SHA256，避免扫描大文件拖慢速度。</summary>
    public const int MaxHashFileSizeBytes = 8 * 1024 * 1024;

    /// <summary>
    /// 默认注册表排除清单（可在界面上编辑）。
    /// 这些键属于系统自己就会频繁变动的“噪音”，拍进来只会淹没真正的变化。
    /// 注意：【刻意不排除】开机自启动键（…\CurrentVersion\Run 等）——
    /// 软件往 Run 里写了什么，恰恰是最重要的线索之一。
    /// </summary>
    public static readonly string[] DefaultExcludedRegistryPrefixes = new[]
    {
        "AppEvents",                                                              // 系统声音方案，自己就会变
        "Software\\Microsoft\\Clipboard",                                         // 剪贴板历史
        "Volatile Environment",                                                   // 每次登录都会重建的临时环境信息
        "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer",                 // 资源管理器状态（最近文件、缩略图缓存等）
        "Software\\Microsoft\\Windows\\CurrentVersion\\CloudStore",               // Windows 云配置推送，几乎每次开机都在变
        "Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo",          // 广告 ID
        "Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell", // Shell 状态（MUICache、文件夹视图缓存）
        "Software\\Microsoft\\Tracing",                                           // 系统跟踪日志（不少软件联网时会被写进来）
    };

    /// <summary>默认监视的文件夹：两个 Program Files + 当前用户的 AppData（Local 与 Roaming）。</summary>
    public static List<string> GetDefaultFileSystemRoots()
    {
        var roots = new List<string>();
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        return roots;
    }

    /// <summary>给界面用的默认配置（只拍 HKCU + 默认文件夹，不开哈希）。</summary>
    public static SnapshotOptions CreateDefault()
    {
        return new SnapshotOptions
        {
            RegistryRoots = { "HKEY_CURRENT_USER" },
            ExcludedRegistryPrefixes = new List<string>(DefaultExcludedRegistryPrefixes),
            FileSystemRoots = GetDefaultFileSystemRoots(),
            ComputeSha256 = false,
        };
    }

    private static void AddIfNotEmpty(List<string> list, string path)
    {
        if (!string.IsNullOrWhiteSpace(path)) list.Add(path);
    }
}
