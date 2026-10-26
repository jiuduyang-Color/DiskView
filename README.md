# DiskView 磁盘空间分析器

一款 Windows 平台的磁盘空间可视化分析工具，界面与操作对标 WizTree：以矩形树图直观展示每个文件夹、每个文件占用了多少空间，并支持复制、剪切、粘贴、删除、重命名等日常文件操作。

提供**中文版**与**英文版**两个独立版本，均为单文件程序，免安装运行。

## 功能特性

- **矩形树图（Treemap）**：按文件/文件夹实际占用面积绘制，一眼看出空间被谁吃掉；颜色区分视频、图片、压缩包、文档等类型。
- **高速扫描**：直接调用 Win32 `FindFirstFileExW`（批量枚举）并配合多线程遍历，扫描数十万文件仅需数秒。
- **目录树 + 文件列表**：左侧目录树逐层下钻，右侧文件列表显示大小、占比、修改时间、类型，可按列排序。
- **文件操作**：右键菜单或快捷键完成 复制 / 剪切 / 粘贴 / 删除（回收站）/ 永久删除 / 重命名 / 新建文件夹 / 全选 / 属性 / 在资源管理器中显示 / 复制路径。
- **系统剪贴板互通**：与资源管理器共用剪贴板，可直接跨程序剪切、粘贴文件。
- **实时搜索**：按文件名即时过滤当前目录。
- **长路径支持**：使用 `\\?\` 前缀，可扫描超长路径文件；无权限目录自动跳过并统计数量。
- **中英双语**：同一套源码，通过编译常量生成两个语言版本。
- **首次启动确认**：首次运行需阅读并手动勾选同意免责声明，未同意则程序直接退出；同意结果按声明版本记录，升级后如有更新会再次提示。

## 下载

前往 [Releases](https://github.com/jiuduyang-Color/DiskView/releases) 下载：

| 版本 | 文件 | 说明 |
| --- | --- | --- |
| 中文 | `DiskView-1.0.0-zh-CN-portable.exe` | 便携版，双击即用 |
| 中文 | `DiskView-1.0.0-zh-CN-setup.exe` | 安装版，安装到当前用户目录，无需管理员权限 |
| English | `DiskView-1.0.0-en-portable.exe` | Portable, run directly |
| English | `DiskView-1.0.0-en-setup.exe` | Installer, per-user install, no admin required |

> 程序未进行代码签名，首次运行时 Windows SmartScreen 可能提示，选择“更多信息 → 仍要运行”即可。

## 运行环境

- Windows 10 / 11，64 位
- 便携版与安装版均为自包含发布，**无需安装 .NET 运行时**

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl` + `C` / `X` / `V` | 复制 / 剪切 / 粘贴 |
| `Delete` | 删除到回收站 |
| `Shift` + `Delete` | 永久删除 |
| `F2` | 重命名 |
| `F5` | 刷新当前目录 |
| `Enter` | 打开 |
| `Ctrl` + `A` | 全选 |
| `Backspace` | 返回上一级 |

## 从源码构建

需要安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

一键构建中英文各两个版本（便携版 + 安装版），产物输出到 `release` 目录：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

也可以单独发布某个语言版本的程序：

```powershell
# 中文版
dotnet publish src\DiskView.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:Lang=zh -o out

# 英文版（Lang=en 会切换到独立的中间目录，避免增量编译缓存串味）
dotnet publish src\DiskView.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:Lang=en -o out-en
```

语言切换原理：`Lang=en` → MSBuild 定义 `APP_LANG_EN` 常量 → `Loc.cs` 选用英文文案表，XAML 中通过 `{app:Tr Key}` 标记扩展取值。

## 项目结构

```
DiskView/
├─ src/
│  ├─ DiskView.Core/     核心层：高速目录扫描、节点模型、Shell 文件操作、本地化文案
│  ├─ DiskView.App/      WPF 主程序：目录树、矩形树图、文件列表、右键菜单
│  └─ DiskView.Setup/    安装程序：解压内嵌程序、创建快捷方式、写入卸载信息
├─ tests/
│  └─ DiskView.SmokeTest/  扫描结果与文件操作的冒烟测试
├─ assets/                应用图标
├─ tools/                 图标生成脚本
├─ build.ps1              一键打包脚本
└─ Directory.Build.props  英文版独立编译目录配置
```

## 技术栈

C# · .NET 8 · WPF · Win32 API（`FindFirstFileExW`、`SHFileOperationW`、`ShellExecuteExW`）

## 免责声明

本软件按“现状”提供，不附带任何明示或暗示的担保。软件具备删除、剪切、移动、重命名等直接修改文件系统的能力，请在使用前自行备份重要数据，并确认操作对象无误；因误操作或程序缺陷导致的数据丢失、系统故障等后果由使用者自行承担。扫描结果受权限、硬链接、压缩、去重等因素影响，仅供容量分析参考，不应作为计费、审计或司法取证的唯一依据。

本项目为独立开源项目，与 WizTree 等商业软件无任何关联。完整条款（含英文版）见 [DISCLAIMER.md](DISCLAIMER.md)。

## 许可证

[MIT License](LICENSE)
