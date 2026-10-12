# 統合ネームラベル（togo-name-label）

.NET Framework 4.8 WinForms のネームラベル印刷アプリです。詳細な操作は [`統合ネームラベル/使い方.txt`](統合ネームラベル/使い方.txt) を参照してください。

## ビルド

Windows 上で Visual Studio / Build Tools の MSBuild が必要です。

```powershell
cd 統合ネームラベル\ソース
.\ビルド.ps1
```

- プロジェクト: `UnifiedLabels.csproj`
- NuGet: `ZXing.Net` **厳密に 0.16.10**（`packages.lock.json` + `RestoreLockedMode`）
- 出力: `統合ネームラベル/統合ネームラベル.exe`（同フォルダーに `zxing.dll`、当面は `DotNetBarcode.dll` も同梱）

ソースを変更したら、上記のビルドで得た `統合ネームラベル.exe`・`統合ネームラベル.exe.config`・`zxing.dll` も同じコミットに含めてください（リポジトリ上で最新の実行ファイルを揃える運用）。CI はビルド・検証のみ行い、exe の自動コミットはしません。

## バーコード検証（自動）

```powershell
.\統合ネームラベル\統合ネームラベル.exe --verify-barcode .\barcode-verify-out
```

代表的な QR / Code39 値について、生成画像のサイズとデコード内容を検査し、`barcode-verification.txt` を出力します（確認用画像は書き出しません）。

## 未検証（実機必須）

自動検証は生成ビットマップのサイズとソフトウェアデコードまでです。**実プリンターでの印字・カット位置、および実スキャナーでの読取りは未確認**です。ZXing 置換後は、新旧比較と実機確認が終わるまで `DotNetBarcode.dll` を削除しないでください。

サードパーティ表記: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)
