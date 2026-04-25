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

## Modified Files

* Assets/Scripts/Messages/ClientMessage.cs
* Assets/Scripts/Messages/ServerMessages.cs
* Assets/Scripts/Network/GameEvents.cs
* Assets/Scripts/Network/EllmudNetworkManager.cs

## Changes By Category

### Modified

* Client message payloads now use targetSlot and startingZoneSlug, and include toggle-flag support
* Server message payloads now reflect current room switch, zone transfer, inventory, loadout, exploration, help, flag, combat, and who-list shapes
* Network manager room transitions now target zone-prefixed refuge rooms, pass through roomId and targetRoomSlug, and join zone transfers via zone:{slug}
* COMBAT_RESULT is now registered and surfaced through GameEvents

## Validation

* Workspace diagnostics for the touched C# files reported no errors after the changes

## Release Summary

The Unity client is materially closer to the live server at the wire-contract layer, but it still needs follow-up work in REST-based character flow and higher-level gameplay UI/state parity.