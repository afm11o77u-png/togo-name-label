$ErrorActionPreference = 'Stop'
$baseDir = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
& $compiler /nologo /target:winexe /platform:anycpu "/out:$baseDir\統合ネームラベル.exe" /reference:System.Xml.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$baseDir\DotNetBarcode.dll" "$PSScriptRoot\Program.cs" "$PSScriptRoot\LabelCatalog.cs" "$PSScriptRoot\AdditionalCatalog.cs" "$PSScriptRoot\UserFeatures.cs" "$PSScriptRoot\CatalogVerification.cs" "$PSScriptRoot\GenericLabel.cs"
if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました。' }
Copy-Item -LiteralPath "$PSScriptRoot\App.config" -Destination "$baseDir\統合ネームラベル.exe.config"
