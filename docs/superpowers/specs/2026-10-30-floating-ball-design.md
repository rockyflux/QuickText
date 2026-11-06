# Floating ball design

## Goal

On every launch, show a small always-on-top floating ball. Drag to move (position remembered). Left-click toggles the search panel. Right-click uses the same app menu as the tray, plus hide; when hidden, the tray can show it again for this session.

## Behavior

- **Startup:** show the ball every launch (including after a previous session hide). Hide is session-only.
- **Left-click:** `ShowSearch(toggle: true)` — same as the summon hotkey.
- **Drag:** move the window; distinguish from click with a small move threshold; on release, clamp to the work area of the monitor under the ball and persist `FloatBallX` / `FloatBallY`.
- **Right-click (ball):** same items as the tray (Open Search, Open Manager, New from clipboard, Settings, Pause/Resume, Exit), plus **Hide floating ball**.
- **Tray:** when the ball is hidden this session, show **Show floating ball**; when visible, that item is collapsed.
- **Default position:** if both saved coords are `0`, place near the bottom-right of the primary work area (with margin).

## Approach

Small borderless WPF `Topmost` tool window (`FloatingBallWindow`). Do not call `SetWindowPos` on it. Skip DWM cloak/`UseDarkChrome` so the ball stays visible without a title bar. Include it in `--smoke`. Persist position via existing `SettingsStore` / `AppSettings`.

## Out of scope

Settings toggle for “never show”, opacity/size options, multi-ball, custom artwork beyond the existing brand mark.
