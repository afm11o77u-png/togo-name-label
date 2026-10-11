# 統合ネームラベル — NuGet 復元込みの再現可能ビルド（UnifiedLabels.csproj と整合）
$ErrorActionPreference = 'Stop'
$baseDir = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $PSScriptRoot 'UnifiedLabels.csproj'

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    foreach ($candidate in @(
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
        )) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    return $null
}

$msbuild = Find-MSBuild
if (-not $msbuild) {
    throw 'MSBuild が見つかりません。Visual Studio または Build Tools（.NET desktop build tools）を入れてください。'
}

Write-Host "MSBuild: $msbuild"
# packages.lock.json で ZXing.Net 0.16.10 を固定（勝手に追従させない）
& $msbuild $proj /t:Restore /p:Configuration=Release /p:RestoreLockedMode=true /v:m
if ($LASTEXITCODE -ne 0) { throw 'NuGet 復元に失敗しました。' }

& $msbuild $proj /t:Rebuild /p:Configuration=Release /p:RestoreLockedMode=true /v:m
if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました。' }

$exe = Join-Path $baseDir '統合ネームラベル.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "出力が見つかりません: $exe" }
$config = Join-Path $baseDir '統合ネームラベル.exe.config'
if (!(Test-Path -LiteralPath $config)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'App.config') -Destination $config
}
$zxing = Join-Path $baseDir 'zxing.dll'
if (!(Test-Path -LiteralPath $zxing)) { throw 'zxing.dll が出力フォルダーにありません（ZXing.Net 参照を確認）。' }

Write-Host "ビルド完了: $exe"
