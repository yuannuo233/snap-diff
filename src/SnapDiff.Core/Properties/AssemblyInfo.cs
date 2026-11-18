using System.Runtime.Versioning;

// SnapDiff 是 Windows 专用工具（注册表 API 只在 Windows 上存在）。
// 在程序集级别声明“仅支持 Windows”，注册表 API 的平台分析器警告（CA1416）就不会再刷屏。
[assembly: SupportedOSPlatform("windows")]
