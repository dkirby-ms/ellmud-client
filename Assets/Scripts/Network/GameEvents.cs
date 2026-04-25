using System;

public static class GameEvents
{
    // Server → Client events
    public static Action<NarrateMessage> OnNarrate;
    public static Action<RoomHeaderMessage> OnRoomHeader;
    public static Action<PlayerStateMessage> OnPlayerState;
    public static Action<CombatStateMessage> OnCombatState;
    public static Action<TelegraphMessage> OnTelegraph;
    public static Action<OverlayMessage> OnOverlayState;
    public static Action<InventoryUpdateMessage> OnInventoryUpdate;
    public static Action<StashUpdateMessage> OnStashUpdate;
    public static Action<LoadoutUpdateMessage> OnLoadoutUpdate;
    public static Action<RoomSwitchMessage> OnRoomSwitch;
    public static Action<ZoneTransferMessage> OnZoneTransfer;
    public static Action<RoomOccupantsMessage> OnRoomOccupants;
    public static Action<ExplorationDataMessage> OnExplorationData;
    public static Action<ExplorationUpdateMessage> OnExplorationUpdate;
    public static Action<EffectiveStatsMessage> OnEffectiveStats;
    public static Action<CombatResultMessage> OnCombatResult;
    public static Action<FlagStateMessage> OnFlagState;
    public static Action<HelpDataMessage> OnHelpData;
    public static Action<PlayerListMessage> OnPlayerList;

    // Connection lifecycle
    public static Action OnConnected;
    public static Action<int> OnDisconnected;      // code
    public static Action<string> OnError;            // message
    public static Action<string> OnConnectionError;
}