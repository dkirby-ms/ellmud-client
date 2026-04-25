using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Bootstraps a minimal playable flow for the Unity client using the existing UXML HUD.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameHUDController : MonoBehaviour
{
    [Header("Authentication")]
    [SerializeField] private bool autoLogin = false;
    [SerializeField] private bool autoRegisterIfLoginFails = false;
    [SerializeField] private string username = "";
    [SerializeField] private string password = "";

    [Header("Character")]
    [SerializeField] private bool autoCreateCharacterIfMissing = true;
    [SerializeField] private string defaultStartingZoneSlug = "the-reliquary";

    [Header("Runtime")]
    [SerializeField] private bool reconnectOnDisconnect = true;
    [SerializeField] private int reconnectAttempts = 3;

    private UIDocument uiDocument;
    private Label roomNameLabel;
    private Label zoneNameLabel;
    private Label playerNameLabel;
    private VisualElement exitsContainer;
    private ScrollView terminalScroll;
    private VisualElement terminalContent;
    private TextField commandInput;
    private Label postureLabel;
    private ProgressBar hpBar;
    private ProgressBar staminaBar;
    private VisualElement combatHud;
    private Label combatTickLabel;
    private ScrollView combatantsList;
    private VisualElement overlayContainer;
    private bool reconnecting;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private async void Start()
    {
        BindUi();
        RegisterEvents();
        await BootstrapAsync();
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    private void BindUi()
    {
        var root = uiDocument.rootVisualElement;
        roomNameLabel = root.Q<Label>("room-name");
        zoneNameLabel = root.Q<Label>("zone-name");
        playerNameLabel = root.Q<Label>("player-name");
        exitsContainer = root.Q<VisualElement>("exits-container");
        terminalScroll = root.Q<ScrollView>("terminal-scroll");
        terminalContent = root.Q<VisualElement>("terminal-content");
        commandInput = root.Q<TextField>("command-input");
        postureLabel = root.Q<Label>("posture-label");
        hpBar = root.Q<ProgressBar>("hp-bar");
        staminaBar = root.Q<ProgressBar>("stamina-bar");
        combatHud = root.Q<VisualElement>("combat-hud");
        combatTickLabel = root.Q<Label>("combat-tick");
        combatantsList = root.Q<ScrollView>("combatants-list");
        overlayContainer = root.Q<VisualElement>("overlay-container");

        if (commandInput != null)
            commandInput.RegisterCallback<KeyDownEvent>(OnCommandInputKeyDown);
    }

    private void RegisterEvents()
    {
        GameEvents.OnConnected += HandleConnected;
        GameEvents.OnDisconnected += HandleDisconnected;
        GameEvents.OnConnectionError += HandleConnectionError;
        GameEvents.OnError += HandleError;
        GameEvents.OnNarrate += HandleNarrate;
        GameEvents.OnRoomHeader += HandleRoomHeader;
        GameEvents.OnZoneState += HandleZoneState;
        GameEvents.OnPlayerState += HandlePlayerState;
        GameEvents.OnCombatState += HandleCombatState;
        GameEvents.OnRoomOccupants += HandleRoomOccupants;
        GameEvents.OnLoadoutUpdate += HandleLoadoutUpdate;
        GameEvents.OnHelpData += HandleHelpData;
        GameEvents.OnPlayerList += HandlePlayerList;
    }

    private void UnregisterEvents()
    {
        GameEvents.OnConnected -= HandleConnected;
        GameEvents.OnDisconnected -= HandleDisconnected;
        GameEvents.OnConnectionError -= HandleConnectionError;
        GameEvents.OnError -= HandleError;
        GameEvents.OnNarrate -= HandleNarrate;
        GameEvents.OnRoomHeader -= HandleRoomHeader;
        GameEvents.OnZoneState -= HandleZoneState;
        GameEvents.OnPlayerState -= HandlePlayerState;
        GameEvents.OnCombatState -= HandleCombatState;
        GameEvents.OnRoomOccupants -= HandleRoomOccupants;
        GameEvents.OnLoadoutUpdate -= HandleLoadoutUpdate;
        GameEvents.OnHelpData -= HandleHelpData;
        GameEvents.OnPlayerList -= HandlePlayerList;

        if (commandInput != null)
            commandInput.UnregisterCallback<KeyDownEvent>(OnCommandInputKeyDown);
    }

    private async Task BootstrapAsync()
    {
        try
        {
            if (AuthService.Instance == null)
            {
                AppendSystemLine("AuthService is missing from the scene.");
                return;
            }

            if (EllmudNetworkManager.Instance == null)
            {
                AppendSystemLine("EllmudNetworkManager is missing from the scene.");
                return;
            }

            if (!AuthService.Instance.IsAuthenticated)
            {
                if (!autoLogin || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    AppendSystemLine("Not authenticated. Enable auto-login or sign in through another UI.");
                    return;
                }

                var loginOk = await AuthService.Instance.Login(username, password);
                if (!loginOk && autoRegisterIfLoginFails)
                    loginOk = await AuthService.Instance.Register(username, password);

                if (!loginOk)
                {
                    AppendSystemLine("Authentication failed.");
                    return;
                }
            }

            var characters = await AuthService.Instance.FetchCharacters();
            if ((characters == null || characters.Length == 0) && autoCreateCharacterIfMissing)
            {
                var generatedName = BuildFallbackCharacterName();
                await AuthService.Instance.CreateCharacter(generatedName, defaultStartingZoneSlug);
                characters = await AuthService.Instance.FetchCharacters();
            }

            if (characters == null || characters.Length == 0)
            {
                AppendSystemLine("No character available. Create one to enter the world.");
                return;
            }

            var active = AuthService.Instance.ActiveCharacter ?? characters[0];
            if (AuthService.Instance.ActiveCharacter == null)
                active = await AuthService.Instance.SelectCharacter(active.id);

            if (playerNameLabel != null)
                playerNameLabel.text = active?.name ?? "Player";

            AppendSystemLine($"Entering world as {active.name}...");
            await EllmudNetworkManager.Instance.EnterActiveCharacterWorld(active.id);
        }
        catch (Exception ex)
        {
            AppendSystemLine($"Bootstrap failed: {ex.Message}");
        }
    }

    private string BuildFallbackCharacterName()
    {
        var suffix = UnityEngine.Random.Range(100, 999);
        return $"Wanderer{suffix}";
    }

    private void OnCommandInputKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            return;

        SubmitCommand(commandInput?.value);
        evt.StopPropagation();
    }

    private void SubmitCommand(string input)
    {
        if (EllmudNetworkManager.Instance == null || !EllmudNetworkManager.Instance.IsConnected)
        {
            AppendSystemLine("Not connected.");
            return;
        }

        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        commandInput.value = string.Empty;
        AppendSystemLine($"> {trimmed}");

        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var verb = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();
        EllmudNetworkManager.Instance.SendCommand(verb, args);
    }

    private async void HandleDisconnected(int code)
    {
        AppendSystemLine($"Disconnected ({code}).");

        if (!reconnectOnDisconnect || code == 4000 || reconnecting || EllmudNetworkManager.Instance == null)
            return;

        reconnecting = true;
        AppendSystemLine("Attempting reconnect...");

        var success = await EllmudNetworkManager.Instance.ReconnectToLastRoom(reconnectAttempts);
        AppendSystemLine(success ? "Reconnect successful." : "Reconnect failed.");
        reconnecting = false;
    }

    private void HandleConnected()
    {
        AppendSystemLine("Connected.");
    }

    private void HandleConnectionError(string message)
    {
        AppendSystemLine($"Connection error: {message}");
    }

    private void HandleError(string message)
    {
        AppendSystemLine($"Error: {message}");
    }

    private void HandleNarrate(NarrateMessage msg)
    {
        if (msg == null)
            return;

        AppendNarrationLine(msg.text, msg.type);
    }

    private void HandleRoomHeader(RoomHeaderMessage msg)
    {
        if (msg == null)
            return;

        if (roomNameLabel != null)
            roomNameLabel.text = string.IsNullOrEmpty(msg.roomName) ? "Unknown Room" : msg.roomName;

        if (zoneNameLabel != null)
            zoneNameLabel.text = string.IsNullOrEmpty(msg.zoneName) ? string.Empty : msg.zoneName;

        RenderExits(msg.exits);
    }

    private void HandleZoneState(ZoneStateMessage msg)
    {
        if (msg == null)
            return;

        AppendSystemLine($"Zone state: {msg.state}");
    }

    private void HandlePlayerState(PlayerStateMessage msg)
    {
        if (msg == null)
            return;

        if (postureLabel != null)
            postureLabel.text = $"Posture: {msg.posture}";

        if (hpBar != null)
        {
            hpBar.highValue = Mathf.Max(1, msg.maxHp);
            hpBar.value = Mathf.Clamp(msg.hp, 0, msg.maxHp);
            hpBar.title = $"HP {msg.hp}/{msg.maxHp}";
        }

        if (staminaBar != null)
        {
            staminaBar.highValue = Mathf.Max(1, msg.maxStamina);
            staminaBar.value = Mathf.Clamp(msg.stamina, 0, msg.maxStamina);
            staminaBar.title = $"Stamina {msg.stamina}/{msg.maxStamina}";
        }
    }

    private void HandleCombatState(CombatStateMessage msg)
    {
        if (msg == null)
            return;

        var hasCombatants = msg.combatants != null && msg.combatants.Length > 0;
        if (combatHud != null)
            combatHud.EnableInClassList("hidden", !hasCombatants);

        if (combatTickLabel != null)
            combatTickLabel.text = hasCombatants
                ? $"Encounter {msg.encounterId} · Tick {msg.tick}"
                : string.Empty;

        if (combatantsList?.contentContainer == null)
            return;

        combatantsList.contentContainer.Clear();
        if (!hasCombatants)
            return;

        foreach (var combatant in msg.combatants)
        {
            var row = new VisualElement();
            row.AddToClassList("combatant-row");

            var isHostile = msg.hostileIds != null && Array.IndexOf(msg.hostileIds, combatant.id) >= 0;
            row.AddToClassList(isHostile ? "combatant-hostile" : "combatant-friendly");

            var label = new Label(combatant.name)
            {
                tooltip = $"{combatant.status}"
            };
            label.AddToClassList("combatant-name");
            row.Add(label);

            var hp = new ProgressBar
            {
                highValue = Mathf.Max(1, combatant.maxHp),
                value = Mathf.Clamp(combatant.hp, 0, combatant.maxHp),
                title = $"{combatant.hp}/{combatant.maxHp}",
            };
            hp.AddToClassList("combatant-hp-bar");
            row.Add(hp);

            combatantsList.contentContainer.Add(row);
        }
    }

    private void HandleRoomOccupants(RoomOccupantsMessage msg)
    {
        if (msg == null)
            return;

        var creatureCount = msg.creatures?.Length ?? 0;
        var playerCount = msg.players?.Length ?? 0;
        AppendSystemLine($"Occupants: {playerCount} player(s), {creatureCount} creature(s)");
    }

    private void HandleLoadoutUpdate(LoadoutUpdateMessage msg)
    {
        if (msg?.slots == null)
            return;

        var equipped = 0;
        if (msg.slots.head != null) equipped++;
        if (msg.slots.chest != null) equipped++;
        if (msg.slots.legs != null) equipped++;
        if (msg.slots.feet != null) equipped++;
        if (msg.slots.hands != null) equipped++;
        if (msg.slots.weapon != null) equipped++;
        if (msg.slots.offhand != null) equipped++;
        if (msg.slots.ring1 != null) equipped++;
        if (msg.slots.ring2 != null) equipped++;
        if (msg.slots.amulet != null) equipped++;

        AppendSystemLine($"Loadout updated: {equipped} item(s) equipped.");
    }

    private void HandleHelpData(HelpDataMessage msg)
    {
        if (msg?.commands == null)
            return;

        ShowOverlay(
            "Help",
            $"Commands: {msg.commands.Length}\nFocus: {(string.IsNullOrEmpty(msg.focusCommand) ? "none" : msg.focusCommand)}");
    }

    private void HandlePlayerList(PlayerListMessage msg)
    {
        if (msg?.players == null)
            return;

        var lines = string.Join("\n", msg.players.Select(p =>
            $"{p.name} {(string.IsNullOrEmpty(p.zone) ? "" : $"@ {p.zone}")}"));
        ShowOverlay("Who List", string.IsNullOrEmpty(lines) ? "No players online." : lines);
    }

    private void ShowOverlay(string title, string body)
    {
        if (overlayContainer == null)
            return;

        overlayContainer.Clear();
        overlayContainer.RemoveFromClassList("hidden");

        var content = new VisualElement();
        content.AddToClassList("overlay-content");

        var titleLabel = new Label(title);
        titleLabel.AddToClassList("overlay-title");
        content.Add(titleLabel);

        var bodyLabel = new Label(body);
        bodyLabel.AddToClassList("overlay-text");
        content.Add(bodyLabel);

        var dismiss = new Button(() => overlayContainer.AddToClassList("hidden"))
        {
            text = "Close"
        };
        dismiss.AddToClassList("overlay-button");
        content.Add(dismiss);

        overlayContainer.Add(content);
    }

    private void RenderExits(string[] exits)
    {
        if (exitsContainer == null)
            return;

        exitsContainer.Clear();
        if (exits == null || exits.Length == 0)
            return;

        foreach (var exit in exits)
        {
            var exitDirection = exit;
            var button = new Button(() => SubmitCommand($"go {exitDirection}"))
            {
                text = exitDirection,
            };
            button.AddToClassList("exit-button");
            exitsContainer.Add(button);
        }
    }

    private void AppendSystemLine(string text)
    {
        AppendNarrationLine(text, "system");
    }

    private void AppendNarrationLine(string text, string type)
    {
        if (terminalContent == null)
            return;

        var line = new Label(text ?? string.Empty);
        line.AddToClassList("narration-line");
        if (!string.IsNullOrEmpty(type))
            line.AddToClassList($"narration-{type}");

        terminalContent.Add(line);
        terminalScroll?.ScrollTo(line);
    }
}