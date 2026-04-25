<!-- markdownlint-disable-file -->
---
title: Unity Client Server Parity Research
description: Verified protocol and routing differences between the Unity client and the current Ellmud server
author: GitHub Copilot
ms.date: 2026-04-25
ms.topic: reference
---

## Scope

Review the Unity thick client against the current Ellmud server implementation in dkirby-ms/ellmud and identify the highest-impact compatibility gaps.

## Success Criteria

* Align the Unity client's current Colyseus message contracts with the server's shared protocol where feasible
* Fix the room switching path so it honors the server's current room target and option fields
* Produce a concise review of remaining parity gaps that require larger product work

## Evidence

* The Unity client contains seven C# source files under Assets/Scripts, centered on auth, message DTOs, state, and a Colyseus network manager
* The server's canonical message protocol now lives in packages/shared/src/index.ts and diverges from several Unity DTOs
* Verified protocol drift includes:
  * Equip and swap messages use targetSlot on the server, not slot or fromSlot/toSlot
  * RoomSwitchOptions now uses roomId and targetRoomSlug, not entryRoomId
  * ZoneTransferMessage uses targetZoneSlug and targetRoomSlug
  * FlagStateMessage sends a flags map, not a single flagName/value pair
  * PlayerListMessage returns who-list entries, not in-room player info
  * RoomOccupantsMessage now uses aggressive and disconnected flags
  * Exploration messages include currentRoomId and exits as a string map
  * HelpDataMessage sends commands and optional focusCommand
  * InventoryUpdateMessage uses currentWeight and maxWeight
* The server now routes hubs as zone-prefixed targets such as zone:the-refuge, while procedural rooms still use the bare zone room name
* Character creation on the server is REST-based with name and startingZoneSlug, not the older websocket archetype payload

## Selected Approach

Prioritize protocol compatibility fixes in the Unity transport layer instead of attempting feature parity with the web client. This addresses the root cause for immediate runtime incompatibilities and keeps the change set local to the thin client infrastructure.

## Next Steps

* Update the Unity DTOs to match the verified server payloads
* Update the Unity network manager to use roomId and targetRoomSlug during room changes
* Validate touched C# files for compile or analyzer issues
* Report remaining gaps outside the transport layer as follow-up work