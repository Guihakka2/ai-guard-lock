# English launch copy

1.1.0 is published: https://github.com/Guihakka2/ai-guard-lock/releases/tag/v1.1.0-preview . These are drafts, not posts sent to an external account. The English demo is `docs/media/demo-en.gif` and is available from main.

## Short introduction

**Step away. Keep your work in sight.**

I built AI Guard Lock: a translucent desktop cover for long-running coding, builds, and background AI work. Verify with Windows Hello or a temporary app PIN when you return. English and Japanese UI, Windows app, macOS/Linux previews, MIT licensed.

It's a convenience cover, not an OS lock. The background remains visible, task switching can bypass it, and same-desktop GUI agents are not supported.

Source, demo, and downloads: https://github.com/Guihakka2/ai-guard-lock

## Short post

Long-running build? Background coding agent?

AI Guard Lock puts a translucent cover over your primary display. Return with Windows Hello or an app PIN. English + Japanese UI. MIT licensed.

For real access protection, use your OS lock. This cover doesn't replace it.

https://github.com/Guihakka2/ai-guard-lock

## Technical article draft

### I built a translucent desktop cover for background coding work

Sometimes I want a desktop cover while a build, command, or coding task runs in the background. I still want to see the work, and I want an explicit verification step when I return.

That's the narrow problem AI Guard Lock addresses. It covers the primary display with a translucent window. On Windows, you can dismiss it with Windows Hello or a temporary app PIN. The app now has English and Japanese UI, chosen from the display language, with an explicit startup option for either language.

The important limit: this is a convenience cover, not an operating-system lock or an access-control boundary. It leaves the background visible. Task switching, other displays, and process termination can bypass it. If you need real access protection or are leaving sensitive information on screen, use the OS lock.

### What the demo shows

The eight-second looping demo shows the actual Windows cover appearing and being dismissed with a test app PIN. Progress continues behind it, but that progress is synthetic. The demo does not claim compatibility with a particular AI agent or demonstrate real Windows Hello authentication. It contains no work desktop, notifications, or personal credentials.

### Small scope, explicit tradeoffs

The cover does not suspend background processes and does not change sleep, power, or automatic OS-lock policies. It is intended for command-line tasks, builds, code generation, and API requests. It does not establish that OS locking would stop those workloads. Compatibility with every AI tool is unverified, and a GUI agent trying to click or type on the same desktop will be blocked by the cover.

Windows Hello delegates verification to Windows. The app receives a result, not your Windows PIN or biometric data. Cancelled or failed verification keeps the cover in place. The separate app credential lasts only for the current session and is not written to disk. PIN attempts have a short retry delay, but a small PIN and a user-space window are not defenses against someone who controls the process.

### English UI without changing verification

The Windows app uses WPF, and the macOS/Linux preview uses Avalonia. Both share a small language selector and the existing credential/session code. Japanese display-language systems use Japanese; other display languages use English. `--language=en` and `--language=ja` choose explicitly. System Windows Hello dialogs remain in Windows' own language.

Language tests cover both UI paths and invalid language options. The existing authentication, retry, cleanup, and window-lifecycle checks still run. Hosted Windows, macOS, and Linux checks cover build and headless behavior; they do not prove transparency, focus, physical input, or real biometric verification on every machine.

### Try it and tell me what breaks

The source and demo are on GitHub. Published packages are on the Releases page, with .NET included, SHA-256 sidecars, and licenses. Windows x64/ARM64 builds are provided alongside macOS/Linux previews. Binaries are unsigned, and macOS bundles are not notarized. Follow your organization's application policy.

Questions and ideas go to Discussions; reproducible bugs go to Issues. Include your OS, CPU, app version, and reproduction steps. Please remove credentials, personal paths, and work content from reports.

Repository: https://github.com/Guihakka2/ai-guard-lock

## Possible discussion title

Show HN: AI Guard Lock — a translucent cover for background coding work

## What to measure after launch

Use actual reader questions, GitHub visits, release downloads, and reproducible bug reports to decide what to improve. Do not describe stars, downloads, security assurance, or agent compatibility that have not been measured. The copy should help people understand the product; it cannot guarantee virality.
