# BatteryMonitor リポジトリ用メモ

このリポジトリで作業する Codex 向けの補足ルールです。

## まず守ること

- テキストファイルは UTF-8 で読むこと。PowerShell では `Get-Content -Encoding UTF8` を優先する。
- このリポジトリのテキストは原則 `utf-8-bom` で保存する。
- 既存の変更は勝手に戻さないこと。
- 破壊的な操作はしないこと。
- 変更前に、対象ファイルと周辺の責務を確認すること。

## プロジェクトの前提

- アプリは .NET 8 の WPF で作られている。
- 画面の主役はメインウィンドウではなく、トレイアイコンとポップアップ。
- バッテリー情報は WMI から取得している。
- 設定は `%LOCALAPPDATA%\BatteryMonitor\settings.json` に保存している。
- 自動起動は Velopack インストール版だけを現在ユーザーの Run キーへ登録し、Task Manager の「スタートアップ アプリ」で管理する。

## 主要な責務

- `App.xaml.cs`
  - 起動時の組み立て、タイマー、イベント購読、終了処理
- `ViewModels/BatteryViewModel.cs`
  - バッテリー情報の表示用変換、更新制御、設定への橋渡し
- `Services/BatteryService.cs`
  - WMI からのバッテリー情報取得
- `Services/TrayIconController.cs`
  - トレイ表示、ポップアップの開閉、フォーカス制御
- `Views/PopupView.xaml` と `Views/PopupView.xaml.cs`
  - ポップアップ UI、テーマ切り替え、ドラッグ、位置保存

## リファクタ時の注意

- `BatteryViewModel` は責務が広いので、表示整形と更新判定は分離候補。
- `TrayIconController` と `PopupView` に位置計算が重複しているので、統合候補。
- `SvgIconGenerator` は実態として ICO 読み込みなので、名前の見直し候補。
- 文字化けに見える箇所は、まず UTF-8 で読んでから判断すること。

## 確認コマンド

```powershell
dotnet build BatteryMonitor.sln
```

必要なら、リファクタの方針が固まった段階でこのファイルを更新してよい。

## Oracle 利用メモ

- この環境では `oracle` のブラウザ実行は `--browser-manual-login` と `--browser-keep-browser` を前提にする。
- モデル切り替え UI の検出で失敗することがあるため、ブラウザ実行では `--browser-model-strategy ignore` を優先する。
- 会話を続けるときは `-s <slug>` でセッション名を固定し、`--followup <slug>` で再接続する。
- 動作確認の最小例は次のとおり。

```powershell
oracle --engine browser --browser-manual-login --browser-keep-browser --browser-model-strategy ignore -s "codex browser check" -p "Respond with exactly OK"
```

### よく使う形

初回:

```powershell
oracle --engine browser --browser-manual-login --browser-keep-browser --browser-model-strategy ignore -s "my-session" -p "最初の依頼"
```

続き:

```powershell
oracle --engine browser --browser-manual-login --browser-keep-browser --browser-model-strategy ignore --followup my-session -p "続きの依頼"
```

短い動作確認:

```powershell
oracle --engine browser --browser-manual-login --browser-keep-browser --browser-model-strategy ignore -s "oracle-smoke-test" -p "Respond with exactly OK"
```

## 補足

- 文字化けが見えたら、まず読み取り側のエンコーディングを疑う。
- 迷ったときは UTF-8 BOM として読み直してから判断する。
