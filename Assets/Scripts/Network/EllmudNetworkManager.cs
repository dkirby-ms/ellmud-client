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
    private string lastJoinedRoomName;
    private Dictionary<string, object> lastJoinOptions;

    public bool IsConnected => currentRoom != null;
    public string CurrentRoomType { get; private set; }

    private const string RefugeRoomName = "zone:the-refuge";
    private const string ProceduralZoneRoomName = "zone";

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
        await JoinRoom(RefugeRoomName, BuildJoinOptions(zoneSlug: "the-refuge"));
    }

    // ── Join Zone (dangerous area) ────────────────────────
    public async Task JoinZone(
        string zoneSlug = null,
        string roomId = null,
        string targetRoomSlug = null,
        int? tier = null)
    {
        await JoinRoom(
            ProceduralZoneRoomName,
            BuildJoinOptions(zoneSlug, roomId, targetRoomSlug, tier));
    }

    /// <summary>
    /// Resolves the authenticated player's spawn target via REST and joins it.
    /// </summary>
    public async Task EnterActiveCharacterWorld(string characterId = null)
    {
        var spawnZone = await AuthService.Instance.FetchSpawnZone();
        await JoinSpawnTarget(spawnZone, characterId);
    }

    /// <summary>
    /// Selects a character through the REST API, then enters the world.
    /// </summary>
    /// <param name="characterId">The character identifier to activate.</param>
    /// <returns>The selected character summary.</returns>
    public async Task<CharacterSummary> SelectCharacterAndEnterWorld(string characterId)
    {
        var selectedCharacter = await AuthService.Instance.SelectCharacter(characterId);
        await EnterActiveCharacterWorld(selectedCharacter?.id);
        return selectedCharacter;
    }

    private Dictionary<string, object> BuildJoinOptions(
        string zoneSlug = null,
        string roomId = null,
        string targetRoomSlug = null,
        int? tier = null,
        string characterId = null)
    {
        var options = new Dictionary<string, object>
        {
            { "token", AuthService.Instance.Token }
        };

        var resolvedCharacterId = ResolveCharacterId(characterId);
        if (!string.IsNullOrEmpty(resolvedCharacterId))
            options["characterId"] = resolvedCharacterId;

        if (!string.IsNullOrEmpty(zoneSlug))
            options["zoneSlug"] = zoneSlug;
        if (!string.IsNullOrEmpty(roomId))
            options["roomId"] = roomId;
        if (!string.IsNullOrEmpty(targetRoomSlug))
            options["targetRoomSlug"] = targetRoomSlug;
        if (tier.HasValue)
            options["tier"] = tier.Value;

        return options;
    }

    private string ResolveCharacterId(string explicitCharacterId = null)
    {
        if (!string.IsNullOrEmpty(explicitCharacterId))
            return explicitCharacterId;

        var auth = AuthService.Instance;
        if (auth?.ActiveCharacter != null && !string.IsNullOrEmpty(auth.ActiveCharacter.id))
            return auth.ActiveCharacter.id;

        return null;
    }

    private async Task<bool> JoinRoom(string roomName, Dictionary<string, object> options)
    {
        try
        {
            if (currentRoom != null)
                await LeaveCurrentRoom();

            currentRoom = await client.JoinOrCreate(roomName, options);
            CurrentRoomType = roomName;
            lastJoinedRoomName = roomName;
            lastJoinOptions = CloneJoinOptions(options);
            RegisterMessageHandlers();
            RegisterLifecycleHandlers();
            GameEvents.OnConnected?.Invoke();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to join {roomName}: {ex.Message}");
            GameEvents.OnError?.Invoke(ex.Message);
            return false;
        }
    }

    public async Task<bool> ReconnectToLastRoom(int maxAttempts = 3)
    {
        if (string.IsNullOrEmpty(lastJoinedRoomName) || lastJoinOptions == null)
        {
            GameEvents.OnConnectionError?.Invoke("No previous room context found for reconnect.");
            return false;
        }

        var attempts = Mathf.Max(1, maxAttempts);
        for (var i = 1; i <= attempts; i++)
        {
            if (await JoinRoom(lastJoinedRoomName, CloneJoinOptions(lastJoinOptions)))
                return true;
        }

        GameEvents.OnConnectionError?.Invoke(
            $"Reconnect failed after {attempts} attempt(s).");
        return false;
    }

    private Dictionary<string, object> CloneJoinOptions(Dictionary<string, object> source)
    {
        if (source == null)
            return null;

        return new Dictionary<string, object>(source);
    }

    private async Task JoinSpawnTarget(AuthService.SpawnZoneInfo spawnZone, string characterId = null)
    {
        if (spawnZone == null)
        {
            await JoinRefuge();
            return;
        }

        var roomName = string.IsNullOrEmpty(spawnZone.target)
            ? RefugeRoomName
            : spawnZone.target;
        var zoneSlug = string.IsNullOrEmpty(spawnZone.zoneSlug)
            ? ExtractZoneSlug(roomName)
            : spawnZone.zoneSlug;

        await JoinRoom(roomName, BuildJoinOptions(zoneSlug: zoneSlug, characterId: characterId));
    }

    private string ExtractZoneSlug(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
            return null;

        return roomName.StartsWith("zone:")
            ? roomName.Substring("zone:".Length)
            : null;
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

        var options = BuildJoinOptions(
            roomId: msg.options?.roomId,
            targetRoomSlug: msg.options?.targetRoomSlug,
            tier: msg.options != null && msg.options.tier > 0 ? msg.options.tier : (int?)null);

        if (msg.target.StartsWith("zone:") && !options.ContainsKey("zoneSlug"))
            options["zoneSlug"] = msg.target.Substring("zone:".Length);

        await JoinRoom(msg.target, options);
    }

    private async void HandleZoneTransfer(ZoneTransferMessage msg)
    {
        Debug.Log(
            $"Zone transfer → {msg.targetZoneSlug}:{msg.targetRoomSlug}");
        await LeaveCurrentRoom();

        var targetRoomName = $"zone:{msg.targetZoneSlug}";
        await JoinRoom(
            targetRoomName,
            BuildJoinOptions(
                zoneSlug: msg.targetZoneSlug,
                targetRoomSlug: msg.targetRoomSlug));
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
        room.OnMessage<ZoneStateMessage>(
            MessageTypes.ZONE_STATE,
            msg => GameEvents.OnZoneState?.Invoke(msg));

        // Player vitals
        room.OnMessage<PlayerStateMessage>(
            MessageTypes.PLAYER_STATE,
            msg => GameEvents.OnPlayerState?.Invoke(msg));

        // Combat
        room.OnMessage<CombatResultMessage>(
            MessageTypes.COMBAT_RESULT,
            msg => GameEvents.OnCombatResult?.Invoke(msg));
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