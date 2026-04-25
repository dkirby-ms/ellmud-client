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

[Serializable]
public class ZoneStateMessage
{
    public string state;
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
}

[Serializable]
public class CombatResultMessage
{
    public int tick;
    public string encounterId;
    public CombatResultEntry[] results;
    public bool combatEnded;
}

[Serializable]
public class CombatResultEntry
{
    public string actorId;
    public string actorName;
    public string action;
    public string targetId;
    public string targetName;
    public int damage;
    public int newHp;
    public int maxHp;
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
    public TelegraphedAction telegraphedAction;
}

[Serializable]
public class TelegraphedAction
{
    public string abilityName;
    public int remainingTicks;
    public string targetId;
}

[Serializable]
public class TelegraphMessage
{
    public string creatureId;
    public string creatureName;
    public string abilityName;
    public int remainingTicks;
    public string targetId;
    public string telegraphText;
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
    public string roomId;
    public int tier;
    public string targetRoomSlug;
}

[Serializable]
public class ZoneTransferMessage
{
    public string targetZoneSlug;
    public string targetRoomSlug;
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
    public int totalDeaths;
    public int survivedSeconds;
    public string causeOfDeath;
    public string zoneOfDeath;
}

// ── Inventory, Stash, Loadout ─────────────────────────────

[Serializable]
public class InventoryUpdateMessage
{
    public ItemData[] items;
    public float currentWeight;
    public float maxWeight;
}

[Serializable]
public class StashUpdateMessage
{
    public ItemData[] items;
}

[Serializable]
public class LoadoutUpdateMessage
{
    public EquipmentSlots slots;
}

[Serializable]
public class ItemData
{
    public string id;
    public string instanceId;
    public string definitionId;
    public string name;
    public string description;
    public string tier;
    public string type;
    public float weight;
    public string[] allowedSlots;
}

[Serializable]
public class EquipmentSlots
{
    public ItemData head;
    public ItemData chest;
    public ItemData legs;
    public ItemData feet;
    public ItemData hands;
    public ItemData weapon;
    public ItemData offhand;
    public ItemData ring1;
    public ItemData ring2;
    public ItemData amulet;
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
    public bool aggressive;
}

[Serializable]
public class PlayerInfo
{
    public string id;
    public string name;
    public bool disconnected;
}

// ── Exploration Map ───────────────────────────────────────

[Serializable]
public class ExplorationDataMessage
{
    public string type;
    public ExploredRoomData[] rooms;
    public string currentRoomId;
}

[Serializable]
public class ExplorationUpdateMessage
{
    public string type;
    public ExploredRoomData room;
}

[Serializable]
public class ExploredRoomData
{
    public string roomId;
    public string zoneSlug;
    public string visitedAt;
    public string roomName;
    public string roomType;
    public SerializableExitMap exits;
}

[Serializable]
public class SerializableExitMap
{
    public string north;
    public string south;
    public string east;
    public string west;
    public string up;
    public string down;
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
    public string startingZoneSlug;
    public string startingZoneName;
    public string factionSlug;
    public string factionName;
    public bool isActive;
    public string createdAt;
    public string lastPlayedAt;
    public TopSkillSummary[] topSkills;
    public int totalRuns;
    public BaseStatsMessage baseStats;
    public CharacterEquipmentSummary equipment;
    public int statPointsAvailable;
}

[Serializable]
public class TopSkillSummary
{
    public string name;
    public int level;
}

[Serializable]
public class CharacterEquipmentSummary
{
    public CharacterEquipmentItem head;
    public CharacterEquipmentItem chest;
    public CharacterEquipmentItem legs;
    public CharacterEquipmentItem feet;
    public CharacterEquipmentItem hands;
    public CharacterEquipmentItem weapon;
    public CharacterEquipmentItem offhand;
    public CharacterEquipmentItem ring1;
    public CharacterEquipmentItem ring2;
    public CharacterEquipmentItem amulet;
}

[Serializable]
public class CharacterEquipmentItem
{
    public string itemId;
    public string name;
}

// ── Effective Stats ───────────────────────────────────────

[Serializable]
public class EffectiveStatsMessage
{
    public int maxHp;
    public int attack;
    public int armour;
    public int shieldBlock;
    public int dodge;
    public BaseStatsMessage baseStats;
    public int statPointsAvailable;
}

[Serializable]
public class BaseStatsMessage
{
    public int maxHp;
    public int unarmed;
    public int oneHanded;
    public int twoHanded;
    public int ranged;
    public int shieldBlock;
    public int dodge;
    public int armour;
}

// ── Flag State ────────────────────────────────────────────

[Serializable]
public class FlagStateMessage
{
    public UserFlags flags;
}

[Serializable]
public class UserFlags
{
    public bool anon;
    public bool rp;
}

// ── Player List & Help ────────────────────────────────────

[Serializable]
public class PlayerListMessage
{
    public PlayerListEntry[] players;
}

[Serializable]
public class PlayerListEntry
{
    public string name;
    public int level;
    public string @class;
    public string zone;
    public string[] flags;
    public bool anon;
    public string posture;
}

[Serializable]
public class HelpDataMessage
{
    public HelpEntry[] commands;
    public string focusCommand;
}

[Serializable]
public class HelpEntry
{
    public string name;
    public string description;
    public string usage;
    public string[] aliases;
    public string category;
}