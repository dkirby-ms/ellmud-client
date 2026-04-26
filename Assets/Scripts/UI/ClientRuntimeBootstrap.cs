using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Ensures required runtime objects exist so the client can boot in a fresh scene.
/// </summary>
public static class ClientRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureComponent<AuthService>("AuthService");
        EnsureComponent<EllmudNetworkManager>("EllmudNetworkManager");
        EnsureComponent<GameStateManager>("GameStateManager");

        if (Object.FindAnyObjectByType<GameHUDController>() != null)
            return;

        var panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
        var visualTree = Resources.Load<VisualTreeAsset>("UI/GameHUD");

        if (panelSettings == null || visualTree == null)
        {
            Debug.LogWarning("[Ellmud] Missing Resources/UI/GameHUD or Resources/UI/PanelSettings for auto-bootstrap.");
            return;
        }

        var go = new GameObject("GameHUD");
        Object.DontDestroyOnLoad(go);

        var uiDocument = go.AddComponent<UIDocument>();
        uiDocument.panelSettings = panelSettings;
        uiDocument.visualTreeAsset = visualTree;

        go.AddComponent<GameHUDController>();
        go.AddComponent<ClientSmokeHarness>();
    }

    private static void EnsureComponent<T>(string objectName) where T : Component
    {
        if (Object.FindAnyObjectByType<T>() != null)
            return;

        var go = new GameObject(objectName);
        Object.DontDestroyOnLoad(go);
        go.AddComponent<T>();
    }
}
