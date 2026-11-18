using System.IO;
using System.Text.Json;

namespace SnapDiff.App;

/// <summary>
/// 程序设置（目前只有界面语言）。
/// 保存在 %APPDATA%\SnapDiff\settings.json，下次启动还记得上次选的语言。
/// 这是程序自己的一个小设置文件——除此之外程序对系统完全只读。
/// </summary>
public static class AppSettings
{
    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SnapDiff",
            "settings.json");

    /// <summary>界面语言（"zh-CN" 或 "en-US"）。</summary>
    public static string Language { get; set; } = Loc.ZhCn;

    /// <summary>读取设置；文件不存在或损坏时安静地用默认值。</summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (!string.IsNullOrWhiteSpace(data?.Language)) Language = data.Language;
        }
        catch
        {
            // 设置文件坏了就忽略，绝不能因为设置文件影响主功能
        }
    }

    /// <summary>保存设置；失败也不影响使用（比如磁盘只读），下次启动回到默认。</summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new SettingsData { Language = Language }));
        }
        catch
        {
            // 同上：安静失败
        }
    }

    private sealed class SettingsData
    {
        public string Language { get; set; } = Loc.ZhCn;
    }
}
