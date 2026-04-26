using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drives the room background visual layer.
/// Subscribes to <see cref="GameEvents.OnRoomHeader"/> and cross-fades the
/// background to a themed color on every room transition.
/// Add this component to the same GameObject as <see cref="GameHUDController"/>.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class RoomBackgroundController : MonoBehaviour
{
    // ── Room Themes ─────────────────────────────────────────────────────────

    private readonly struct RoomTheme
    {
        public readonly string CssClass;
        public readonly string Icon;
        public readonly string DisplayName;

        public RoomTheme(string cssClass, string icon, string displayName)
        {
            CssClass = cssClass;
            Icon = icon;
            DisplayName = displayName;
        }
    }

    private static readonly Dictionary<string, RoomTheme> Themes = new()
    {
        { "entry",                    new RoomTheme("room-entry",     "⛩",  "Entry Hall")       },
        { "corridor",                 new RoomTheme("room-corridor",  "░",  "Corridor")          },
        { "junction",                 new RoomTheme("room-junction",  "✦",  "Junction")          },
        { "dead_end",                 new RoomTheme("room-dead-end",  "☠",  "Dead End")          },
        { "boss",                     new RoomTheme("room-boss",      "⚔",  "Boss Chamber")      },
        { "feature_stash",            new RoomTheme("room-stash",     "⚑",  "Stash Room")        },
        { "feature_expedition_board", new RoomTheme("room-board",     "◈",  "Expedition Board")  },
        { "feature_marketplace",      new RoomTheme("room-market",    "⊕",  "Marketplace")       },
        { "feature_crafting",         new RoomTheme("room-crafting",  "⚒",  "Forge")             },
        { "feature_training",         new RoomTheme("room-training",  "⚔",  "Training Grounds")  },
        { "feature_contracts",        new RoomTheme("room-contracts", "◉",  "Contract Board")    },
        { "feature_infirmary",        new RoomTheme("room-infirmary", "✚",  "Infirmary")         },
        { "feature_armoury",          new RoomTheme("room-armoury",   "⛨",  "Armoury")           },
        { "feature_war_room",         new RoomTheme("room-war",       "◆",  "War Room")          },
        { "feature_inn",              new RoomTheme("room-inn",       "◌",  "Inn")               },
        { "feature_sandbox",          new RoomTheme("room-sandbox",   "⚙",  "Sandbox")           },
        { "feature_sandbox_arena",    new RoomTheme("room-arena",     "⚔",  "Combat Arena")      },
        { "feature_sandbox_stats",    new RoomTheme("room-stats",     "◎",  "Stats Lab")         },
    };

    private static readonly RoomTheme DefaultTheme = new RoomTheme("room-unknown", "?", string.Empty);

    // ── State ────────────────────────────────────────────────────────────────

    private VisualElement backgroundElement;
    private VisualElement fadeOverlay;
    private Label roomTypeBadge;
    private Label roomTypeLabel;

    private bool transitioning;
    private RoomHeaderMessage pendingRoom;
    private string currentTypeClass;

    // ── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        var uiDocument = GetComponent<UIDocument>();

        // Apply this unconditionally, before any rootVisualElement null-check.
        // Unity sets a camera-capturing lambda on PanelSettings in ScreenSpaceOverlay mode;
        // when that camera reference is null the lambda throws every mouse-move frame.
        // An identity function is correct for screen-space overlays (screen == panel coords).
        uiDocument?.panelSettings?.SetScreenToPanelSpaceFunction(p => p);

        if (uiDocument?.rootVisualElement == null)
            return;

        var root = uiDocument.rootVisualElement;
        backgroundElement = root.Q<VisualElement>("room-background");
        fadeOverlay       = root.Q<VisualElement>("room-fade-overlay");
        roomTypeBadge     = root.Q<Label>("room-type-badge");
        roomTypeLabel     = root.Q<Label>("room-type-label");
    }

    private void OnEnable()
    {
        GameEvents.OnRoomHeader += HandleRoomHeader;
    }

    private void OnDisable()
    {
        GameEvents.OnRoomHeader -= HandleRoomHeader;
    }

    // ── Event Handler ────────────────────────────────────────────────────────

    private void HandleRoomHeader(RoomHeaderMessage msg)
    {
        if (backgroundElement == null || fadeOverlay == null)
            return;

        if (transitioning)
        {
            // Queue the most recent room; drop anything in between
            pendingRoom = msg;
            return;
        }

        StartTransition(msg);
    }

    // ── Transition Logic ─────────────────────────────────────────────────────

    private void StartTransition(RoomHeaderMessage msg)
    {
        transitioning = true;
        pendingRoom = null;

        // Phase 1 — fade to black (300 ms)
        fadeOverlay.RemoveFromClassList("room-fade-out");
        fadeOverlay.AddToClassList("room-fade-in");

        // Phase 2 — swap theme while screen is black, then reveal (300 ms)
        fadeOverlay.schedule.Execute(() =>
        {
            ApplyTheme(msg);

            fadeOverlay.RemoveFromClassList("room-fade-in");
            fadeOverlay.AddToClassList("room-fade-out");

            // Phase 3 — after reveal, check for queued room
            fadeOverlay.schedule.Execute(() =>
            {
                transitioning = false;

                if (pendingRoom != null)
                {
                    var next = pendingRoom;
                    pendingRoom = null;
                    StartTransition(next);
                }
            }).StartingIn(350);

        }).StartingIn(350);
    }

    private void ApplyTheme(RoomHeaderMessage msg)
    {
        var roomType = msg?.roomType ?? string.Empty;
        var theme = Themes.TryGetValue(roomType, out var t) ? t : DefaultTheme;

        // Swap the type class on the background element
        if (!string.IsNullOrEmpty(currentTypeClass))
            backgroundElement.RemoveFromClassList(currentTypeClass);

        currentTypeClass = theme.CssClass;
        backgroundElement.AddToClassList(currentTypeClass);

        // Update the room type badge text
        if (roomTypeBadge != null)
            roomTypeBadge.text = theme.Icon;

        if (roomTypeLabel != null)
            roomTypeLabel.text = theme.DisplayName;
    }
}
