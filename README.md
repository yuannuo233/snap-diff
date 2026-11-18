[English](README.en.md) | 简体中文

![License](https://img.shields.io/badge/license-MIT-green) ![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-10.0-blue)

# 快照侦探 SnapDiff

装软件前后各拍一张只读"系统快照"，对比出这个软件到底新增、修改、删除了哪些文件和注册表项——Regshot 的现代开源替代品。

## 功能

- 纯只读扫描：注册表（HKCU / HKLM，噪音键可排除）+ 文件夹（Program Files、AppData 等），无权限自动跳过
- 精确对比：新增 / 删除 / 修改三类变化，文件可选 SHA256 指纹
- 结果树 + 搜索框，一键导出 Markdown 报告 / CSV 表格
- 快照是开放格式：GZip 压缩的 JSON Lines 单文件（`.snapdiff`），可脚本化处理
- 中英双语界面（运行时切换），核心库零第三方依赖

## 使用（三步）

1. 拍快照 A → 2. 去安装 / 运行要检查的软件 → 3. 拍快照 B，看差异、导报告。

## 构建

```bat
dotnet build src\SnapDiff.App -c Release
```

产物：`src\SnapDiff.App\bin\Release\net10.0-windows\SnapDiff.exe`（需 .NET 10 SDK）；测试：`dotnet test tests\SnapDiff.Tests`。

## 隐私

全程只读、完全离线：不写注册表、不改你的文件、不上传任何数据。
