using System;
using System.Collections.Generic;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    // ── Current State ─────────────────────────────────────
    public RoomHeaderMessage CurrentRoom { get; private set; }
    public PlayerStateMessage PlayerState { get; private set; }
    public CombatStateMessage CombatState { get; private set; }
    public InventoryUpdateMessage Inventory { get; private set; }
    public StashUpdateMessage Stash { get; private set; }
    public LoadoutUpdateMessage Loadout { get; private set; }
    public RoomOccupantsMessage Occupants { get; private set; }
    public EffectiveStatsMessage EffectiveStats { get; private set; }
    public bool InCombat =>
        CombatState != null && CombatState.isParticipant;

    // Exploration map: roomId → data
    public Dictionary<string, ExploredRoomData> ExplorationMap
        { get; private set; }
        = new Dictionary<string, ExploredRoomData>();

    // ── UI-facing change events ───────────────────────────
    public event Action OnRoomChanged;
    public event Action OnPlayerStateChanged;
    public event Action OnCombatStateChanged;
    public event Action OnInventoryChanged;
    public event Action OnStashChanged;
    public event Action OnLoadoutChanged;
    public event Action OnOccupantsChanged;
    public event Action OnMapUpdated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        GameEvents.OnRoomHeader += HandleRoomHeader;
        GameEvents.OnPlayerState += HandlePlayerState;
        GameEvents.OnCombatState += HandleCombatState;
        GameEvents.OnInventoryUpdate += HandleInventory;
        GameEvents.OnStashUpdate += HandleStash;
        GameEvents.OnLoadoutUpdate += HandleLoadout;
        GameEvents.OnRoomOccupants += HandleOccupants;
        GameEvents.OnEffectiveStats += HandleStats;
        GameEvents.OnExplorationData += HandleExplorationData;
        GameEvents.OnExplorationUpdate += HandleExplorationUpdate;
    }

    private void OnDisable()
    {
        GameEvents.OnRoomHeader -= HandleRoomHeader;
        GameEvents.OnPlayerState -= HandlePlayerState;
        GameEvents.OnCombatState -= HandleCombatState;
        GameEvents.OnInventoryUpdate -= HandleInventory;
        GameEvents.OnStashUpdate -= HandleStash;
        GameEvents.OnLoadoutUpdate -= HandleLoadout;
        GameEvents.OnRoomOccupants -= HandleOccupants;
        GameEvents.OnEffectiveStats -= HandleStats;
        GameEvents.OnExplorationData -= HandleExplorationData;
        GameEvents.OnExplorationUpdate -= HandleExplorationUpdate;
    }

    private void HandleRoomHeader(RoomHeaderMessage msg)
    {
        CurrentRoom = msg;
        OnRoomChanged?.Invoke();
    }

    private void HandlePlayerState(PlayerStateMessage msg)
    {
        PlayerState = msg;
        OnPlayerStateChanged?.Invoke();
    }

    private void HandleCombatState(CombatStateMessage msg)
    {
        CombatState = msg;
        OnCombatStateChanged?.Invoke();
    }

    private void HandleInventory(InventoryUpdateMessage msg)
    {
        Inventory = msg;
        OnInventoryChanged?.Invoke();
    }

    private void HandleStash(StashUpdateMessage msg)
    {
        Stash = msg;
        OnStashChanged?.Invoke();
    }

    private void HandleLoadout(LoadoutUpdateMessage msg)
    {
        Loadout = msg;
        OnLoadoutChanged?.Invoke();
    }

    private void HandleOccupants(RoomOccupantsMessage msg)
    {
        Occupants = msg;
        OnOccupantsChanged?.Invoke();
    }

    private void HandleStats(EffectiveStatsMessage msg)
    {
        EffectiveStats = msg;
    }

    private void HandleExplorationData(ExplorationDataMessage msg)
    {
        ExplorationMap.Clear();
        foreach (var room in msg.rooms)
            ExplorationMap[room.roomId] = room;
        OnMapUpdated?.Invoke();
    }

    private void HandleExplorationUpdate(ExplorationUpdateMessage msg)
    {
        ExplorationMap[msg.room.roomId] = msg.room;
        OnMapUpdated?.Invoke();
    }

    // Call on room switch to reset transient state
    public void ResetForRoomSwitch()
    {
        CombatState = null;
        Occupants = null;
        OnCombatStateChanged?.Invoke();
        OnOccupantsChanged?.Invoke();
    }
}