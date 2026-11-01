# Security policy

AI Guard Lock is a convenience screen cover for a signed-in desktop session. It is not an operating-system lock or an access-control boundary. The background remains visible. Task switching, other displays, process termination, privileged software, debuggers, and changes to the display configuration can bypass or dismiss it. Use the OS lock when actual access protection is required.

The application implements no network listener, remote-control endpoint, automatic updater, telemetry, or credential upload. Windows Hello is delegated to Windows; the application receives a verification result, not the Windows PIN or biometric data. The separate app PIN/password lasts for the current cover session and is not saved to disk. macOS/Linux use managed text strings, which cannot be reliably erased from process memory; do not reuse important passwords.

The minimum four-digit PIN and one-second retry delay provide limited resistance to guessing. The credential hash uses PBKDF2-SHA256 with a random salt and constant-time comparison. This does not protect against an attacker who can inspect or control the running process.

## Supported builds

Security fixes are provided through a new release of the latest version. Update the whole distribution: self-contained builds include their own .NET runtime, so a system .NET update alone does not update the bundled runtime. macOS/Linux previews are not yet validated on their target operating systems. Current binaries are unsigned; macOS bundles are not notarized.

## Reporting

Do not put secrets, actual PINs/passwords, personal data, or exploit details in a public issue. If the repository's Security tab offers private vulnerability reporting, use it. Otherwise open an issue requesting a private contact method without disclosing the vulnerability. No response-time or bounty commitment is offered.

## Maintaining dependencies and releases

- Restore with NuGet auditing enabled for direct and transitive dependencies. Audit errors are not suppressed.
- Run the existing credential and window-lifecycle tests before release.
- Scan source and reachable history for secrets; review synthetic test passwords separately from actual credentials.
- Scan release contents for personal paths, credentials, debug files, and unexpected files. Use a fresh publish build directory to avoid stale assemblies.
- Verify archive hashes and executable architecture; perform antivirus scanning of the distributions.
- Keep private build machines and self-hosted CI runners separate from untrusted contributions. Never execute a downloaded PR or workflow on a work PC merely because it was submitted to this repository.

An automated scan is evidence about the checks performed at that time, not proof that software or the whole PC is free of vulnerabilities or malware.
