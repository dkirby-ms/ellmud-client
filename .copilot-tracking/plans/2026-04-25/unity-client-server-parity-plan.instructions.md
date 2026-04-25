<!-- markdownlint-disable-file -->
---
description: Plan for aligning the Unity client transport layer with the current Ellmud server
applyTo: '**'
---

## User Requests

* Review the Unity-based thick client project
* Bring it up to par with the actual Ellmud server in the external repository

## Overview

This plan focuses on the smallest high-value slice that materially improves parity: the client/server protocol layer and room transition behavior. Full feature parity with the web client is out of scope for this iteration.

## Context Summary

* C# implementation guidance loaded from the HVE Core C# instructions
* Markdown and writing guidance loaded for tracking artifacts
* Server contract evidence recorded in .copilot-tracking/research/2026-04-25/unity-client-server-parity-research.md

## Dependencies

* No additional skills required for this slice
* Server reference repo: dkirby-ms/ellmud

## Implementation Checklist

### Phase 1
<!-- parallelizable: false -->

- [x] Align client message DTOs with the server's current shared protocol
- [x] Update room switching and zone transfer handling in the Unity network manager
- [x] Validate the touched scripts with focused diagnostics
- [x] Compile a review summary of remaining parity gaps

## Success Criteria

* Unity DTO field names match the current server payloads for the touched messages
* Room switching logic consumes roomId and targetRoomSlug correctly
* No new C# errors are introduced in touched files
* Remaining parity gaps are clearly documented for follow-up work