# リファクタリング分析メモ

このメモは、現状実装を読んで「どこを改善すると効果が大きいか」を整理したものです。
2026-07-01 時点のコードを根拠にしています。

## 先に結論

改善余地が大きいのは次の4点です。

1. `BatteryViewModel` に責務が集まりすぎている
2. `TrayIconController` と `PopupView` に位置・表示制御が重複している
3. 設定保存の扱いが少し不自然で、`ChargeLimit` 更新が分かりにくい
4. `PopupView` と `KeyboardHookService` にライフサイクル上の注意点がある

## 1. `BatteryViewModel` の責務集中

[`ViewModels/BatteryViewModel.cs`](../ViewModels/BatteryViewModel.cs) は、単なる表示モデルを超えて多くの役割を持っています。

根拠:

- `UpdateData` が更新頻度判定、WMI 呼び出し、表示文字列生成、トレイアイコン更新まで担当している
- `ChargeLimit` の setter が設定保存を直接行っている
- `IsStartupEnabled` の getter / setter が `StartupManager` に直結している

該当箇所:

- [`BatteryViewModel.cs:69`](../ViewModels/BatteryViewModel.cs#L69)
- [`BatteryViewModel.cs:307`](../ViewModels/BatteryViewModel.cs#L307)
- [`BatteryViewModel.cs:357`](../ViewModels/BatteryViewModel.cs#L357)

改善候補:

- 更新タイミングの判定を別クラスへ分離する
- 表示用フォーマットを別クラスへ分離する
- 設定保存を ViewModel から外す
- アイコン選択ロジックを独立させる

期待できる効果:

- テストしやすくなる
- 更新ロジックの変更が UI 文字列に波及しにくくなる
- WMI 依存と表示ロジックを分けやすくなる

## 2. ポップアップ位置・表示制御の重複

`TrayIconController` と `PopupView` の両方に、位置決定や保存済み位置の扱いが入っています。

根拠:

- `TrayIconController` に `ApplyPopupPosition`
- `PopupView` に `ApplySavedPosition`
- どちらも `AppSettings.Load()` を参照している

該当箇所:

- [`TrayIconController.cs:469`](../Services/TrayIconController.cs#L469)
- [`PopupView.xaml.cs:223`](../Views/PopupView.xaml.cs#L223)

改善候補:

- ポップアップ初期位置の計算を共通サービスにまとめる
- 「保存済み位置がある場合」と「カーソル位置に置く場合」を1か所で扱う
- DPI 変換の責務を共通化する

期待できる効果:

- 位置ズレの原因を一箇所で追える
- モニター環境差異の修正がしやすくなる
- `TrayIconController` の状態管理が少し軽くなる

## 3. 設定保存の扱い

`ChargeLimit` の setter は、保存時に少し回り道をしています。

根拠:

- いったん `AppSettings.Save(double.NaN, double.NaN, _chargeLimit)` を呼ぶ
- その後 `AppSettings.Load()` で読み直し
- もう一度 `AppSettings.Save(current.WindowLeft, current.WindowTop, _chargeLimit)` を呼ぶ

該当箇所:

- [`BatteryViewModel.cs:317`](../ViewModels/BatteryViewModel.cs#L317)

気になる点:

- 2 回保存していて意図が読み取りにくい
- `NaN` を「維持したい項目の番兵値」として使っているので、API から意味が見えにくい
- 設定変更のたびにファイル I/O が発生する

改善候補:

- `AppSettings` に部分更新メソッドを追加する
- 保存時は「現在の設定を1回だけ読み、1回だけ書く」形にする
- `ChargeLimit` の入力値を検証する

期待できる効果:

- 保存ロジックの意図が明確になる
- 位置設定と充電上限の関係が分かりやすくなる
- 今後の設定項目追加が簡単になる

## 4. ライフサイクルの注意点

### `PopupView` のイベント購読

`PopupView` はコンストラクタで静的イベントとシステムイベントに購読していますが、解除処理が見当たりません。

根拠:

- `ThemeManager.ThemeChanged += ...`
- `SystemEvents.UserPreferenceChanged += ...`
- `SystemEvents.PowerModeChanged += ...`

該当箇所:

- [`PopupView.xaml.cs:35`](../Views/PopupView.xaml.cs#L35)

懸念:

- 破棄や再生成が起きると、イベントが残る可能性がある
- テーマ切り替えや電源イベントの重複反応が起きる余地がある

### `KeyboardHookService` の nullability と静的状態

`KeyboardHookService` は実装自体は分かりやすいですが、nullable 警告が出ています。

根拠:

- `TriggerActivated` が null 非許容だが初期化されていない
- `_proc` が static で、生成タイミングに依存する

該当箇所:

- [`KeyboardHookService.cs:11`](../Services/KeyboardHookService.cs#L11)
- [`KeyboardHookService.cs:27`](../Services/KeyboardHookService.cs#L27)

改善候補:

- `TriggerActivated` を `event EventHandler?` にする
- `_proc` を static にしない
- `Dispose` でフック状態を明示的に管理する

期待できる効果:

- nullability 警告を減らせる
- フックの寿命が追いやすくなる
- 将来的な多重生成時の事故を減らせる

## 5. 起動時オーケストレーション

[`App.xaml.cs`](../App.xaml.cs) はアプリ全体の配線を担っていて、現状では自然な役割です。
ただし、責務はかなり広めです。

根拠:

- トレイアイコン取得
- ViewModel 初期化
- トレイコントローラー初期化
- キーボードフック初期化
- 電源イベント購読
- タイマー制御
- 終了時の破棄

該当箇所:

- [`App.xaml.cs:29`](../App.xaml.cs#L29)

改善候補:

- 起動処理を小さな初期化クラスに分ける
- タイマー制御をアプリコーディネーターに寄せる

これは必須ではありませんが、機能追加が続くなら効いてきます。

## 優先順位

今のコードベースで着手順を付けるなら次の順がよさそうです。

1. `BatteryViewModel` の表示フォーマットと更新判定を分離する
2. 設定保存を `ChargeLimit` setter から切り出す
3. `TrayIconController` と `PopupView` の位置計算を共通化する
4. `PopupView` のイベント解除を整える
5. `KeyboardHookService` の nullability と寿命管理を整理する

## 補足

現状は `dotnet build BatteryMonitor.sln` が通っています。
ただし、ビルド警告は残っているので、次のリファクタ候補の一部はそこに直結しています。

