# BatteryMonitor Release Procedure

このリポジトリでは、GitHub Releases を Velopack の配布元として使う。

## 前提

- 配布先リポジトリは `https://github.com/zwnj/BatteryMonitor`
- アプリ側の更新先 URL は `App.xaml.cs` の `UpdateRepositoryUrl`
- リリース workflow は `.github/workflows/release.yml`
- バージョン番号は workflow が指定値を使うか、未指定なら最新 Release から patch を 1 つ上げて決める
- `Directory.Build.props` の `AppVersion` はローカルビルド用の既定値として使う
- 既存の `v1.0.1` は ZIP 配布の Release で、Velopack 形式ではない
- Velopack の最初の本番 Release は `v1.0.2` から始める

## 通常リリース

1. `main` に必要な変更をコミットする。
2. ローカルで `dotnet build BatteryMonitor.sln` を通す。
3. 必要なら `v1.2.3` のように明示したい版番号を決める。
4. タグを push する。
5. GitHub Actions の `Release` workflow が Velopack パッケージを作成し、GitHub Release にアップロードする。

## 手動リリース確認

1. GitHub Actions から `Release` workflow を手動実行する。
2. `version` を空欄にすると、最新 Release から patch が 1 つ上がる。
3. `version` に値を入れると、その版番号が使われる。
4. workflow が `vpk pack` と `vpk upload` を実行し、Release を作る。
5. Release に `Setup.exe` と `nupkg` 群、`RELEASES`、`releases.win.json` が出ていることを確認する。

## アプリ側の確認

- トレイメニューの「更新を確認」で手動チェックする。
- 起動後の自動確認は、起動から 20 秒後に静かに1回実行する。
- 更新がある場合は、利用者の承認後にダウンロードし、アプリ固有リソースを解放してから適用・再起動する。
- 自動起動登録はインストール・更新・通常起動で修復し、アンインストールで削除する。

### 実機確認チェックリスト

次はインストール済みの旧版と公開済みの新版が必要なため、通常の単体テストでは確認しない。

- [ ] 未確認: Setup版から更新し、ダウンロード後に安全に再起動する
- [ ] 未確認: Portable版と開発実行で更新適用が行われない
- [ ] 未確認: タスク マネージャーの「スタートアップ アプリ」に表示される
- [ ] 未確認: 無効化後の通常起動で無効状態が維持される
- [ ] 未確認: サインアウト／サインイン後、ポップアップを開かずトレイ常駐する
- [ ] 未確認: `--startup` の二重起動で既存ポップアップを前面化しない
- [ ] 未確認: 明示的な二重起動で既存ポップアップを表示する
- [ ] 未確認: 更新後に旧タスクが残らずRun登録が重複しない
- [ ] 未確認: アンインストール後に旧タスクとRun登録が残らない

## 補足

- `workflow_dispatch` は検証用に使える。
- 最初の Velopack Release では前回版がないため、delta が作られないことがある。
- 以後のリリースでは `vpk download github` が前回版を取り込み、delta 生成を助ける。
