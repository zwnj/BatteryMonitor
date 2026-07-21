# BatteryMonitor Release Procedure

このリポジトリでは、GitHub ReleasesをVelopackの配布元として使う。
Pack IDは既存インストールとの互換性に関わるため、`BatteryMonitor`から変更しない。

## 現在のリリース構成

- 配布先: `https://github.com/zwnj/BatteryMonitor`
- 実行プロジェクト: `BatteryMonitor3.csproj`
- main exe: `BatteryMonitor3.exe`
- runtime: `win-x64`
- publish: .NET 8自己完結、single-file
- Velopack NuGet / repository-local vpk: `1.2.0`
- 現在の準備対象: `1.0.9` / `v1.0.9`
- prerelease channel: 使用しない
- Portable ZIP: 配布する
- shortcut: Start menuのみ
- コード署名: 未実施

Artifact AttestationはGitHub Actionsで生成されたSetupとPortable ZIPの由来を示す。
コード署名ではないため、未署名の実行ファイルではWindows SmartScreenが未知の発行元として警告する可能性がある。

## ローカル検証とパッケージ作成

通常の検証を実行する。

```powershell
dotnet restore BatteryMonitor.sln
dotnet build BatteryMonitor.sln -c Release --no-restore
dotnet test BatteryMonitor.sln -c Release --no-build --no-restore
dotnet tool restore
```

更新履歴を引き継ぐため、公開済みの前回版を同じreleaseディレクトリへ取得する。
`v1.0.2`以降はVelopack Releaseが存在するため、取得失敗を無視しない。

```powershell
dotnet vpk download github `
  --repoUrl "https://github.com/zwnj/BatteryMonitor" `
  --outputDir artifacts/release
```

前回版を残したまま対象版をパッケージする。

```powershell
.\scripts\package.ps1 -Version 1.0.9 -KeepReleaseDirectory
```

スクリプトは`BatteryMonitor3.csproj`を自己完結形式でpublishし、バージョンを次へ同一指定する。

- `Version`
- `AssemblyVersion`
- `FileVersion`
- `InformationalVersion`
- Velopack `packVersion`

生のpublishディレクトリは中間生成物であり、GitHub Releaseへ公開しない。
生成物は`artifacts/`配下だけに置き、Gitへコミットしない。

## ローカル生成物の確認

`artifacts/release`で、固定ファイル名を前提にせず実際の生成結果を確認する。

- `*-Setup.exe`
- `*-Portable.zip`
- `*-full.nupkg`
- 対応するdelta package
- `RELEASES`
- `releases.win.json`

full packageと更新フィードでPack ID、対象バージョン、前回版の履歴を確認する。
Setup、Portable ZIP、full package、feed以外の大量のDLLや生publish出力をRelease assetsへ含めない。

`1.0.9`は従来のframework-dependent publishから自己完結publishへ切り替える最初の版である。
そのため`1.0.8`からのdelta packageもfull packageに近い大きさになるが、次版以降の差分生成では自己完結版同士を比較できる。

## 公開手順

公開前に、対象コミットが`main`へpush済みであり、作業ツリーがcleanであることを確認する。
対象版と一致する注釈付きタグを新規作成し、そのタグだけをpushする。

```powershell
git tag -a v1.0.9 -m "BatteryMonitor 1.0.9 リリース"
git push origin v1.0.9
```

タグpushで`.github/workflows/release.yml`が起動する。
workflowはタグから数値版を取得し、ソース検証、前回Release取得、パッケージ、workflow artifact保存、Attestation、安定Release公開を順に行う。

公開済みまたはpush済みのタグは、失敗しても移動・上書きしない。
修正をコミットし、patch番号を上げた新しいタグで再リリースする。

## GitHub Actionsの権限と認証情報

workflowの権限は次だけにする。

- `contents: write`: GitHub Release公開
- `id-token: write`: Artifact Attestation
- `attestations: write`: Artifact Attestation

GitHub tokenはworkflow環境変数だけでvpkと`gh`へ渡す。
アプリ、リポジトリローカルスクリプト、Release notes、生成物へtokenを保存しない。

## 公開後の確認

- ReleaseがDraftでもPrereleaseでもない
- Releaseタグ、アセンブリ、ファイル、full packageが同じバージョン
- Setup、Portable ZIP、full/delta package、`RELEASES`、`releases.win.json`が添付されている
- raw publishディレクトリや大量のDLLが添付されていない
- workflow artifactが14日保持されている
- SetupとPortable ZIPのAttestationを`gh attestation verify`で検証できる
- 更新フィードに前回版と今回版の履歴が含まれる
- Setupからインストール、起動、アンインストールできる

## 実機確認チェックリスト

次はインストール済み旧版と公開済み新版が必要なため、公開前の通常テストでは確認しない。

- [ ] 未確認: Setup版から更新し、ダウンロード後に安全に再起動する
- [ ] 未確認: Portable版と開発実行で更新適用が行われない
- [ ] 未確認: タスク マネージャーの「スタートアップ アプリ」に表示される
- [ ] 未確認: 無効化後の通常起動で無効状態が維持される
- [ ] 未確認: サインアウト／サインイン後、ポップアップを開かずトレイ常駐する
- [ ] 未確認: `--startup`の二重起動で既存ポップアップを前面化しない
- [ ] 未確認: 明示的な二重起動で既存ポップアップを表示する
- [ ] 未確認: 更新後に旧タスクが残らずRun登録が重複しない
- [ ] 未確認: アンインストール後に旧タスクとRun登録が残らない
