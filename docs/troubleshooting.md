# BatteryMonitor トラブルシューティング

このメモは、よくありそうな確認項目を短くまとめたものです。

## まず確認すること

- `dotnet build BatteryMonitor.sln` が通るか
- アプリを終了して再起動すると改善するか
- `%LOCALAPPDATA%\BatteryMonitor\settings.json` が壊れていないか
- `debug.log` に例外や取得失敗が出ていないか

## よくある症状

### 温度やサイクル数が `--` になる

WMI が機種ごとに公開している情報が異なるため、取得できない値があります。
これは不具合とは限らず、PC 側がその項目を返していない可能性があります。

### ポップアップの位置がおかしい

- 位置保存が残っていないか確認する
- 複数モニター構成を一度外して試す
- `settings.json` の `WindowLeft` と `WindowTop` を見直す

### 自動起動が効かない

- Setup から導入した Velopack インストール版か確認する。Portable 版と開発実行は登録しない
- タスク マネージャーの「スタートアップ アプリ」で `BatteryMonitor` が無効になっていないか確認する
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` の `BatteryMonitor` が安定ランチャーと `--startup` を指しているか確認する
- 旧 `BatteryMonitorAutoStart` タスクと Run 登録が同時に残っている場合は、通常起動後のログと登録権限を確認する

### 更新が反映されない

- 起動後しばらく待って自動チェックが走るか確認する
- GitHub Releases 側に最新版が公開されているか確認する
- リリースノートのバージョンとアプリ内の更新先 URL を見直す
- Portable 版と開発実行では更新の確認結果を表示できるが、ダウンロードと適用は行わない
