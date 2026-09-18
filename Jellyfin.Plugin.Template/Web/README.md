# Watch Wheel: saved filters and recent picks

## Features

- Media type, genre, decade, watch status, and Avoid recent picks are saved automatically when changed.
- Settings are restored after available filter options load. Unavailable saved genres/decades fall back to Any.
- The last 10 completed spins appear newest first, including pick time, title, selected episode information, and Details buttons.
- Avoid recent picks defaults off. When enabled, any movie or whole series appearing in those 10 entries is excluded from the next spin.
- Clear History clears recent entries and their exclusions without resetting filters or marking anything watched.
- Changes to watch filters still require Refresh Wheel; changing Avoid recent picks takes effect immediately.
- Manual Remove from Wheel exclusions persist until Refresh Wheel. Clearing history does not clear these manual removals.
- The badge counts choices available for the next spin. The completed wheel retains the winning segment until the next spin or pool refresh.
- If every matching title is recent, the spin buttons disable and the message explains how to restore choices.

## Storage scope

Settings and history use localStorage keyed by Jellyfin server ID and user ID. They are specific to this browser profile, not synced between devices, and can disappear when browser data is cleared or a private session ends. No authentication tokens are stored. If storage is blocked or full, the wheel keeps working for the visit and displays a notice. Recent picks record spins, not playback completion or watched status.

History Details buttons navigate to Jellyfin's authenticated details pages. They do not replay stale episode progress from history. Use the main winner card for playback. Concurrent tabs do not synchronize their history live; use one Watch Wheel tab for consistent history.

## Install

Back up or commit the working project, then replace all three files together:

    Jellyfin.Plugin.Template/Web/watchWheel.html
    Jellyfin.Plugin.Template/Web/watchWheel.js
    Jellyfin.Plugin.Template/Web/watchWheel.css

No backend edits are required. This bundle builds on the Watch Status filter and confirmed session playback version.

Build:

    dotnet build .\Jellyfin.Plugin.Template\Jellyfin.Plugin.Template.csproj -c Release --no-incremental

Verify the release marker in PowerShell:

    $dll = (Resolve-Path .\Jellyfin.Plugin.Template\bin\Release\net9.0\Jellyfin.Plugin.WatchWheel.dll).Path
    [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($dll)).Contains('history-5')

After a successful build and True result, upload that output DLL, replace the installed copy using the current Jellyfin process path, restart Jellyfin, and refresh the browser page.

## Check in Jellyfin

1. Choose filters, refresh, and reload: the selections should return.
2. Spin twice: both completed picks should appear in Recent picks below the winner card.
3. Enable Avoid recent picks: those titles should be excluded from later spins, even after a reload.
4. Clear History: recent exclusions should be removed, while filters remain.
5. For TV, confirm the card still plays the selected episode and History has separate series/episode Details buttons.

Validation: JavaScript syntax and mock integration tests passed for persistence, user/server isolation, exclusions, empty-pool recovery, manual removals, 10-entry cap, blocked/corrupt storage, series identity, and canceled animations. Full .NET compilation and live Jellyfin rendering/playback were not available in this environment.
