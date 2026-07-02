# BatteryMonitor Release Procedure

このリポジトリでは、GitHub Releases を Velopack の配布元として使う。

## 前提

- 配布先リポジトリは `https://github.com/zwnj/BatteryMonitor`
- アプリ側の更新先 URL は `App.xaml.cs` の `UpdateRepositoryUrl`
- リリース workflow は `.github/workflows/release.yml`
- 既存の `v1.0.1` は ZIP 配布の Release で、Velopack 形式ではない
- Velopack の最初の本番 Release は `v1.0.2` から始める

## 通常リリース

1. `main` に必要な変更をコミットする。
2. ローカルで `dotnet build BatteryMonitor.sln` を通す。
3. `v1.0.2` のようなタグを切る。
4. タグを push する。
5. GitHub Actions の `Release` workflow が Velopack パッケージを作成し、GitHub Release にアップロードする。

## 手動リリース確認

1. GitHub Actions から `Release` workflow を手動実行する。
2. `version` に `1.2.3` のような値を入れる。
3. workflow が `vpk pack` と `vpk upload` を実行し、Release を作る。
4. Release に `Setup.exe` と `nupkg` 群、`RELEASES`、`releases.win.json` が出ていることを確認する。

## アプリ側の確認

- トレイメニューの「更新を確認」で手動チェックする。
- 起動後の自動確認は、起動から 20 秒後に静かに1回実行する。
- 更新がある場合は、ダウンロード確認後に再起動する。

## 補足

- `workflow_dispatch` は検証用に使える。
- 最初の Velopack Release では前回版がないため、delta が作られないことがある。
- 以後のリリースでは `vpk download github` が前回版を取り込み、delta 生成を助ける。
