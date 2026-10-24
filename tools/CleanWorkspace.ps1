$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$archiveRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $sourceRoot) ('root-autoconnect-archive-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
$relativePaths = @(
    '发布\root包 逆向自动连接助手.exe', '发布\使用说明.md', '发布\root包 逆向自动连接助手-net471.zip',
    '发布\Framework4.7.1版', '发布\便携压缩版', '发布\轻量版', '验证记录.md',
    'UsbAutoConnect\bin', 'UsbAutoConnect\obj', 'Verification\bin', 'Verification\obj',
    'Verification\FakeTool\bin', 'Verification\FakeTool\obj',
    'tools\IconBuilder\bin', 'tools\IconBuilder\obj',
    'tools\ThemeVerification\bin', 'tools\ThemeVerification\obj',
    'tools\Screenshots\bin', 'tools\Screenshots\obj'
)
$moved = [Collections.Generic.List[string]]::new()
foreach ($relativePath in $relativePaths) {
    $sourcePath = [IO.Path]::GetFullPath((Join-Path $sourceRoot $relativePath))
    $destinationPath = [IO.Path]::GetFullPath((Join-Path $archiveRoot $relativePath))
    if (!$sourcePath.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source outside project' }
    if (!$destinationPath.StartsWith($archiveRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destination outside recovery archive' }
    if (!(Test-Path -LiteralPath $sourcePath)) { continue }
    if ((Get-Item -LiteralPath $sourcePath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing to move a reparse point' }
    if (Test-Path -LiteralPath $destinationPath) { throw 'Archive destination exists; no files overwritten' }
    New-Item -Path (Split-Path -Parent $destinationPath) -ItemType Directory -Force | Out-Null
    Move-Item -LiteralPath $sourcePath -Destination $destinationPath
    $moved.Add($relativePath)
}
if ($moved.Count -gt 0) {
    $recovery = @('Recovery archive for: ' + $sourceRoot, 'Original relative paths:', '') + $moved.ToArray()
    $recovery | Set-Content -LiteralPath (Join-Path $archiveRoot 'RESTORE-INFO.txt') -Encoding utf8
}
Write-Output ('Archived items: ' + $moved.Count)
Write-Output ('Recovery location: ' + $archiveRoot)
