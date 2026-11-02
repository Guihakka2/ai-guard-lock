# 公開前の残作業

リポジトリはPrivateのままです。安全の保証やPC全体の侵害調査を完了したという意味ではありません。

## 完了した確認

- [x] 1.0.2の6配布物とハッシュ（12添付）を[非公開下書き](https://github.com/Guihakka2/ai-guard-lock/releases/tag/untagged-f07d4bfe0a6cb251a8de)に保存。旧1.0.1も保持。6アーカイブのハッシュ一致・個人パス等の混入0件・Defender脅威検出0件を確認。

- [x] 1.0.1の6アーカイブを作成し、GitHubの非公開下書きに12添付ファイルとして保管。
- [x] 1.0.1のハッシュ一致、個人パス・機微ファイルの混入0件を再確認。
- [x] Windows・共通アプリのNuGet直接・間接依存の監査で既知報告0件を再確認。
- [x] Avalonia 12.1.4への更新エラーを再現し、関連3依存・ウィンドウ装飾API・画像保存APIを一括移行。ビルド成功、自動20チェック・Windows実ウィンドウ4チェック成功、依存監査0件。
- [x] GitHubの共同編集者・Deploy keys・Webhooks・Runner・Secrets・Actions既定権限を再確認。GitHub Appsの対象範囲も再確認。
- [x] 利用者の指示によりCodex・Claude・GraphifyのGitHub AppsをAI Guard Lockの1リポジトリだけに限定して保存後に確認。
- [x] 手元のWindows PCのデスクトップショートカットをハッシュ確認済みの1.0.1に切り替え、設定画面の起動とWindows Hello利用可能表示を確認。旧版の重複した設定画面は終了。
- [x] 公開準備PR #4とAvalonia移行PR #5を取り込み、個別更新PR #1〜#3を閉じた。
- [x] 手動実行のOS別ワークフローを追加し、GitHub-hosted Windows・Ubuntu・macOS ARM64でビルドとヘッドレス20チェックの成功を確認。[実行記録](https://github.com/Guihakka2/ai-guard-lock/actions/runs/41375991374)。

## 公開前に必要な判断・操作

- [ ] 旧1.0.0 Releaseと12添付ファイルの処置。個人パスを含む配布物の公開を防ぐため。削除は承認されていないので実行しない。利用者の現在の希望は、まず手元で普通に使える状態にすること。リポジトリはPrivateを維持する。
- [ ] 修正版1.0.2の配布対象を決定。説明・12添付ファイルの保存と検査は完了。旧1.0.1は保持。
- [ ] 利用者の最終判断でリポジトリをPublicに変更。それまでは公開しない。

## 実機・署名に関する制限

利用者の希望により、今回のmacOS/Linux検証は成功済みのOS別自動テストを採用します。下記は未確認の制限の記録であり、今回の非公開配布準備を実機確認待ちで停止する理由にはしません。

- [ ] 別のWindows PCでの起動・Hello成功／キャンセル、ARM64実機の確認。
- [ ] macOS Apple Silicon/Intelでの透過・最前面・PIN解除と署名・公証。
- [ ] Linux x64/ARM64での透過・最前面・PIN解除。X11/XWaylandを想定。

未検証OSはプレビューと明記する。署名や組織の配布条件を満たさない環境について、動作確認済み・安全保証と表示しない。

技術的な確認範囲は[SECURITY-REVIEW-JA.md](SECURITY-REVIEW-JA.md)、実機検証項目は[TESTING.md](TESTING.md)を参照してください。
