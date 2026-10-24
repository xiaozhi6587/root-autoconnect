# 开发与验证

## 环境

- Windows 和 .NET 10 SDK（用于新版 C# 编译器）。
- .NET Framework 4.7.1 Targeting Pack。
- 软件目标为 .NET Framework 4.7.1；当前实机验证环境为 Windows 11 x64、.NET Framework 4.8.1。

主项目保留 net471 与 net10.0-windows 两个编译目标，公开下载包仅分发 net471。没有在只有 4.7.1 的独立实体机器上测试。

## 构建发布包

```powershell
.\tools\Publish.ps1
```

生成 EXE、同名运行配置、使用说明及带 SHA-256 校验的 ZIP。程序依赖系统提供的框架程序集，不附带第三方 DLL。`发布/` 为本地运行产物，不提交到 Git；`downloads/` 保留当前版本的下载包。

## 功能检查

```powershell
dotnet build .\Verification\FakeTool\FakeTool.csproj -c Release -f net471
dotnet run --project .\Verification\Verification.csproj -c Release -f net471
dotnet run --project .\tools\ThemeVerification\ThemeVerification.csproj -c Release -f net471
```

默认运行 38 项功能与路径检查；启用本机只读扫描后共 40 项。另外有 11 项 WPF 与启动检查，覆盖：

- 多服务器解析、设备状态、筛选、排除和旧配置兼容。
- Unicode、带空格和引号的命令参数。
- 失败处理、取消命令及清理子进程。
- 自定义安装目录、自动定位、搜索预算及取消。
- 首次启动的空路径、无效路径，以及完整主窗口构造。
- 明暗颜色同步、动画终点、快速切换、资源冻结保护和原生圆角裁剪。

加上 `-- --live` 可以额外调用本机已安装的技师端进行只读扫描，不会连接设备。连接命令通过独立测试程序验证。

## 文档截图

```powershell
dotnet run --project .\tools\Screenshots\Screenshots.csproj -c Release
```

截图使用产品的真实 WPF 窗口和模板，渲染浅色、深色及排除管理界面。工具在独立进程中禁用主窗口的实际扫描事件，只填入演示数据；不会读取客户设备、调用原厂命令行、修改用户配置或写入用户日志。

## 项目结构

| 路径 | 内容 |
| --- | --- |
| `UsbAutoConnect/` | WPF 应用、兼容封装、资源与图标 |
| `Verification/` | 功能检查及测试命令行程序 |
| `tools/ThemeVerification/` | WPF、启动与圆角检查 |
| `tools/Screenshots/` | 使用演示数据渲染文档截图 |
| `tools/IconBuilder/` | 从矢量几何重新生成 ICO |
| `docs/` | 使用说明、开发说明和截图 |
| `downloads/` | 最新分发包及校验值 |
