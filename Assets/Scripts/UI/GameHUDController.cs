using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Bootstraps and drives the Unity UI Toolkit gameplay HUD.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameHUDController : MonoBehaviour
{
    private sealed class OverlaySection
    {
        public string Title;
        public string[] Lines;
    }

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

    private HelpDataMessage cachedHelpData;
    private InventoryUpdateMessage cachedInventory;
    private StashUpdateMessage cachedStash;
    private PlayerListMessage cachedPlayerList;
    private LoadoutUpdateMessage cachedLoadout;

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
        GameEvents.OnInventoryUpdate += HandleInventoryUpdate;
        GameEvents.OnStashUpdate += HandleStashUpdate;
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
        GameEvents.OnInventoryUpdate -= HandleInventoryUpdate;
        GameEvents.OnStashUpdate -= HandleStashUpdate;
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
        if (evt.keyCode == KeyCode.Escape)
        {
            overlayContainer?.AddToClassList("hidden");
            evt.StopPropagation();
            return;
        }

        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            return;

        SubmitCommand(commandInput?.value);
        evt.StopPropagation();
    }

    private void SubmitCommand(string input)
    {
        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        if (HandleLocalPanelCommand(trimmed))
        {
            commandInput.value = string.Empty;
            return;
        }

        if (EllmudNetworkManager.Instance == null || !EllmudNetworkManager.Instance.IsConnected)
        {
            AppendSystemLine("Not connected.");
            return;
        }

        commandInput.value = string.Empty;
        AppendSystemLine($"> {trimmed}");

        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var verb = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();
        EllmudNetworkManager.Instance.SendCommand(verb, args);
    }

    private bool HandleLocalPanelCommand(string input)
    {
        if (!input.StartsWith("/panel ", StringComparison.OrdinalIgnoreCase))
            return false;

        var panelName = input.Substring(7).Trim().ToLowerInvariant();
        switch (panelName)
        {
            case "inventory":
                ShowOverlaySections("Inventory", BuildInventorySections());
                return true;
            case "stash":
                ShowOverlaySections("Stash", BuildStashSections());
                return true;
            case "loadout":
                ShowOverlaySections("Loadout", BuildLoadoutSections(cachedLoadout));
                return true;
            case "help":
                ShowOverlaySections("Help", BuildHelpSections(cachedHelpData));
                return true;
            case "who":
                ShowOverlaySections("Who List", BuildPlayerListSections(cachedPlayerList));
                return true;
            default:
                AppendSystemLine("Unknown panel. Use /panel inventory|stash|loadout|help|who");
                return true;
        }
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

        cachedLoadout = msg;
        ShowOverlaySections("Loadout", BuildLoadoutSections(msg));
    }

    private void HandleInventoryUpdate(InventoryUpdateMessage msg)
    {
        cachedInventory = msg;
    }

    private void HandleStashUpdate(StashUpdateMessage msg)
    {
        cachedStash = msg;
    }

    private void HandleHelpData(HelpDataMessage msg)
    {
        if (msg?.commands == null)
            return;

        cachedHelpData = msg;
        ShowOverlaySections("Help", BuildHelpSections(msg));
    }

    private void HandlePlayerList(PlayerListMessage msg)
    {
        if (msg?.players == null)
            return;

        cachedPlayerList = msg;
        ShowOverlaySections("Who List", BuildPlayerListSections(msg));
    }

    private OverlaySection[] BuildHelpSections(HelpDataMessage msg)
    {
        if (msg?.commands == null)
            return new[] { new OverlaySection { Title = "Help", Lines = new[] { "No help data yet." } } };

        var sections = msg.commands
            .GroupBy(c => string.IsNullOrEmpty(c.category) ? "Other" : c.category)
            .OrderBy(g => g.Key)
            .Select(g => new OverlaySection
            {
                Title = g.Key,
                Lines = g.Select(c => $"{c.name}: {c.description} ({c.usage})").ToArray(),
            })
            .ToList();

        if (!string.IsNullOrEmpty(msg.focusCommand))
        {
            sections.Insert(0, new OverlaySection
            {
                Title = "Focus",
                Lines = new[] { msg.focusCommand },
            });
        }

        return sections.ToArray();
    }

    private OverlaySection[] BuildPlayerListSections(PlayerListMessage msg)
    {
        if (msg?.players == null)
            return new[] { new OverlaySection { Title = "Players", Lines = new[] { "No player list data yet." } } };

        var grouped = msg.players
            .GroupBy(p => string.IsNullOrEmpty(p.zone) ? "Unknown Zone" : p.zone)
            .OrderBy(g => g.Key)
            .Select(g => new OverlaySection
            {
                Title = g.Key,
                Lines = g.Select(p =>
                    $"{p.name} · lvl {p.level} · {p.posture}{(p.anon ? " · [Anon]" : string.Empty)}").ToArray(),
            })
            .ToArray();

        if (grouped.Length > 0)
            return grouped;

        return new[] { new OverlaySection { Title = "Players", Lines = new[] { "No players online." } } };
    }

    private OverlaySection[] BuildLoadoutSections(LoadoutUpdateMessage msg)
    {
        if (msg?.slots == null)
            return new[] { new OverlaySection { Title = "Loadout", Lines = new[] { "No loadout data yet." } } };

        return new[]
        {
            new OverlaySection { Title = "Weapon", Lines = BuildSlotLines(msg.slots.weapon, msg.slots.offhand) },
            new OverlaySection { Title = "Armour", Lines = BuildSlotLines(msg.slots.head, msg.slots.chest, msg.slots.legs, msg.slots.feet, msg.slots.hands) },
            new OverlaySection { Title = "Accessories", Lines = BuildSlotLines(msg.slots.ring1, msg.slots.ring2, msg.slots.amulet) },
        };
    }

    private OverlaySection[] BuildInventorySections()
    {
        if (cachedInventory?.items == null)
            return new[] { new OverlaySection { Title = "Inventory", Lines = new[] { "No inventory data yet." } } };

        return new[]
        {
            new OverlaySection
            {
                Title = $"Items ({cachedInventory.items.Length})",
                Lines = cachedInventory.items.Select(i => $"{i.name} · {i.tier} · {i.weight} wt").ToArray(),
            },
            new OverlaySection
            {
                Title = "Carry",
                Lines = new[] { $"{cachedInventory.currentWeight}/{cachedInventory.maxWeight}" },
            },
        };
    }

    private OverlaySection[] BuildStashSections()
    {
        if (cachedStash?.items == null)
            return new[] { new OverlaySection { Title = "Stash", Lines = new[] { "No stash data yet." } } };

        return new[]
        {
            new OverlaySection
            {
                Title = $"Items ({cachedStash.items.Length})",
                Lines = cachedStash.items.Select(i => $"{i.name} · {i.tier}").ToArray(),
            },
        };
    }

    private string[] BuildSlotLines(params ItemData[] items)
    {
        return items
            .Where(i => i != null)
            .Select(i => $"{i.name} ({i.tier})")
            .DefaultIfEmpty("None equipped")
            .ToArray();
    }

    private void ShowOverlaySections(string title, OverlaySection[] sections)
    {
        if (overlayContainer == null || sections == null || sections.Length == 0)
            return;

        overlayContainer.Clear();
        overlayContainer.RemoveFromClassList("hidden");

        var content = new VisualElement();
        content.AddToClassList("overlay-content");

        var titleLabel = new Label(title);
        titleLabel.AddToClassList("overlay-title");
        content.Add(titleLabel);

        var tabs = new VisualElement();
        tabs.AddToClassList("overlay-tabs");
        content.Add(tabs);

        var body = new VisualElement();
        body.AddToClassList("overlay-body");
        content.Add(body);

        var list = new ScrollView();
        list.AddToClassList("overlay-list");
        body.Add(list);

        var detail = new Label();
        detail.AddToClassList("overlay-detail");
        body.Add(detail);

        void RenderSection(OverlaySection section, Button activeButton)
        {
            foreach (var child in tabs.Children())
            {
                if (child is Button b)
                    b.EnableInClassList("overlay-tab-button-active", b == activeButton);
            }

            list.contentContainer.Clear();
            detail.text = string.Empty;

            if (section?.Lines == null || section.Lines.Length == 0)
            {
                detail.text = "No data.";
                return;
            }

            foreach (var line in section.Lines)
            {
                var row = new Button(() => detail.text = line)
                {
                    text = line,
                };
                row.AddToClassList("overlay-item-button");
                list.Add(row);
            }

            detail.text = section.Lines[0];
        }

        for (var i = 0; i < sections.Length; i++)
        {
            var section = sections[i];
            var tabButton = new Button { text = section.Title };
            tabButton.AddToClassList("overlay-tab-button");
            tabButton.clicked += () => RenderSection(section, tabButton);
            tabs.Add(tabButton);

            if (i == 0)
                RenderSection(section, tabButton);
        }

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