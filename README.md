# BatteryMonitor3

BatteryMonitor3 は、Windows のシステムトレイに常駐して、必要なときだけバッテリー情報をすばやく確認できる軽量モニターアプリです。

普段は画面を占有せず、トレイアイコンやショートカットからポップアップで情報を表示します。

## 使う人向け

### このアプリでできること

BatteryMonitor3 は、バッテリー残量や充放電レートをシステムトレイからすばやく確認するためのアプリです。ポップアップでは、残量、充放電レート、電圧、推定残り時間、詳細容量、温度、健康度などをまとめて確認できます。テーマ切り替え、ピン留め表示、Windows 起動時の自動実行、充電上限の設定にも対応しています。

### 使い方

#### 起動

アプリを起動すると、通常のウィンドウは開かず、そのままシステムトレイに常駐します。普段は画面を占有せず、必要なときだけポップアップで情報を確認する使い方を想定しています。

#### ポップアップ表示

トレイアイコンにカーソルを合わせると、ポップアップが表示されます。また、`右Shift` をすばやく 2 回押して表示することもできます。右Shift での表示は、トレイにカーソルがない状態でもすぐに呼び出せるショートカットです。

#### ホバー表示とスティッキー表示

ホバーで表示したポップアップは、カーソルが離れると自動で閉じます。一方で、クリックやショートカットで明示的に表示した場合は、内容を確認したり操作したりしやすい表示モードになります。必要であれば、そのまま固定表示に切り替えることもできます。

#### ピン留め

ポップアップ右上のピンアイコンで固定表示を切り替えられます。ピン留め中はポップアップが自動で閉じないため、情報を見続けたいときや設定を調整したいときに便利です。

#### テーマ切り替え

ポップアップ右上のトグルで、ライトモードとダークモードを切り替えられます。見やすさや好みに合わせてその場で変更できます。

#### 設定

設定ボタンからは、Windows 起動時に自動実行するかどうかと、充電上限の値を変更できます。

#### ポップアップ位置

ポップアップはドラッグで移動できます。移動した位置は保存され、次回以降も同じ場所に表示されます。

#### 終了

終了したいときは、トレイアイコンを右クリックして終了メニューを開きます。

### 補足

初回ポップアップ後は、WPF の描画リソース確保によりメモリ使用量が増えることがあります。また、常駐時の負荷を減らすため、更新頻度や取得項目は用途に応じて最適化されています。トレイアイコンはバッテリー残量と充電状態に応じて切り替わります。

## 開発者向け

### 技術スタック

- .NET 8
- WPF
- `Hardcodet.NotifyIcon.Wpf`
- `System.Management`

### 構成

[App.xaml.cs](/mnt/c/Users/yushin/source/repos/BatteryMonitor3/BatteryMonitor3/App.xaml.cs) では起動処理、トレイ初期化、更新タイマー管理を担当します。[ViewModels/BatteryViewModel.cs](/mnt/c/Users/yushin/source/repos/BatteryMonitor3/BatteryMonitor3/ViewModels/BatteryViewModel.cs) は表示用データ整形、更新頻度制御、トレイアイコン更新の中心です。[Services/BatteryService.cs](/mnt/c/Users/yushin/source/repos/BatteryMonitor3/BatteryMonitor3/Services/BatteryService.cs) は WMI からのバッテリー情報取得を担当し、[Services/TrayIconController.cs](/mnt/c/Users/yushin/source/repos/BatteryMonitor3/BatteryMonitor3/Services/TrayIconController.cs) はトレイポップアップの表示制御を行います。UI は [Views/PopupView.xaml](/mnt/c/Users/yushin/source/repos/BatteryMonitor3/BatteryMonitor3/Views/PopupView.xaml) にあり、状態別トレイアイコンは [Images/TrayIconsIco](/mnt/c/Users/yushin/source/repos/BatteryMonitor3/BatteryMonitor3/Images/TrayIconsIco) にあります。

### 開発メモ

- メイン画面は廃止済みで、トレイ常駐専用アプリとして動作します。
- トレイアイコンは実行時 SVG 生成ではなく、事前生成した `.ico` を読み込みます。
- バッテリーの重要情報は短めの間隔で、それ以外は長めの間隔で更新します。

### ビルド

Windows 環境で .NET 8 SDK を使ってビルドします。

```bash
dotnet build BatteryMonitor3.sln
```

### 注意

- WPF のため、実行とビルドは Windows 環境が前提です。
- `bin/`, `obj/`, `.vs/`, `*.user` などのローカル生成物は Git 管理しません。
