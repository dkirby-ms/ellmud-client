// MessageTypes.cs — Wire-level message type identifiers
// Mirrors the server's MessageTypes const from packages/shared/src/index.ts

public static class MessageTypes
{
    // Client → Server
    public const string COMMAND = "cmd";
    public const string EQUIP_ITEM = "equip_item";
    public const string UNEQUIP_ITEM = "unequip_item";
    public const string SWAP_ITEM = "swap_item";
    public const string TOGGLE_FLAG = "toggle_flag";

    // Server → Client
    public const string NARRATE = "narrate";
    public const string ROOM_HEADER = "room_header";
    public const string ZONE_STATE = "zone_state";
    public const string COMBAT_RESULT = "combat_result";
    public const string PLAYER_STATE = "player_state";
    public const string TELEGRAPH = "telegraph";
    public const string OVERLAY_STATE = "overlay_state";
    public const string STASH_UPDATE = "stash_update";
    public const string LOADOUT_UPDATE = "loadout_update";
    public const string INVENTORY_UPDATE = "inventory_update";
    public const string ROOM_SWITCH = "room_switch";
    public const string ZONE_TRANSFER = "zone_transfer";
    public const string EXPLORATION_DATA = "exploration_data";
    public const string EXPLORATION_UPDATE = "exploration_update";
    public const string ROOM_OCCUPANTS = "room_occupants";
    public const string FLAG_STATE = "flag_state";
    public const string EFFECTIVE_STATS = "effective_stats";
    public const string COMBAT_STATE = "combat_state";
    public const string REQUEST_PLAYER_LIST = "request_player_list";
    public const string PLAYER_LIST = "player_list";
    public const string HELP_DATA = "help_data";
}