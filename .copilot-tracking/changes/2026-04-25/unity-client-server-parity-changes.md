<!-- markdownlint-disable-file -->
---
title: Unity Client Server Parity Changes
description: Transport-layer parity changes applied to the Unity client
author: GitHub Copilot
ms.date: 2026-04-25
ms.topic: reference
---

## Related Plan

* .copilot-tracking/plans/2026-04-25/unity-client-server-parity-plan.instructions.md

## Summary

Implemented the first high-value parity slice by updating Unity transport DTOs and room transition behavior to match the current Ellmud server contract.

Implemented the second parity slice by adding runtime bootstrap wiring, reconnect and zone-state handling, a smoke harness, and broader HUD/event rendering.

## Modified Files

* Assets/Scripts/Messages/ClientMessage.cs
* Assets/Scripts/Messages/ServerMessages.cs
* Assets/Scripts/Network/GameEvents.cs
* Assets/Scripts/Network/EllmudNetworkManager.cs
* Assets/Scripts/UI/GameHUDController.cs
* Assets/Scripts/UI/ClientRuntimeBootstrap.cs
* Assets/Scripts/UI/ClientSmokeHarness.cs
* Assets/Resources/UI/GameHUD.uxml
* Assets/Resources/UI/EllmudTheme.uss
* Assets/Resources/UI/PanelSettings.asset

## Changes By Category

### Modified

* Client message payloads now use targetSlot and startingZoneSlug, and include toggle-flag support
* Server message payloads now reflect current room switch, zone transfer, inventory, loadout, exploration, help, flag, combat, and who-list shapes
* Network manager room transitions now target zone-prefixed refuge rooms, pass through roomId and targetRoomSlug, and join zone transfers via zone:{slug}
* COMBAT_RESULT is now registered and surfaced through GameEvents
* Network manager now tracks last successful join context and supports reconnect attempts via `ReconnectToLastRoom`
* Zone lifecycle payloads are now represented in Unity DTOs and surfaced via `GameEvents.OnZoneState`
* Added `GameHUDController` support for combat state panel rendering, occupants summary, loadout summary, help overlay, and who-list overlay
* Added `ClientRuntimeBootstrap` to auto-provision Auth, Network, GameState, and HUD objects in fresh scenes
* Added `ClientSmokeHarness` context-menu and optional startup smoke checks for login, enter world, command send, disconnect, and reconnect
* Added runtime-loadable HUD assets under `Assets/Resources/UI` for auto bootstrap

## Validation

* Workspace diagnostics for the touched C# files reported no errors after the changes

* Workspace diagnostics for the continuation slice reported no errors in:
	* Assets/Scripts/Messages/ServerMessages.cs
	* Assets/Scripts/Network/GameEvents.cs
	* Assets/Scripts/Network/EllmudNetworkManager.cs
	* Assets/Scripts/UI/GameHUDController.cs
	* Assets/Scripts/UI/ClientRuntimeBootstrap.cs
	* Assets/Scripts/UI/ClientSmokeHarness.cs

## Release Summary

The Unity client is materially closer to the live server at the wire-contract layer, but it still needs follow-up work in REST-based character flow and higher-level gameplay UI/state parity.

The Unity client now has an executable bootstrap and smoke-test path suitable for in-editor playable validation, with expanded HUD/event handling for major gameplay surfaces.