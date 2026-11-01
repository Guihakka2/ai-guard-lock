using Windows.Security.Credentials.UI;

namespace AiGuardLock;

internal interface IWindowsHelloService
{
    Task<UserConsentVerifierAvailability> CheckAvailabilityAsync();
    Task<UserConsentVerificationResult> VerifyAsync(nint ownerWindow);
}

internal sealed class WindowsHelloService : IWindowsHelloService
{
    public async Task<UserConsentVerifierAvailability> CheckAvailabilityAsync() =>
        await UserConsentVerifier.CheckAvailabilityAsync();

    public async Task<UserConsentVerificationResult> VerifyAsync(nint ownerWindow) =>
        await UserConsentVerifierInterop.RequestVerificationForWindowAsync(ownerWindow,
            "AI Guard Lock の本人確認を行います。");
}

internal static class WindowsHelloMessages
{
    public static string Availability(UserConsentVerifierAvailability result) => result switch
    {
        UserConsentVerifierAvailability.Available => "Windows Helloを利用できます。登録済みの指紋・顔・Windows PINなどで解除します。",
        UserConsentVerifierAvailability.NotConfiguredForUser => "このWindowsユーザーにはHelloが設定されていません。アプリ専用PIN・パスワードを利用できます。",
        UserConsentVerifierAvailability.DisabledByPolicy => "Windows Helloは組織のポリシーで無効です。アプリ専用PIN・パスワードを利用できます。",
        UserConsentVerifierAvailability.DeviceBusy => "認証機能が使用中です。再確認するか、アプリ専用PIN・パスワードを選んでください。",
        UserConsentVerifierAvailability.DeviceNotPresent => "事前チェックでは認証機能を検出できませんでした。普段WindowsのPINを使っている場合は、下のボタンから本人確認を試せます。",
        _ => "事前チェックで利用可否を確認できませんでした。下のボタンから本人確認を試せます。"
    };

    public static string Probe(UserConsentVerificationResult result) => result switch
    {
        UserConsentVerificationResult.Canceled => "本人確認をキャンセルしました。カバーは開始していません。もう一度試せます。",
        UserConsentVerificationResult.DeviceNotPresent => "Windowsから認証機能を利用できないという結果が返りました（DeviceNotPresent）。このアプリからのWindows PIN認証はまだ確認できていません。",
        UserConsentVerificationResult.NotConfiguredForUser => "Windowsから、このユーザーのHelloが未設定という結果が返りました。カバーは開始していません。",
        UserConsentVerificationResult.DisabledByPolicy => "組織のポリシーで本人確認を利用できません。カバーは開始していません。",
        UserConsentVerificationResult.DeviceBusy => "Windowsの認証機能が使用中です。少し待って再試行してください。",
        UserConsentVerificationResult.RetriesExhausted => "Windowsの本人確認の試行回数を超えました。しばらく待って再試行してください。",
        _ => "Windowsで本人確認できませんでした。カバーは開始していません。"
    };

    public static string Verification(UserConsentVerificationResult result) => result switch
    {
        UserConsentVerificationResult.Canceled => "本人確認をキャンセルしました。カバーは解除していません。",
        UserConsentVerificationResult.RetriesExhausted => "本人確認の試行回数を超えました。しばらく待ってから再試行してください。",
        UserConsentVerificationResult.DeviceBusy => "認証機能が使用中です。しばらく待ってから再試行してください。",
        UserConsentVerificationResult.NotConfiguredForUser => "Windows Helloが設定されていません。カバーは解除していません。",
        UserConsentVerificationResult.DisabledByPolicy => "Windows Helloは組織のポリシーで無効です。カバーは解除していません。",
        _ => "Windows Helloで確認できませんでした。カバーは解除していません。"
    };
}
