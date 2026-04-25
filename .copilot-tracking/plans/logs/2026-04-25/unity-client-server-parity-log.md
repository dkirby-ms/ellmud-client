<!-- markdownlint-disable-file -->
---
title: Unity Client Server Parity Planning Log
description: Planning decisions and deviations for Unity client parity work
author: GitHub Copilot
ms.date: 2026-04-25
ms.topic: reference
---

## Selected Path

Focus on protocol-layer parity first because the current client is infrastructure-heavy and UI-light. This gives the best chance of making the project function against the live server without rebuilding the web client.

## Alternatives Considered

* Rebuild the Unity client toward full web-client feature parity now
  * Rejected because the current repository lacks equivalent UI, state slices, and test coverage
* Patch only room switching
  * Rejected because several DTO mismatches would still break basic server messaging

## Known Gaps To Revisit

* Character management in the Unity client is still websocket-oriented while the server now uses REST for that flow
* The Unity project still lacks the broader gameplay UI and state orchestration present in the web client

## Implementation Notes

* Completed the first parity slice in the Unity transport layer:
  * Updated client and server DTOs in Assets/Scripts/Messages to match the current shared contract for room switching, equipment, flags, help, who list, exploration, effective stats, and combat snapshots
  * Updated EllmudNetworkManager to join zone-prefixed hub rooms, honor roomId and targetRoomSlug, and route zone transfers to zone:{slug}
  * Registered COMBAT_RESULT so the Unity event surface now matches its declared GameEvents entry
* Focused validation via workspace diagnostics reported no errors in the touched files
* Continuation slice completed for all previously suggested follow-on work:
  * Added character-scoped join propagation through world-entry and spawn-target pathways
  * Added reconnect helper and zone-state event wiring to the network/event surface
  * Added `GameHUDController` support for combat state, occupants, loadout updates, help overlays, and who-list overlays
  * Added `ClientRuntimeBootstrap` to stand up required runtime objects in fresh scenes
  * Added `ClientSmokeHarness` for lightweight login → enter world → command → reconnect validation
  * Added runtime HUD assets under `Assets/Resources/UI` for bootstrap loading

## Deviations

* Character-management websocket message types were left in place for compatibility with the existing Unity codebase, but they remain secondary to the server's REST flow and should not be treated as authoritative parity
* Loadout and map payloads were aligned structurally at the DTO layer without expanding the current Unity UI, because there is no existing gameplay surface in this repository that consumes the richer data yet

## Suggested Follow-on Work

* Create a concrete Unity scene asset that references the bootstrap and HUD prefabs for out-of-the-box editor play mode
* Add protocol-contract assertions or generated DTO checks against `packages/shared` exports to catch drift automatically in CI
* Expand overlay UX for multi-section views (inventory/loadout/help/who) instead of terminal-only summaries