# BatteryMonitor プロジェクト概要

このメモは、現在の構成と責務を確認するための開発資料です。
2026-06-30 時点でコードを読んだ内容をもとにしています。

全体の入口は [docs/README.md](./README.md) にあります。

## 概要

BatteryMonitor は Windows のシステムトレイに常駐する WPF アプリです。
通常のメインウィンドウは持たず、トレイアイコンとポップアップでバッテリー情報を表示します。

主な機能は次のとおりです。

- バッテリー残量、充放電状態、電圧、電力、推定残り時間、容量、温度、健康度、サイクル数の表示
- トレイアイコンの残量別表示
- トレイアイコンのホバー、左クリック、右 Shift 2 回押しによるポップアップ表示
- ポップアップのピン留め、ドラッグ移動、位置保存
- ダーク/ライトテーマ切り替え
- Windows 起動時の自動実行設定
- Velopack と GitHub Releases によるアプリ内更新

## 技術スタック

- .NET 8
- WPF
- `Hardcodet.NotifyIcon.Wpf`
- `System.Management`
- Windows WMI
- Win32 API / DWM API

## 起動と全体フロー

`App.xaml` に `TaskbarIcon` リソースが定義され、`TrayPopup` として `PopupView` が接続されています。

`Program.cs` は通常の WPF 初期化より前に Velopack の処理とインストールイベントを実行します。
その後、`App.xaml.cs` がアプリ全体のオーケストレーションを担います。

1. Velopack の install/update/uninstall callback で自動起動登録を修復または削除する。
2. `--startup` を解析し、単一インスタンスを確立する。
3. 通常起動で二重起動した場合だけ既存インスタンスへ表示を通知する。
4. Run 登録を修復し、BatteryMonitor 所有の旧タスクを移行する。
5. 未処理例外のログ設定とトレイ、ViewModel、キーボードフックを初期化する。
6. 電源状態変更イベントを購読し、`DispatcherTimer` でバッテリー情報を更新する。
7. 起動20秒後に更新を静かに確認する。

更新間隔は次の設計です。

- ポップアップ非表示時: 15 秒ごと
- ポップアップ表示時: 5 秒ごと
- ポップアップ表示直後の詳細更新: 180 ms 後

## ディレクトリと責務

| 場所 | 現在の主な責務 |
| --- | --- |
| `App.xaml`, `App.xaml.cs` | アプリ起動、DI なしの手動構築、タイマー、イベント購読、終了処理 |
| `Models` | 永続化設定と WMI 取得結果の受け渡し型 |
| `Services` | バッテリー取得、トレイ制御、テーマ、キーボードフック |
| `Infrastructure/Startup` | Run 登録、旧タスク移行、起動モード、単一インスタンス |
| `Updates` | 更新確認、ダウンロード、適用と再起動の分離 |
| `ViewModels` | UI 表示用文字列の整形、更新頻度制御、設定コマンド、トレイアイコン更新 |
| `Views` | ポップアップ UI、ドラッグ移動、テーマ切替アニメーション、透明効果 |
| `Views/Themes` | ダーク/ライトテーマの ResourceDictionary |
| `Helpers` | ログ、コマンド、アイコン読み込み、DWM 背景、コンバーター |
| `Images/TrayIconsIco` | 残量・色・充電状態別のトレイアイコン |

## 主要クラス

### `BatteryViewModel`

UI にバインドされる状態を管理します。
`BatteryService` から取得した `BatteryInfo` を、画面表示用の文字列や `ImageSource` に変換します。

現在の責務はやや大きく、次が混在しています。

- WMI 取得タイミングの判断
- 非同期更新の排他
- バッテリー値の単位変換
- 表示文字列の整形
- 充電上限までの推定時間計算
- 設定保存
- トレイアイコン差し替え

### `BatteryService`

WMI からバッテリー情報を取得します。

主に参照している WMI クラスは次のとおりです。

- `BatteryStatus`
- `BatteryStaticData`
- `BatteryFullChargedCapacity`
- `BatteryCycleCount`
- `MSAcpi_ThermalZoneTemperature`

設計容量、満充電容量、サイクル数、温度などはキャッシュを持ち、呼び出し側から渡される更新フラグで再取得します。
WMI が取得できない項目は 0 や `--` 相当の表示につながる前提です。

### `TrayIconController`

トレイアイコン操作とポップアップ表示状態を制御します。

担当している操作は次のとおりです。

- ホバー後 0.5 秒でポップアップ表示
- 左クリックで明示表示
- 右クリック時はコンテキストメニュー優先
- ピン留め時は閉じない
- 右 Shift ショートカットによる表示/非表示
- マウス位置監視による自動クローズ
- 表示・非表示アニメーション連携
- Win32 API によるフォーカス制御
- 保存済み位置またはカーソル位置へのポップアップ配置

状態フラグが多いため、リファクタ時は表示モードを明示的な型にまとめる余地があります。

### `PopupView`

ポップアップ UI と、UI に近い操作を担当します。

主な責務は次のとおりです。

- テーマ切り替えと切り替えアニメーション
- 透明効果とアクリル背景の適用
- ドラッグ移動
- マルチモニターの作業領域内への位置クランプ
- 位置保存
- 閉じるアニメーション
- 設定オーバーレイ表示

UI コードビハインドとしては現実的ですが、Win32 座標変換と永続化が混ざっているため、切り出し候補があります。

### `AppSettings`

`%LOCALAPPDATA%\BatteryMonitor\settings.json` に設定を保存します。

保存している値は次のとおりです。

- `WindowLeft`
- `WindowTop`
- `ChargeLimit`

現在は `Save(left, top, chargeLimit)` で常に全体を書き換えるため、個別項目更新時に読み直してから保存するコードがあります。
今後は設定リポジトリ、または部分更新メソッドを用意すると扱いやすくなります。

### `SvgIconGenerator`

クラス名は `SvgIconGenerator` ですが、現在は SVG 生成ではなく `Images/TrayIconsIco` の `.ico` ファイルを読み込んでキャッシュしています。
リファクタ時には `TrayIconProvider` のような名前に変えると実態と合います。

## 更新とスタートアップ

- 更新確認は手動と自動で通知方針を分け、自動確認では最新版や非インストール状態を通知しない。
- Portable 版と開発実行は正常な非インストール状態として更新適用とRun登録を行わない。
- 更新適用前にタイマー、WMI更新、システムイベント、フック、トレイを解放する。
- インストール版は安定した Velopack ランチャーを `"...\\BatteryMonitor3.exe" --startup` の形で Run キーへ登録する。
- Run 値が同一なら書き直さず、Windows が保持する有効・無効状態を尊重する。`StartupApproved` は操作しない。
- 旧 `BatteryMonitorAutoStart` タスクは所有情報を確認できた場合だけ削除する。

詳細は [Velopack 更新・スタートアップ ADR](./adr/0001-velopack-update-and-startup.md) を参照してください。

## 現在の注意点

- `README.md`、コメント、UI 表示文字列の一部に文字化けが残っています。
- ただし `dotnet build BatteryMonitor.sln` は成功しており、コンパイル上は壊れていません。
- 文字化け修正はロジック変更と分けるのが安全です。
- `BatteryViewModel.UpdateData` は `async void` です。タイマーイベントから呼ばれているため動きますが、テストやエラー伝播の観点では `Task` 化の余地があります。
- `BatteryViewModel` が表示整形、取得頻度、設定保存、アイコン更新まで抱えています。
- `TrayIconController` と `PopupView` の両方にポップアップ位置適用のロジックがあります。
- `PopupView` はイベント購読を解除していません。長寿命アプリなので大きな問題になりにくい可能性はありますが、リファクタ時には確認対象です。
- WMI は機種依存で取れない値があるため、取得失敗時の表示とログ方針を固定しておく必要があります。
- `Logger` は実行ディレクトリに `debug.log` を書きます。インストール先によっては書き込み権限の確認が必要です。

## リファクタリング候補

優先度順に進めるなら、次の順が安全です。

1. 文字化けした README、コメント、UI 文言を復旧する。
2. `SvgIconGenerator` を実態に合う名前へ変更し、アイコン選択ロジックを明確化する。
3. `BatteryViewModel` から表示フォーマット処理を切り出す。
4. `BatteryViewModel` から更新頻度判断を切り出す。
5. `AppSettings` に部分更新メソッドを追加し、読み直し保存を減らす。
6. ポップアップ位置計算を `PopupView` と `TrayIconController` から共通サービスへ切り出す。
7. `TrayIconController` の表示状態を enum や小さな状態オブジェクトにまとめる。
8. WMI 取得をインターフェイス化して、表示整形や更新制御をテストしやすくする。

## 動作確認

現時点で確認したコマンドです。

```powershell
dotnet build BatteryMonitor.sln
```

結果:

- 成功
- 警告 0
- エラー 0

## 次に着手しやすい単位

最初の一手としては、動作リスクが低い「ドキュメント復旧」と「名前の整理」から入るのがよさそうです。

おすすめ順は次のとおりです。

1. README を UTF-8 の正常な日本語に書き直す。
2. 画面表示文字列の文字化けを直す。
3. `SvgIconGenerator` の命名とコメントを整理する。
4. `BatteryViewModel` の表示フォーマットだけを小クラスに切り出す。
