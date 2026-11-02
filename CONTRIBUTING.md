# 改善への参加方法

使ってみた感想、質問、改善案は[Discussions](https://github.com/Guihakka2/ai-guard-lock/discussions)へ。不具合は[報告フォーム](https://github.com/Guihakka2/ai-guard-lock/issues/new?template=bug_report.md)から、OS・CPU・版番号・再現手順を添えてください。脆弱性の詳細は公開Issueへ書かず、[SECURITY.md](SECURITY.md)の方法で連絡してください。

## コードを変更する場合

1. 大きな機能追加や依存関係の変更は、まずIssueやDiscussionで目的を相談してください。
2. 作業用ブランチを作り、目的に必要な変更をまとめます。
3. .NET 10 SDKでビルドし、変更箇所に関係するテストを実行します。

```powershell
# Windows版（Windows上で実行）
dotnet build AiGuardLock.slnx -c Release
dotnet run --project tests/AiGuardLock.Tests -c Release

# 共通アプリ（Windows / macOS / Linux）
dotnet build AiGuardLock.CrossPlatform.slnx -c Release
dotnet run --project tests/AiGuardLock.CrossPlatform.Tests -c Release
```

専用ランナーのため`dotnet test`では実行しません。実ウィンドウのテストは画面を一時的に覆うので、操作中の作業に注意してください。[検証記録](docs/TESTING.md)も参照してください。

PRには変更の目的、変更後の動作、実行した確認、未確認の環境を記載してください。秘密情報、個人パス、ビルド出力、SDK、配布ZIPをコミットしないでください。スクリーンショットも公開可能なテスト用データに限ってください。

OSのロック・セキュリティ境界と誤解させる説明は追加しないでください。ユーザーの電源管理や認証ポリシーを無断で変更する機能も避けてください。

本プロジェクトのコードは[MIT License](LICENSE)です。新しい外部コード・画像・依存を追加する場合は、その出所とライセンスを明記し、必要な告知を残してください。対応時間やマージは保証できません。[行動規範](CODE_OF_CONDUCT.md)を守って参加してください。
