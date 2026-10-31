@echo off
chcp 65001 >nul
setlocal
rem 統合ネームラベル：ソースから exe をビルドして 統合ネームラベル\ に置く
set "ROOT=%~dp0"
set "APPDIR=%ROOT%統合ネームラベル"
set "EXE=%APPDIR%\統合ネームラベル.exe"
set "PS1=%APPDIR%\ソース\ビルド.ps1"

echo ============================================
echo  統合ネームラベル 更新（ソースからビルド）
echo ============================================
echo.

if not exist "%PS1%" (
  echo [失敗] ビルド.ps1 が見つかりません: "%PS1%"
  echo ZIP を展開したフォルダーの中で 更新.bat を実行してください。
  goto :fail
)

rem 既存 exe を日時付きでバックアップ
if exist "%EXE%" (
  powershell -NoProfile -ExecutionPolicy Bypass -Command "$d=Join-Path $env:APPDIR 'バックアップ'; New-Item -ItemType Directory -Force -Path $d | Out-Null; $n='統合ネームラベル_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.exe'; Copy-Item -LiteralPath $env:EXE -Destination (Join-Path $d $n); Write-Host ('バックアップ: '+(Join-Path $d $n))"
  if errorlevel 1 (
    echo [失敗] 既存 exe のバックアップに失敗しました。
    goto :fail
  )
) else (
  echo 既存の exe がないため、バックアップは作成しません。
)
echo.

echo ビルドを開始します（NuGet 復元を含みます。初回はインターネット接続が必要です）...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
if errorlevel 1 goto :fail

echo.
echo ============================================
echo  [成功] 更新が完了しました。
echo  出力先: "%EXE%"
echo ============================================
echo.
pause
exit /b 0

:fail
echo.
echo ============================================
echo  [失敗] 更新できませんでした。上のメッセージを確認してください。
echo  Visual Studio Installer で「.NET デスクトップ開発」ワークロード
echo  （.NET Framework 4.8 開発ツールを含む）が入っているか確認してください。
echo ============================================
echo.
pause
exit /b 1
