[简体中文](README.md) | English

![License](https://img.shields.io/badge/license-MIT-green) ![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-10.0-blue)

# SnapDiff

Take one read-only snapshot before you install something and another one after — SnapDiff tells you exactly which files and registry entries the program added, changed, or removed. A modern, open-source successor to Regshot.

## Features

- 100% read-only scanning: registry (HKCU / HKLM, noisy keys excluded) plus folders (Program Files, AppData, …); inaccessible paths are skipped and counted
- Precise diff: added / removed / modified, with optional SHA256 fingerprints for files
- Searchable result tree, one-click export to Markdown report or CSV
- Open snapshot format: a single GZip-compressed JSON Lines file (`.snapdiff`), easy to script against
- English & Chinese UI (switchable at runtime), zero third-party dependencies in the core library

## Download & Usage (three steps)

Download the latest `SnapDiff-v0.1.0-win-x64.zip` from [Releases](../../releases) and extract it (single file, no installer), then:

1. Take snapshot A → 2. Install / run the program in question → 3. Take snapshot B, review the diff, export a report.

## Build

```bat
dotnet build src\SnapDiff.App -c Release
```

Binary: `src\SnapDiff.App\bin\Release\net10.0-windows\SnapDiff.exe` (requires the .NET 10 SDK); tests: `dotnet test tests\SnapDiff.Tests`.

## Privacy

Read-only and fully offline: never writes the registry, never touches your files, never uploads anything.

## Feedback / Get help

Found a bug or have a suggestion? Please open an **[Issue](../../issues)** on this repository — I'll reply there. Including your Windows version, reproduction steps, and a screenshot helps me pinpoint it faster.

> 💡 Why Issues? Questions and answers stay public — the next person with the same problem can find the answer by searching, so we build a shared manual together.
