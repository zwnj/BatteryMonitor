# BatteryMonitor ドキュメント案内

このフォルダは、BatteryMonitor の開発資料をまとめる場所です。

## まず読むもの

- [プロジェクト概要](./project-overview.md)
  - 全体構成、責務分担、起動フローを知りたいときに読む
- [リファクタリング分析](./refactor-analysis.md)
  - どこを直すと効果が大きいかを知りたいときに読む
- [リリース手順](./release-procedure.md)
  - GitHub Releases と Velopack の運用を確認したいときに読む
- [トラブルシューティング](./troubleshooting.md)
  - 既知の詰まりどころや確認手順を見たいときに読む

## 実装方針とレビュー

- [領域外クリック時の閉じる処理改善](./reviews/2026-07-18-popup-outside-click-improvement.md)
  - 既存のWPF `Popup`を維持したまま、閉じるアニメーションの多重起動と早期非表示を防ぐ方針

## 役割分担

- 利用者向けの説明は [README.md](../README.md) に置く
- 開発判断や実装メモはこの `docs/` に置く
- 変更履歴や作業ログは必要に応じて別ファイルに分ける
