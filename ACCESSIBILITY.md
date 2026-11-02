# アクセシビリティについて

AI Guard LockはWindows WPF版とmacOS/Linux用のAvaloniaプレビューを提供します。認証欄の名前、入力後のEnter操作、エラー表示などを用意していますが、スクリーンリーダー・拡大表示・ハイコントラスト・すべての入力機器との互換性を実機で網羅したものではありません。適合規格の認証を受けたという意味でもありません。

## 分かっている制限

- 主画面のみを覆います。表示倍率や画面構成を変えた際の読みやすさは環境に依存します。
- 背景は透けたままで、文字の読みやすさや刺激の少なさを保証できません。
- 認証方法はWindowsとPCの構成に依存します。Mac/Linuxはアプリ専用PIN・パスワードのみです。
- カバー中は背後のアプリへの入力が遮られます。Escや通常の終了操作では解除しません。
- 復旧が必要な場合、WindowsではCtrl+Alt+Delからタスクマネージャーで終了できます。OSごとの復旧方法は同梱ガイドを参照してください。

利用しづらい点は[不具合報告](https://github.com/Guihakka2/ai-guard-lock/issues/new?template=bug_report.md)で、OS・表示倍率・利用した支援技術・困った操作を教えてください。個人情報やPINは書かないでください。改善案や相談は[Discussions](https://github.com/Guihakka2/ai-guard-lock/discussions)でも受け付けます。修正時期は保証できません。
