using System;

// ── Narration ──────────────────────────────────────────────

[Serializable]
public class NarrateMessage
{
    public string text;
    public string type;       // "room", "combat", "system", "speech",
                               // "sound", "trace", "awareness", "ambient"
    public long timestamp;
    public CombatEventData combatEvent;  // nullable
}

[Serializable]
public class CombatEventData
{
    public string eventType;   // "strike", "dodge", "flee", "defeated",
                               // "combat_end"
    public string actorId;
    public string targetId;
    public string signalClass;
    public string icon;
}

// ── Room Info ──────────────────────────────────────────────

[Serializable]
public class RoomHeaderMessage
{
    public string roomName;
    public string[] exits;
    public float stability;
    public string zoneName;
    public string roomType;
    public string roomSlug;
}

// ── Player State ──────────────────────────────────────────

[Serializable]
public class PlayerStateMessage
{
    public int hp;
    public int maxHp;
    public int stamina;
    public int maxStamina;
    public StatusEffect[] statusEffects;
    public string posture;
}

[Serializable]
public class StatusEffect
{
    public string id;
    public string name;
    public int remainingTicks;
    public string icon;
}

// ── Combat State ──────────────────────────────────────────

[Serializable]
public class CombatStateMessage
{
    public string encounterId;
    public int tick;
    public CombatantSnapshot[] combatants;
    public string[] hostileIds;
    public string playerTargetId;
    public bool isParticipant;
}

[Serializable]
public class CombatantSnapshot
{
    public string id;
    public string name;
    public int hp;
    public int maxHp;
    public bool isPlayer;
    public bool isNPC;
    public string status;        // "fighting", "downed", "dead"
    public string currentTarget;
}

[Serializable]
public class TelegraphMessage
{
    public string encounterId;
    public string actorId;
    public string actorName;
    public string actionType;
    public string description;
    public int tick;
}

// ── Room Switching & Zones ────────────────────────────────

[Serializable]
public class RoomSwitchMessage
{
    public string target;          // "zone" or "refuge"
    public RoomSwitchOptions options;
    public string reason;
}

[Serializable]
public class RoomSwitchOptions
{
    public string zoneId;
    public string entryRoomId;
}

[Serializable]
public class ZoneTransferMessage
{
    public string targetZoneId;
    public string targetRoomId;
    public string reason;
}

// ── Overlay / Death System ────────────────────────────────

[Serializable]
public class OverlayMessage
{
    public string playerId;
    public string state;           // "death", "downed", "stabilized",
                                    // "bleed_out", "permadeath"
    public string narration;
    public long timestamp;
    public PermadeathStats permadeathStats;  // only when state == "permadeath"
}

[Serializable]
public class PermadeathStats
{
    public string characterName;
    public int level;
    public int totalKills;
    public int roomsExplored;
    public long survivalTime;
}

// ── Inventory, Stash, Loadout ─────────────────────────────

[Serializable]
public class InventoryUpdateMessage
{
    public ItemData[] items;
    public int capacity;
}

[Serializable]
public class StashUpdateMessage
{
    public ItemData[] items;
    public int capacity;
}

[Serializable]
public class LoadoutUpdateMessage
{
    public EquipmentSlot[] slots;
}

[Serializable]
public class ItemData
{
    public string id;
    public string name;
    public string description;
    public string rarity;
    public string itemType;
    public string icon;
}

[Serializable]
public class EquipmentSlot
{
    public string slotName;
    public ItemData item;      // null if empty
}

// ── Room Occupants ────────────────────────────────────────

[Serializable]
public class RoomOccupantsMessage
{
    public CreatureInfo[] creatures;
    public PlayerInfo[] players;
}

[Serializable]
public class CreatureInfo
{
    public string id;
    public string name;
    public string type;
    public string status;
    public string icon;
}

[Serializable]
public class PlayerInfo
{
    public string id;
    public string name;
    public string status;
}

// ── Exploration Map ───────────────────────────────────────

[Serializable]
public class ExplorationDataMessage
{
    public ExploredRoomData[] rooms;
}

[Serializable]
public class ExplorationUpdateMessage
{
    public ExploredRoomData room;
}

[Serializable]
public class ExploredRoomData
{
    public string roomId;
    public string roomName;
    public string roomType;
    public ExitMap[] exits;      // direction → roomId pairs
}

[Serializable]
public class ExitMap
{
    public string direction;
    public string targetRoomId;
}

// ── Character Management ──────────────────────────────────

[Serializable]
public class CharacterListResponse
{
    public CharacterSummary[] characters;
}

[Serializable]
public class CharacterSummary
{
    public string id;
    public string name;
    public string archetype;
    public int level;
}

[Serializable]
public class CharacterCreatedMessage
{
    public string characterId;
    public string name;
}

[Serializable]
public class CharacterErrorMessage
{
    public string error;
}

// ── Effective Stats ───────────────────────────────────────

[Serializable]
public class EffectiveStatsMessage
{
    public int attack;
    public int defense;
    public int speed;
    public int maxHp;
    public int maxStamina;
}

// ── Flag State ────────────────────────────────────────────

[Serializable]
public class FlagStateMessage
{
    public string flagName;
    public bool value;
}

// ── Player List & Help ────────────────────────────────────

[Serializable]
public class PlayerListMessage
{
    public PlayerInfo[] players;
}

[Serializable]
public class HelpDataMessage
{
    public HelpEntry[] entries;
}

[Serializable]
public class HelpEntry
{
    public string command;
    public string description;
    public string usage;
}