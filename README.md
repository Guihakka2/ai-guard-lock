# AI Guard Lock

作業画面を半透明で覆い、本人確認で解除するデスクトップアプリです。OSのロック画面へは切り替えません。

![AI Guard Lock](src/AiGuardLock/Assets/AppIcon-preview.png)

| 版 | 解除方法 | 検証状況 |
| --- | --- | --- |
| Windows 11 x64 | Windows Hello / アプリ専用PIN・パスワード | この開発PCで起動・自動検証済み |
| Windows 11 ARM64 | 同上 | ビルド済み、実機未検証 |
| macOS Apple Silicon / Intel | アプリ専用PIN・パスワード | プレビュー、実機未検証・未署名・未公証 |
| Linux x64 / ARM64 | アプリ専用PIN・パスワード | プレビュー、実機未検証 |

配布ファイルはこのリポジトリの **Releases** に配置します。非公開リポジトリのため閲覧にはアクセス権が必要です。

公開準備の進捗と未確認事項は[公開前チェックリスト](docs/PUBLICATION-CHECKLIST-JA.md)に記録しています。1.0.1は公開前確認用の下書きです。旧1.0.0にはビルドPCの個人パスが含まれるため、公開用の配布物として使用しないでください。

## 別のPCへ持っていく

`dist/AI-Guard-Lock-1.0.1-win-x64.zip` をコピーして、移動先で「すべて展開」→ `AiGuardLock.exe` を起動します。Intel/AMDのWindows 11 PC向けです。Snapdragon等のARM PCには `dist/AI-Guard-Lock-1.0.1-win-arm64.zip` を使用してください。

.NET同梱の単一EXEなので、移動先へSDK・.NETを別途インストールする必要はありません。元のPCの.lnkだけをコピーする方法では動きません。移動先のデスクトップアイコンは、同梱の `Create-Desktop-Shortcut.cmd` またはEXEの右クリックメニューから作成できます。

Windows Helloは移動先のPC・ユーザーのPINや生体認証を使います。認証情報は配布物に含めません。利用前に、そのPCで本人確認テストを行ってください。配布版は未署名です。会社の端末で実行が制限される場合は管理者へ確認してください。

配布手順の全文は [PORTABLE-JA.txt](docs/PORTABLE-JA.txt)、検証範囲は [RELEASE.md](docs/RELEASE.md) を参照してください。

## アイコン

青緑のシールドと鍵穴のデザインを、EXE・ウィンドウ・ショートカットに組み込んでいます。`src/AiGuardLock/Assets/AppIcon.ico` は16/24/32/48/64/128/256pxを収録。編集用SVGと512px PNGも同じフォルダーにあります。

アイコンは `scripts/create-icon.ps1` から再生成できます。配布ZIPの再生成は `scripts/publish.ps1 -Runtime win-x64` または `-Runtime win-arm64`。既存配布物を誤って上書きしないよう、同じバージョンの出力がある場合は停止します。

## 使い方

1. 配布ZIPを展開し、`AiGuardLock.exe` を開きます。
2. 「WindowsのPIN・指紋で本人確認を試す」を押し、表示されたWindowsの画面で認証します。事前チェックが利用不可でも試せます。成功すると「Windows Helloで解除」が選択されます。アプリ専用PINを使う場合はそちらを選び、数字PIN（半角4〜12桁）またはパスワード（8〜128文字）を2回入力します。
3. 注意事項の確認にチェックを入れ「半透明カバーを開始」を押します。
4. 戻ったら「Windows Helloで解除」を押し、Windowsの本人確認ダイアログで認証します。アプリ専用PINを選んだ場合は、同じPIN・パスワードを入力して「解除」またはEnterを押します。

Windows Helloは、現在Windowsにサインインしているユーザーの登録済みの指紋・顔・Windows PINなどを利用します。利用できる方法はPCの構成と組織のポリシーによってWindowsが提示します。アプリは生体情報やWindowsのPINを受け取らず、確認結果だけを受け取ります。キャンセル・失敗では解除しません。

アプリ専用PINはWindows PINとは別です。今回のカバー表示中だけ有効で、ファイルには保存しません。Helloモードではアプリ専用PINの設定は不要です。

Windows Helloが利用できない場合は、そのPCのサインイン設定を確認してください。テストボタンはWindowsへ直接本人確認を要求し、成功結果が返った場合だけHelloモードを有効にします。テストではカバーを開始しません。失敗時は結果を設定画面に表示します。

初期設定は自動解除なし。任意で2分・10分・60分の自動解除も選べます。カバー中のEsc、Alt+F4、通常のウィンドウ終了要求では解除しません。画面構成が変わった場合は安全のため解除します。

パスワードを忘れた場合や異常時は、Ctrl+Alt+Del → タスクマネージャーでAI Guard Lockを終了できます。スクリプトから起動した場合は、コマンドラインにAiGuardLock.dllが含まれるdotnet.exeだけを対象にしてください。

## 対応範囲

- 主画面だけを最前面の半透明ウィンドウで覆います。背景の処理を停止する処理はありません。
- バックグラウンドのコード編集・コマンド・API処理を想定しています。各AIでの継続動作は未検証です。
- 同じ画面へのAIのクリック・キー入力もカバーに遮られるため、GUI操作を続ける用途には対応しません。
- 他のモニター、画面切替、仮想デスクトップ、タスクマネージャーを封じません。Windows認証と同等のアクセス制御ではありません。
- スリープやWindowsの自動ロック、電源管理ポリシーは変更しません。

## ビルド・テスト

Windows 11 / .NET 10 SDK / WPF。Windows SDKの.NET参照パッケージをnuget.orgから復元します。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 Build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 Run
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 Test
```

ExecutionPolicyの指定は起動したPowerShellプロセスだけに適用します。組織のポリシーで実行できない場合は、インストール済みSDKのdotnet restore/buildでビルドしてください。

実行ファイルは `src/AiGuardLock/bin/Release/net10.0-windows10.0.22000.0/AiGuardLock.exe`。同じディレクトリのDLL・設定ファイルも必要です。ショートカットを再作成するには、ビルド後に `scripts/create-shortcut.ps1` を実行します。

専用テストランナーで60チェック成功。`dotnet test`は使用しません。詳細は [検証記録](docs/TESTING.md)、設計上の制限は [技術メモ](docs/TECHNICAL-NOTES.md) を参照してください。

SDK・ビルド出力・ショートカットは管理対象外です。

## macOS・Linuxプレビュー

Avaloniaの別アプリで、アプリ専用PINによる主画面カバーを実装しています。Touch ID・OSログイン認証は未対応です。詳細と起動方法は [CROSS-PLATFORM-JA.txt](docs/CROSS-PLATFORM-JA.txt) を参照してください。

```sh
dotnet build AiGuardLock.CrossPlatform.slnx -c Release
dotnet run --project tests/AiGuardLock.CrossPlatform.Tests -c Release
dotnet run --project src/AiGuardLock.CrossPlatform -c Release
```

Windowsで4種類の配布物を生成するには、.NET 10 SDKとPython 3を用意し、`scripts/publish-cross.ps1` を実行します。MacでのDeveloper ID署名・公証は別途必要です。共通コードの19チェックとWindows上のネイティブウィンドウ4チェックを通過していますが、Mac・Linux上での動作を保証するものではありません。
