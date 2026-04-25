using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using UnityEngine;

public class EllmudNetworkManager : MonoBehaviour
{
    public static EllmudNetworkManager Instance { get; private set; }

    [SerializeField]
    private string serverEndpoint = "ws://localhost:2567";

    private Client client;
    private Room<NoState> currentRoom;

    public bool IsConnected => currentRoom != null;
    public string CurrentRoomType { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        client = new Client(serverEndpoint);
    }

    // ── Join Refuge (safe hub) ────────────────────────────
    public async Task JoinRefuge()
    {
        var options = new Dictionary<string, object>
        {
            { "token", AuthService.Instance.Token }
        };

        try
        {
            currentRoom = await client.JoinOrCreate(
                "refuge", options);
            CurrentRoomType = "refuge";
            RegisterMessageHandlers();
            RegisterLifecycleHandlers();
            GameEvents.OnConnected?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to join Refuge: {ex.Message}");
            GameEvents.OnError?.Invoke(ex.Message);
        }
    }

    // ── Join Zone (dangerous area) ────────────────────────
    public async Task JoinZone(
        string zoneId = null, string entryRoomId = null)
    {
        var options = new Dictionary<string, object>
        {
            { "token", AuthService.Instance.Token }
        };
        if (zoneId != null)
            options["zoneId"] = zoneId;
        if (entryRoomId != null)
            options["entryRoomId"] = entryRoomId;

        try
        {
            currentRoom = await client.JoinOrCreate(
                "zone", options);
            CurrentRoomType = "zone";
            RegisterMessageHandlers();
            RegisterLifecycleHandlers();
            GameEvents.OnConnected?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to join Zone: {ex.Message}");
            GameEvents.OnError?.Invoke(ex.Message);
        }
    }

    // ── Send a text command ───────────────────────────────
    public void SendCommand(string verb, params string[] args)
    {
        currentRoom?.Send(MessageTypes.COMMAND,
            new CommandMessage { verb = verb, args = args });
    }

    // ── Send a typed message ──────────────────────────────
    public void SendMessage<T>(string type, T message)
    {
        currentRoom?.Send(type, message);
    }

    // ── Send a simple string message ──────────────────────
    public new void SendMessage(string type)
    {
        currentRoom?.Send(type);
    }

    // ── Room Switching ────────────────────────────────────
    private async void HandleRoomSwitch(RoomSwitchMessage msg)
    {
        Debug.Log($"Room switch → {msg.target} ({msg.reason})");
        await LeaveCurrentRoom();

        if (msg.target == "zone")
        {
            string zoneId = msg.options?.zoneId;
            string entryRoom = msg.options?.entryRoomId;
            await JoinZone(zoneId, entryRoom);
        }
        else
        {
            await JoinRefuge();
        }
    }

    private async void HandleZoneTransfer(ZoneTransferMessage msg)
    {
        Debug.Log(
            $"Zone transfer → {msg.targetZoneId} ({msg.reason})");
        await LeaveCurrentRoom();
        await JoinZone(msg.targetZoneId, msg.targetRoomId);
    }

    public async Task LeaveCurrentRoom()
    {
        if (currentRoom != null)
        {
            await currentRoom.Leave();
            currentRoom = null;
            CurrentRoomType = null;
        }
    }

    // ── Register ALL message handlers ─────────────────────
    private void RegisterMessageHandlers()
    {
        var room = currentRoom;

        // Narration (the primary output channel)
        room.OnMessage<NarrateMessage>(
            MessageTypes.NARRATE,
            msg => GameEvents.OnNarrate?.Invoke(msg));

        // Room information
        room.OnMessage<RoomHeaderMessage>(
            MessageTypes.ROOM_HEADER,
            msg => GameEvents.OnRoomHeader?.Invoke(msg));

        // Player vitals
        room.OnMessage<PlayerStateMessage>(
            MessageTypes.PLAYER_STATE,
            msg => GameEvents.OnPlayerState?.Invoke(msg));

        // Combat
        room.OnMessage<CombatStateMessage>(
            MessageTypes.COMBAT_STATE,
            msg => GameEvents.OnCombatState?.Invoke(msg));
        room.OnMessage<TelegraphMessage>(
            MessageTypes.TELEGRAPH,
            msg => GameEvents.OnTelegraph?.Invoke(msg));

        // Overlay / death system
        room.OnMessage<OverlayMessage>(
            MessageTypes.OVERLAY_STATE,
            msg => GameEvents.OnOverlayState?.Invoke(msg));

        // Inventory, stash, loadout
        room.OnMessage<InventoryUpdateMessage>(
            MessageTypes.INVENTORY_UPDATE,
            msg => GameEvents.OnInventoryUpdate?.Invoke(msg));
        room.OnMessage<StashUpdateMessage>(
            MessageTypes.STASH_UPDATE,
            msg => GameEvents.OnStashUpdate?.Invoke(msg));
        room.OnMessage<LoadoutUpdateMessage>(
            MessageTypes.LOADOUT_UPDATE,
            msg => GameEvents.OnLoadoutUpdate?.Invoke(msg));

        // Room switching
        room.OnMessage<RoomSwitchMessage>(
            MessageTypes.ROOM_SWITCH,
            HandleRoomSwitch);
        room.OnMessage<ZoneTransferMessage>(
            MessageTypes.ZONE_TRANSFER,
            HandleZoneTransfer);

        // Exploration map
        room.OnMessage<ExplorationDataMessage>(
            MessageTypes.EXPLORATION_DATA,
            msg => GameEvents.OnExplorationData?.Invoke(msg));
        room.OnMessage<ExplorationUpdateMessage>(
            MessageTypes.EXPLORATION_UPDATE,
            msg => GameEvents.OnExplorationUpdate?.Invoke(msg));

        // Occupants
        room.OnMessage<RoomOccupantsMessage>(
            MessageTypes.ROOM_OCCUPANTS,
            msg => GameEvents.OnRoomOccupants?.Invoke(msg));

        // Stats and flags
        room.OnMessage<EffectiveStatsMessage>(
            MessageTypes.EFFECTIVE_STATS,
            msg => GameEvents.OnEffectiveStats?.Invoke(msg));
        room.OnMessage<FlagStateMessage>(
            MessageTypes.FLAG_STATE,
            msg => GameEvents.OnFlagState?.Invoke(msg));

        // Character management
        room.OnMessage<CharacterListResponse>(
            MessageTypes.CHARACTER_LIST_RESPONSE,
            msg => GameEvents.OnCharacterList?.Invoke(msg));
        room.OnMessage<CharacterCreatedMessage>(
            MessageTypes.CHARACTER_CREATED,
            msg => GameEvents.OnCharacterCreated?.Invoke(msg));
        room.OnMessage<CharacterErrorMessage>(
            MessageTypes.CHARACTER_ERROR,
            msg => GameEvents.OnCharacterError?.Invoke(msg));

        // Help and player list
        room.OnMessage<HelpDataMessage>(
            MessageTypes.HELP_DATA,
            msg => GameEvents.OnHelpData?.Invoke(msg));
        room.OnMessage<PlayerListMessage>(
            MessageTypes.PLAYER_LIST,
            msg => GameEvents.OnPlayerList?.Invoke(msg));
    }

    // ── Lifecycle handlers ────────────────────────────────
    private void RegisterLifecycleHandlers()
    {
        currentRoom.OnLeave += (code) =>
        {
            Debug.Log($"Left room (code: {code})");
            GameEvents.OnDisconnected?.Invoke(code);
        };

        currentRoom.OnError += (code, message) =>
        {
            Debug.LogError($"Room error {code}: {message}");
            GameEvents.OnError?.Invoke(message);
        };
    }

    private void OnDestroy()
    {
        LeaveCurrentRoom().ConfigureAwait(false);
    }
}