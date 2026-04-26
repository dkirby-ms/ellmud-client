using System;
using System.Collections.Generic;
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
    private readonly Dictionary<string, ExploredRoomData> exploredRooms = new();
    private string currentExplorationRoomId;

    private bool reconnecting;
    private bool isBlockingOverlayOpen;

    // Status effects
    private VisualElement statusEffectsContainer;

    // Ability bar
    private VisualElement abilityBar;
    private readonly Label[] abilityNameLabels = new Label[5];
    private static readonly string[] DefaultAbilityCommands = { "attack", "skills", "wait", "flee", "look" };
    private static readonly string[] DefaultAbilityNames = { "Attack", "Skills", "Wait", "Flee", "Look" };

    // Minimap
    private VisualElement minimapContainer;
    private MinimapElement minimapElement;

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

    private void Update()
    {
        if (commandInput != null &&
            commandInput.focusController?.focusedElement == commandInput)
            return;

        for (var i = 0; i < 5; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
            {
                TriggerAbilitySlot(i);
                return;
            }
        }
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

        statusEffectsContainer = root.Q<VisualElement>("status-effects");
        minimapContainer = root.Q<VisualElement>("minimap-container");
        abilityBar = root.Q<VisualElement>("ability-bar");
        BuildAbilityBar();
        BuildMinimap();
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
        GameEvents.OnExplorationData += HandleExplorationData;
        GameEvents.OnExplorationUpdate += HandleExplorationUpdate;
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
        GameEvents.OnExplorationData -= HandleExplorationData;
        GameEvents.OnExplorationUpdate -= HandleExplorationUpdate;

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
                var authenticated = false;

                if (!autoLogin || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    authenticated = await ShowAuthenticationOverlayAsync();
                }
                else
                {
                    authenticated = await AuthService.Instance.Login(username, password);
                    if (!authenticated && autoRegisterIfLoginFails)
                        authenticated = await AuthService.Instance.Register(username, password);
                }

                if (!authenticated)
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
                var created = await ShowCharacterSelectionOverlayAsync(Array.Empty<CharacterSummary>());
                if (created == null)
                {
                    AppendSystemLine("No character available. Create one to enter the world.");
                    return;
                }

                characters = await AuthService.Instance.FetchCharacters();
            }

            var active = AuthService.Instance.ActiveCharacter ?? characters[0];
            if (AuthService.Instance.ActiveCharacter == null && characters.Length > 1)
            {
                var selected = await ShowCharacterSelectionOverlayAsync(characters);
                if (selected != null)
                    active = selected;
            }
            else if (AuthService.Instance.ActiveCharacter == null)
            {
                active = await AuthService.Instance.SelectCharacter(active.id);
            }

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
            if (!isBlockingOverlayOpen)
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

        if (string.Equals(trimmed, "/map", StringComparison.OrdinalIgnoreCase))
        {
            ShowOverlaySections("Map", BuildMapSections());
            commandInput.value = string.Empty;
            return;
        }

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
                ShowInventoryOverlay();
                return true;
            case "stash":
                ShowStashOverlay();
                return true;
            case "loadout":
                ShowLoadoutOverlay();
                return true;
            case "help":
                ShowOverlaySections("Help", BuildHelpSections(cachedHelpData));
                return true;
            case "who":
                ShowOverlaySections("Who List", BuildPlayerListSections(cachedPlayerList));
                return true;
            case "map":
                ShowOverlaySections("Map", BuildMapSections());
                return true;
            default:
                AppendSystemLine("Unknown panel. Use /panel inventory|stash|loadout|help|who|map");
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

        if (!string.IsNullOrEmpty(msg.roomSlug))
            currentExplorationRoomId = msg.roomSlug;

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

        RenderStatusEffects(msg.statusEffects);
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

    private void HandleExplorationData(ExplorationDataMessage msg)
    {
        if (msg?.rooms == null)
            return;

        exploredRooms.Clear();
        foreach (var room in msg.rooms)
        {
            if (room == null || string.IsNullOrEmpty(room.roomId))
                continue;

            exploredRooms[room.roomId] = room;
        }

        if (!string.IsNullOrEmpty(msg.currentRoomId))
            currentExplorationRoomId = msg.currentRoomId;

        RefreshMinimap();
    }

    private void HandleExplorationUpdate(ExplorationUpdateMessage msg)
    {
        if (msg?.room == null || string.IsNullOrEmpty(msg.room.roomId))
            return;

        exploredRooms[msg.room.roomId] = msg.room;
        currentExplorationRoomId = msg.room.roomId;
        RefreshMinimap();
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

    private OverlaySection[] BuildMapSections()
    {
        if (exploredRooms.Count == 0)
        {
            return new[]
            {
                new OverlaySection
                {
                    Title = "Map",
                    Lines = new[] { "No exploration data received yet. Use /map again after moving." }
                }
            };
        }

        var sections = exploredRooms.Values
            .GroupBy(room => string.IsNullOrEmpty(room.zoneSlug) ? "Unknown Zone" : room.zoneSlug)
            .OrderBy(group => group.Key)
            .Select(group => new OverlaySection
            {
                Title = group.Key,
                Lines = group
                    .OrderBy(room => room.roomName)
                    .Select(room =>
                    {
                        var marker = room.roomId == currentExplorationRoomId ? "*" : " ";
                        var roomName = string.IsNullOrEmpty(room.roomName) ? room.roomId : room.roomName;
                        return $"{marker} {roomName} [{room.roomType}] {FormatExits(room.exits)}";
                    })
                    .ToArray(),
            })
            .ToArray();

        return sections.Length > 0
            ? sections
            : new[] { new OverlaySection { Title = "Map", Lines = new[] { "No mapped rooms yet." } } };
    }

    private string FormatExits(SerializableExitMap exits)
    {
        if (exits == null)
            return "No exits";

        var list = new List<string>();
        AddExit(list, "N", exits.north);
        AddExit(list, "S", exits.south);
        AddExit(list, "E", exits.east);
        AddExit(list, "W", exits.west);
        AddExit(list, "U", exits.up);
        AddExit(list, "D", exits.down);
        return list.Count == 0 ? "No exits" : string.Join(", ", list);
    }

    private void AddExit(List<string> exits, string label, string target)
    {
        if (!string.IsNullOrEmpty(target))
            exits.Add(label);
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

    private async Task<bool> ShowAuthenticationOverlayAsync()
    {
        if (overlayContainer == null || AuthService.Instance == null)
            return false;

        var resultSource = new TaskCompletionSource<bool>();
        isBlockingOverlayOpen = true;
        overlayContainer.Clear();
        overlayContainer.RemoveFromClassList("hidden");

        var content = new VisualElement();
        content.AddToClassList("overlay-content");

        var title = new Label("Sign In");
        title.AddToClassList("overlay-title");
        content.Add(title);

        var subtitle = new Label("Log in or create an account to continue.");
        subtitle.AddToClassList("overlay-text");
        content.Add(subtitle);

        var usernameField = new TextField("Username")
        {
            value = username ?? string.Empty,
        };
        usernameField.AddToClassList("overlay-input");
        content.Add(usernameField);

        var passwordField = new TextField("Password")
        {
            value = password ?? string.Empty,
            isPasswordField = true,
        };
        passwordField.AddToClassList("overlay-input");
        content.Add(passwordField);

        var status = new Label();
        status.AddToClassList("overlay-status");
        content.Add(status);

        var buttonRow = new VisualElement();
        buttonRow.AddToClassList("overlay-button-row");

        var loginButton = new Button(async () =>
        {
            status.text = "Signing in...";
            username = usernameField.value?.Trim() ?? string.Empty;
            password = passwordField.value ?? string.Empty;

            var success = await AuthService.Instance.Login(username, password);
            if (success)
            {
                status.text = "Signed in.";
                resultSource.TrySetResult(true);
                return;
            }

            status.text = "Login failed.";
        })
        {
            text = "Login",
        };
        loginButton.AddToClassList("overlay-button");
        buttonRow.Add(loginButton);

        var registerButton = new Button(async () =>
        {
            status.text = "Creating account...";
            username = usernameField.value?.Trim() ?? string.Empty;
            password = passwordField.value ?? string.Empty;

            var success = await AuthService.Instance.Register(username, password);
            if (success)
            {
                status.text = "Account created.";
                resultSource.TrySetResult(true);
                return;
            }

            status.text = "Registration failed.";
        })
        {
            text = "Register",
        };
        registerButton.AddToClassList("overlay-button");
        buttonRow.Add(registerButton);

        content.Add(buttonRow);
        overlayContainer.Add(content);

        var result = await resultSource.Task;
        overlayContainer.AddToClassList("hidden");
        isBlockingOverlayOpen = false;
        return result;
    }

    private async Task<CharacterSummary> ShowCharacterSelectionOverlayAsync(CharacterSummary[] characters)
    {
        if (overlayContainer == null || AuthService.Instance == null)
            return null;

        var available = characters ?? Array.Empty<CharacterSummary>();
        CharacterSummary selected = AuthService.Instance.ActiveCharacter;
        if (selected == null && available.Length > 0)
            selected = available[0];

        var resultSource = new TaskCompletionSource<CharacterSummary>();
        isBlockingOverlayOpen = true;
        overlayContainer.Clear();
        overlayContainer.RemoveFromClassList("hidden");

        var content = new VisualElement();
        content.AddToClassList("overlay-content");

        var title = new Label("Select Character");
        title.AddToClassList("overlay-title");
        content.Add(title);

        var status = new Label("Choose a character or create a new one.");
        status.AddToClassList("overlay-status");
        content.Add(status);

        var list = new ScrollView();
        list.AddToClassList("overlay-list");
        content.Add(list);

        Action refreshList = null;
        refreshList = () =>
        {
            list.contentContainer.Clear();

            if (available.Length == 0)
            {
                list.contentContainer.Add(new Label("No characters available."));
                return;
            }

            foreach (var character in available)
            {
                var captured = character;
                var button = new Button(() =>
                {
                    selected = captured;
                    status.text = $"Selected: {captured.name}";
                    refreshList();
                })
                {
                    text = $"{captured.name} ({captured.startingZoneName})",
                };

                button.AddToClassList("character-button");
                if (selected != null && captured.id == selected.id)
                    button.AddToClassList("character-button-active");

                list.contentContainer.Add(button);
            }
        };

        refreshList();

        var createName = new TextField("New Character Name");
        createName.AddToClassList("overlay-input");
        content.Add(createName);

        var createZone = new TextField("Starting Zone")
        {
            value = defaultStartingZoneSlug,
        };
        createZone.AddToClassList("overlay-input");
        content.Add(createZone);

        var actions = new VisualElement();
        actions.AddToClassList("overlay-button-row");

        var createButton = new Button(async () =>
        {
            var newName = createName.value?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                status.text = "Enter a character name first.";
                return;
            }

            status.text = "Creating character...";
            var created = await AuthService.Instance.CreateCharacter(newName, createZone.value?.Trim());
            if (created == null)
            {
                status.text = "Character creation failed.";
                return;
            }

            var activated = await AuthService.Instance.SelectCharacter(created.id);
            resultSource.TrySetResult(activated ?? created);
        })
        {
            text = "Create",
        };
        createButton.AddToClassList("overlay-button");
        actions.Add(createButton);

        var enterButton = new Button(async () =>
        {
            if (selected == null)
            {
                status.text = "Select a character first.";
                return;
            }

            status.text = "Entering world...";
            var activated = await AuthService.Instance.SelectCharacter(selected.id);
            resultSource.TrySetResult(activated ?? selected);
        })
        {
            text = "Enter World",
        };
        enterButton.AddToClassList("overlay-button");
        actions.Add(enterButton);

        content.Add(actions);
        overlayContainer.Add(content);

        var result = await resultSource.Task;
        overlayContainer.AddToClassList("hidden");
        isBlockingOverlayOpen = false;
        return result;
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

    // ── Status Effects ────────────────────────────────────

    private void RenderStatusEffects(StatusEffect[] effects)
    {
        if (statusEffectsContainer == null)
            return;

        statusEffectsContainer.Clear();
        if (effects == null || effects.Length == 0)
            return;

        foreach (var effect in effects)
        {
            var badge = new Label($"{effect.name} ({effect.remainingTicks})");
            badge.AddToClassList("status-effect-tag");
            badge.tooltip = $"{effect.name}: {effect.remainingTicks} tick(s) remaining";
            statusEffectsContainer.Add(badge);
        }
    }

    // ── Ability Bar ───────────────────────────────────────

    private void BuildAbilityBar()
    {
        if (abilityBar == null)
            return;

        abilityBar.Clear();

        for (var i = 0; i < 5; i++)
        {
            var index = i;

            var slot = new VisualElement();
            slot.name = $"ability-slot-{i}";
            slot.AddToClassList("ability-slot");

            var keyLabel = new Label((i + 1).ToString());
            keyLabel.AddToClassList("ability-key");
            slot.Add(keyLabel);

            var nameLabel = new Label(DefaultAbilityNames[i]);
            nameLabel.AddToClassList("ability-name");
            abilityNameLabels[i] = nameLabel;
            slot.Add(nameLabel);

            slot.RegisterCallback<ClickEvent>(_ => TriggerAbilitySlot(index));
            abilityBar.Add(slot);
        }
    }

    private void TriggerAbilitySlot(int index)
    {
        if (index < 0 || index >= 5)
            return;

        if (EllmudNetworkManager.Instance == null || !EllmudNetworkManager.Instance.IsConnected)
            return;

        var slot = abilityBar?.Q<VisualElement>($"ability-slot-{index}");
        if (slot != null)
        {
            slot.AddToClassList("ability-slot--active");
            slot.schedule.Execute(() => slot.RemoveFromClassList("ability-slot--active"))
                .StartingIn(200);
        }

        var verb = DefaultAbilityCommands[index];
        EllmudNetworkManager.Instance.SendCommand(verb);
    }

    // ── Minimap ───────────────────────────────────────────

    private void BuildMinimap()
    {
        if (minimapContainer == null)
            return;

        minimapElement = new MinimapElement();
        minimapContainer.Add(minimapElement);
    }

    private void RefreshMinimap()
    {
        minimapElement?.UpdateMap(exploredRooms, currentExplorationRoomId);
    }

    // ── Inventory Overlay ─────────────────────────────────

    private void ShowInventoryOverlay()
    {
        if (overlayContainer == null)
            return;

        overlayContainer.Clear();
        overlayContainer.RemoveFromClassList("hidden");

        var content = new VisualElement();
        content.AddToClassList("overlay-content");
        content.style.maxWidth = 600;

        var title = new Label("Inventory");
        title.AddToClassList("overlay-title");
        content.Add(title);

        if (cachedInventory != null)
        {
            var weightLabel = new Label(
                $"Weight: {cachedInventory.currentWeight:F1} / {cachedInventory.maxWeight:F1}");
            weightLabel.AddToClassList("overlay-text");
            content.Add(weightLabel);
        }

        var list = new ScrollView();
        list.AddToClassList("overlay-list");
        list.style.maxHeight = 320;
        content.Add(list);

        if (cachedInventory?.items == null || cachedInventory.items.Length == 0)
        {
            var empty = new Label("Inventory is empty.");
            empty.AddToClassList("overlay-text");
            list.contentContainer.Add(empty);
        }
        else
        {
            foreach (var item in cachedInventory.items)
            {
                var row = new VisualElement();
                row.AddToClassList("inventory-item-row");

                var nameLabel = new Label(item.name ?? "Unknown");
                nameLabel.AddToClassList("inventory-item-name");
                row.Add(nameLabel);

                var metaLabel = new Label($"{item.tier} · {item.type} · {item.weight:F1} wt");
                metaLabel.AddToClassList("inventory-item-meta");
                row.Add(metaLabel);

                if (item.allowedSlots != null && item.allowedSlots.Length > 0)
                {
                    var capturedItem = item;
                    var equipBtn = new Button(() =>
                    {
                        EllmudNetworkManager.Instance?.SendMessage(
                            MessageTypes.EQUIP_ITEM,
                            new EquipItemMessage
                            {
                                itemId = capturedItem.instanceId,
                                targetSlot = capturedItem.allowedSlots[0],
                            });
                        overlayContainer.AddToClassList("hidden");
                    })
                    {
                        text = "Equip",
                    };
                    equipBtn.AddToClassList("inventory-equip-button");
                    row.Add(equipBtn);
                }

                list.contentContainer.Add(row);
            }
        }

        var closeBtn = new Button(() => overlayContainer.AddToClassList("hidden")) { text = "Close" };
        closeBtn.AddToClassList("overlay-button");
        content.Add(closeBtn);

        overlayContainer.Add(content);
    }

    // ── Stash Overlay ─────────────────────────────────────

    private void ShowStashOverlay()
    {
        if (overlayContainer == null)
            return;

        overlayContainer.Clear();
        overlayContainer.RemoveFromClassList("hidden");

        var content = new VisualElement();
        content.AddToClassList("overlay-content");
        content.style.maxWidth = 560;

        var title = new Label("Stash");
        title.AddToClassList("overlay-title");
        content.Add(title);

        var list = new ScrollView();
        list.AddToClassList("overlay-list");
        list.style.maxHeight = 320;
        content.Add(list);

        if (cachedStash?.items == null || cachedStash.items.Length == 0)
        {
            var empty = new Label("Stash is empty.");
            empty.AddToClassList("overlay-text");
            list.contentContainer.Add(empty);
        }
        else
        {
            foreach (var item in cachedStash.items)
            {
                var row = new VisualElement();
                row.AddToClassList("inventory-item-row");

                var nameLabel = new Label(item.name ?? "Unknown");
                nameLabel.AddToClassList("inventory-item-name");
                row.Add(nameLabel);

                var metaLabel = new Label($"{item.tier} · {item.type}");
                metaLabel.AddToClassList("inventory-item-meta");
                row.Add(metaLabel);

                list.contentContainer.Add(row);
            }
        }

        var closeBtn = new Button(() => overlayContainer.AddToClassList("hidden")) { text = "Close" };
        closeBtn.AddToClassList("overlay-button");
        content.Add(closeBtn);

        overlayContainer.Add(content);
    }

    // ── Loadout (Equipment) Overlay ───────────────────────

    private void ShowLoadoutOverlay()
    {
        if (overlayContainer == null)
            return;

        overlayContainer.Clear();
        overlayContainer.RemoveFromClassList("hidden");

        var content = new VisualElement();
        content.AddToClassList("overlay-content");
        content.style.maxWidth = 480;

        var title = new Label("Equipment");
        title.AddToClassList("overlay-title");
        content.Add(title);

        var slots = cachedLoadout?.slots;
        var slotsContainer = new VisualElement();
        content.Add(slotsContainer);

        void AddSlotRow(string slotLabel, string slotName, ItemData item)
        {
            var row = new VisualElement();
            row.AddToClassList("equipment-slot-row");

            var label = new Label(slotLabel);
            label.AddToClassList("equipment-slot-label");
            row.Add(label);

            var itemLabel = new Label(item != null ? $"{item.name} ({item.tier})" : "Empty");
            itemLabel.AddToClassList("equipment-slot-item");
            if (item == null)
                itemLabel.AddToClassList("equipment-slot-item--empty");
            row.Add(itemLabel);

            if (item != null)
            {
                var capturedSlot = slotName;
                var unequipBtn = new Button(() =>
                {
                    EllmudNetworkManager.Instance?.SendMessage(
                        MessageTypes.UNEQUIP_ITEM,
                        new UnequipItemMessage { slot = capturedSlot });
                    overlayContainer.AddToClassList("hidden");
                })
                {
                    text = "Unequip",
                };
                unequipBtn.AddToClassList("equipment-unequip-button");
                row.Add(unequipBtn);
            }

            slotsContainer.Add(row);
        }

        AddSlotRow("Head",     "head",    slots?.head);
        AddSlotRow("Chest",    "chest",   slots?.chest);
        AddSlotRow("Legs",     "legs",    slots?.legs);
        AddSlotRow("Feet",     "feet",    slots?.feet);
        AddSlotRow("Hands",    "hands",   slots?.hands);
        AddSlotRow("Weapon",   "weapon",  slots?.weapon);
        AddSlotRow("Off-Hand", "offhand", slots?.offhand);
        AddSlotRow("Ring 1",   "ring1",   slots?.ring1);
        AddSlotRow("Ring 2",   "ring2",   slots?.ring2);
        AddSlotRow("Amulet",   "amulet",  slots?.amulet);

        var closeBtn = new Button(() => overlayContainer.AddToClassList("hidden")) { text = "Close" };
        closeBtn.AddToClassList("overlay-button");
        content.Add(closeBtn);

        overlayContainer.Add(content);
    }
}