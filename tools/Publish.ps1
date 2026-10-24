param([switch]$PackageOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path (Join-Path $projectRoot '发布') 'root包 逆向自动连接助手-net471'
$downloads = Join-Path $projectRoot 'downloads'
$targetPath = Join-Path $outputPath 'root包 逆向自动连接助手.exe'
$projectFile = Join-Path $projectRoot 'UsbAutoConnect\UsbAutoConnect.csproj'
$version = ([xml](Get-Content -LiteralPath $projectFile -Raw)).Project.PropertyGroup.Version
New-Item -Path $outputPath,$downloads -ItemType Directory -Force | Out-Null
if (!$PackageOnly) {
    if (Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $targetPath }) {
        throw '请先关闭助手再重新编译；只更新说明和下载包可使用 -PackageOnly。'
    }
    dotnet build $projectFile -c Release -f net471 -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw '.NET Framework 4.7.1 编译失败，保留现有发布版。' }
    $compiled = Join-Path $projectRoot 'UsbAutoConnect\bin\Release\net471'
    Copy-Item -LiteralPath (Join-Path $compiled 'RootAutoConnect.exe') -Destination $targetPath -Force
    Copy-Item -LiteralPath (Join-Path $compiled 'RootAutoConnect.exe.config') -Destination ($targetPath + '.config') -Force
}
if (!(Test-Path -LiteralPath $targetPath) -or !(Test-Path -LiteralPath ($targetPath + '.config'))) {
    throw '运行文件缺失，请去掉 -PackageOnly 重新构建。'
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\USAGE.md') -Destination (Join-Path $outputPath '使用说明.md') -Force
$archive = Join-Path $downloads ('root-autoconnect-net471-v' + $version + '.zip')
Compress-Archive -LiteralPath $targetPath, ($targetPath + '.config'), (Join-Path $outputPath '使用说明.md') -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $downloads 'SHA256SUMS.txt'), $hash + '  ' + (Split-Path -Leaf $archive) + [Environment]::NewLine, [Text.Encoding]::ASCII)
Write-Output ('运行文件：' + $targetPath)
Write-Output ('发布下载：' + $archive)
