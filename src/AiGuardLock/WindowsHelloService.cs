using AiGuardLock.Core;
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
            UiText.Get("AI Guard Lock の本人確認を行います。", "Verify your identity for AI Guard Lock."));
}

internal static class WindowsHelloMessages
{
    public static string Availability(UserConsentVerifierAvailability result) => result switch
    {
        UserConsentVerifierAvailability.Available => UiText.Get("Windows Helloを利用できます。登録済みの指紋・顔・Windows PINなどで解除します。", "Windows Hello is available. Unlock with your registered fingerprint, face or Windows PIN."),
        UserConsentVerifierAvailability.NotConfiguredForUser => UiText.Get("このWindowsユーザーにはHelloが設定されていません。アプリ専用PIN・パスワードを利用できます。", "Windows Hello is not configured for this Windows user. You can use an app-only PIN or password."),
        UserConsentVerifierAvailability.DisabledByPolicy => UiText.Get("Windows Helloは組織のポリシーで無効です。アプリ専用PIN・パスワードを利用できます。", "Your organization has disabled Windows Hello. You can use an app-only PIN or password."),
        UserConsentVerifierAvailability.DeviceBusy => UiText.Get("認証機能が使用中です。再確認するか、アプリ専用PIN・パスワードを選んでください。", "Windows verification is busy. Check again or choose an app-only PIN or password."),
        UserConsentVerifierAvailability.DeviceNotPresent => UiText.Get("事前チェックでは認証機能を検出できませんでした。普段WindowsのPINを使っている場合は、下のボタンから本人確認を試せます。", "The availability check did not detect Windows verification. If you normally use a Windows PIN, try the verification button below."),
        _ => UiText.Get("事前チェックで利用可否を確認できませんでした。下のボタンから本人確認を試せます。", "The availability check was inconclusive. You can try the verification button below.")
    };

    public static string Probe(UserConsentVerificationResult result) => result switch
    {
        UserConsentVerificationResult.Canceled => UiText.Get("本人確認をキャンセルしました。カバーは開始していません。もう一度試せます。", "Verification was canceled. The cover has not started. You can try again."),
        UserConsentVerificationResult.DeviceNotPresent => UiText.Get("Windowsから認証機能を利用できないという結果が返りました（DeviceNotPresent）。このアプリからのWindows PIN認証はまだ確認できていません。", "Windows reported that verification is unavailable (DeviceNotPresent). Windows PIN verification from this app has not been confirmed."),
        UserConsentVerificationResult.NotConfiguredForUser => UiText.Get("Windowsから、このユーザーのHelloが未設定という結果が返りました。カバーは開始していません。", "Windows reported that Hello is not configured for this user. The cover has not started."),
        UserConsentVerificationResult.DisabledByPolicy => UiText.Get("組織のポリシーで本人確認を利用できません。カバーは開始していません。", "Your organization has disabled verification. The cover has not started."),
        UserConsentVerificationResult.DeviceBusy => UiText.Get("Windowsの認証機能が使用中です。少し待って再試行してください。", "Windows verification is busy. Wait a moment and try again."),
        UserConsentVerificationResult.RetriesExhausted => UiText.Get("Windowsの本人確認の試行回数を超えました。しばらく待って再試行してください。", "Too many Windows verification attempts. Wait a while and try again."),
        _ => UiText.Get("Windowsで本人確認できませんでした。カバーは開始していません。", "Windows could not verify your identity. The cover has not started.")
    };

    public static string Verification(UserConsentVerificationResult result) => result switch
    {
        UserConsentVerificationResult.Canceled => UiText.Get("本人確認をキャンセルしました。カバーは解除していません。", "Verification was canceled. The cover remains active."),
        UserConsentVerificationResult.RetriesExhausted => UiText.Get("本人確認の試行回数を超えました。しばらく待ってから再試行してください。", "Too many verification attempts. Wait a while and try again."),
        UserConsentVerificationResult.DeviceBusy => UiText.Get("認証機能が使用中です。しばらく待ってから再試行してください。", "Windows verification is busy. Wait a while and try again."),
        UserConsentVerificationResult.NotConfiguredForUser => UiText.Get("Windows Helloが設定されていません。カバーは解除していません。", "Windows Hello is not configured. The cover remains active."),
        UserConsentVerificationResult.DisabledByPolicy => UiText.Get("Windows Helloは組織のポリシーで無効です。カバーは解除していません。", "Your organization has disabled Windows Hello. The cover remains active."),
        _ => UiText.Get("Windows Helloで確認できませんでした。カバーは解除していません。", "Windows Hello could not verify your identity. The cover remains active.")
    };
}
