# English Edition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** English-facing README, launch copy, demo, and English/Japanese UI for the Windows and cross-platform apps.

**Architecture:** A small shared `AiGuardLock.Core.UiText` selects Japanese when `CurrentUICulture` starts with ja, otherwise English. `UiText.Get(ja, en)` supplies inline paired translations without changing credential or session logic. `UiText.Configure(args)` accepts `--language=en` or `--language=ja` for explicit startup selection; invalid or conflicting language arguments fail without starting a cover. WPF uses a two-string markup extension for static text and the same helper for dynamic status. Avalonia uses the helper for control text and status.

**Tech Stack:** .NET 10, WPF, Avalonia 12.1.4, existing custom test runners; no localization dependency.

**Scope:** Translate labels, accessibility names, errors, countdowns and Hello status. Preserve Japanese behavior, existing protections and recovery paths. Default GitHub README becomes English, with README.ja.md retained. Prepare English launch copy; do not post to a new external service without a selected destination/account. Do not promise virality or OS-level access protection.

## Tasks

- [x] Shared language selection: write failing culture/override/invalid argument checks; add helper; run checks.
- [x] Windows UI: failing English setup/overlay/Hello assertions; localize static and dynamic strings, startup; Japanese and English runs.
- [x] Cross-platform UI: failing English setup/cover/status assertions; localize startup and controls; Japanese/English headless + native smoke.
- [x] English README, Japanese language link, English guides/launch text/demo; verify links and actual visuals.
- [x] Review, run OS-hosted checks, create self-contained bilingual distribution with project license included, check package hashes/personal paths/architecture/Defender.
- [x] Store draft and request action-time approval before publishing the new release; existing1.0.2 stays available.

## Verification

Explicit English/Japanese culture tests and argument handling; existing authentication/session checks; no real biometric or user PIN automation. A translated setup/cover render must show readable English without leaking the work desktop. English demo uses synthetic progress and a test PIN, and documents that limit. GIF must have24 frames,33cs delays and infinite loop. Documentation must distinguish unsigned/unnotarized previews and unverified physical GUI behavior. Source and final artifacts must exclude local identities/secrets.
