# 技術調査メモ

## Windows Helloによる解除

Windows 11のデスクトップ用API `Windows.Security.Credentials.UI.UserConsentVerifierInterop.RequestVerificationForWindowAsync` に、WPFカバーのHWNDを渡す。`Verified` の場合だけカバーを閉じる。Windowsロックやサインアウトは呼ばない。

設定画面で `UserConsentVerifier.CheckAvailabilityAsync` を確認する。事前チェックが利用不可の場合も、ユーザーが押すテストボタンから直接RequestVerificationForWindowAsyncを呼べる。テストのVerified結果でもHelloモードを有効にする。失敗・キャンセルだけで利用可能と扱うことはない。Helloモードにはアプリ独自の解除資格情報はなく、現在Windowsにサインインしているユーザーの本人確認をOSへ委ねる。キャンセル、例外、利用不可では閉じない。遅れて返ってきた結果は閉じたカバーへ適用しない。

参照: [Microsoft: UserConsentVerifier](https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifier)、[デスクトップ用API](https://learn.microsoft.com/en-us/windows/win32/api/userconsentverifierinterop/nf-userconsentverifierinterop-iuserconsentverifierinterop-requestverificationforwindowasync)。

## 現在の動作

通常起動はMainWindow。半透明カバーをPIN・パスワードで解除する。Windowsロック方式は撤去済み。初期設定は自動解除なし。通常のCloseとEscは解除しない。画面構成変更時は安全のため解除する。以下の入力方式・OS境界に関する調査は引き続き適用する。

調査日: 2026-11-01。Phase 1では入力フックや入力遮断は実装しない。

## オーバーレイとAIの継続

半透明表示でもウィンドウの入力領域は存在する。本MVPは通常の最前面WPFウィンドウで、表示領域へのクリックと、フォーカス中のキーボード入力を受け取る。透明度はクリック透過を意味しない。

この設計から、同じ画面座標を操作するAIもオーバーレイをクリックしてしまうと予想される。これは設計上の推論であり、特定エージェントとの実測結果ではない。スクリーンショットも暗い背景と解除UIを含む可能性があり、画面認識に影響する。

バックグラウンドのCLI・API・計算処理を停止する処理は実装していない。ただし、エージェント全体の継続、OSのスリープ、アプリのフォーカス依存、確認ダイアログの処理までは保証しない。スリープやWindowsのロック設定を自動変更しない。

UI AutomationやアプリのAPIは座標入力とは異なる経路を取り得る。入力方式ごとに別途試験が必要。MicrosoftもUI AutomationとSendInputを別の選択肢として説明している。[Microsoft: 入力のシミュレーション](https://devblogs.microsoft.com/oldnewthing/20250319-00/?p=110979)

## 物理入力と注入入力の区別

| 観測できる情報 | 限界 |
| --- | --- |
| キーボードの `LLKHF_INJECTED` | 注入の印。特定のAIの認証情報ではない |
| マウスの `LLMHF_INJECTED` | 注入の印。ユーザーが許可したプロセスかは判別できない |
| `*_LOWER_IL_INJECTED` | より低い整合性レベルからの注入かを示す。プロセスの信頼性を示さない |
| `dwExtraInfo` | 追加情報のフィールド。認証トークンとして信頼する設計にはしない |

フラグの意味はMicrosoftの [KBDLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct) と [MSLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct) に基づく。

これらの構造体には「許可されたAI」の証明はない。したがって「注入は許可・それ以外は遮断」だけでは安全なアクセス制御にならない。フラグがないことも、人間本人の物理入力である証明として扱わない。リモート入力、仮想デバイス、アクセシビリティ機能などは入力経路ごとの実測が必要。

## パスワードとの競合

物理キーボードを遮断すれば、本人の解除パスワードも届かなくなる。パスワードUIだけ例外にする設計でも、フォーカス切り替えやAIの入力が同じ欄に混入する問題は残る。Phase 1では通常のPasswordBoxを使い、注入入力・物理入力のどちらも特別扱いしない。

## 権限とセキュアデスクトップ

`SendInput` はUIPIの制限を受け、同等または低い整合性レベルのアプリに対してのみ注入できる。失敗時にUIPIが理由であると戻り値から特定できるわけではない。オーバーレイを管理者権限で起動することは互換性を改善する保証にならない。[Microsoft: SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)

デスクトップを跨いでウィンドウメッセージやフックが自由に使えるわけではない。Ctrl+Alt+DelやUACのセキュアデスクトップは、本MVPの通常デスクトップとは別の仕組み。これらを覆う・回避する機能は実装しない。[Microsoft: Desktops](https://learn.microsoft.com/en-us/windows/win32/winstation/desktops)

マニフェストは `asInvoker` / `uiAccess=false`。管理者権限、サービス、ドライバー、キーボードフック、`BlockInput`、`LockWorkStation`、自動起動登録、タスクマネージャー妨害、フォーカスの継続的な奪回は使わない。

## セキュリティ上の位置付け

このMVPのパスワードは通常の解除操作の確認用。EscとAlt+F4による通常終了は拒否するが、タスクマネージャー、他の画面・仮想デスクトップへの移動等は封じない。他の最前面ウィンドウに覆われる場合もある。背景の情報は読める。悪意ある第三者へのアクセス制御として評価しない。

## 表示の判断

主画面はWPFの `SystemParameters.PrimaryScreenWidth/Height` を使用し、左上を `(0, 0)` に置く。Phase 1はSystem DPI awareで、表示構成の変更時には覆い直すのではなく解除する。混在DPI・マルチモニター・リモート接続の実機検証は今後行う。

背景ぼかしは見送り。WPFのBlurEffectをオーバーレイへ付けても背後の別アプリそのものをぼかすことにはならない。透過の安定性と復旧経路を先に検証し、Windowsの背景効果はPhase 4で別途調査する。
