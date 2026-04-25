#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

internal static class PlayableBootstrapSceneCreator
{
    private const string SceneFolderPath = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/PlayableBootstrap.unity";
    private const string PanelSettingsPath = "Assets/UI/PanelSettings.asset";
    private const string GameHudPath = "Assets/UI/GameHUD.uxml";

    [MenuItem("Ellmud/Create Playable Bootstrap Scene")]
    private static void CreatePlayableBootstrapScene()
    {
        Directory.CreateDirectory(SceneFolderPath);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateSystemObject<AuthService>("AuthService");
        CreateSystemObject<EllmudNetworkManager>("EllmudNetworkManager");
        CreateSystemObject<GameStateManager>("GameStateManager");

        var hud = new GameObject("GameHUD");
        var uiDocument = hud.AddComponent<UIDocument>();
        uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GameHudPath);
        hud.AddComponent<GameHUDController>();
        hud.AddComponent<ClientSmokeHarness>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[Ellmud] Created playable bootstrap scene at {ScenePath}");
    }

    private static void CreateSystemObject<T>(string objectName) where T : Component
    {
        var go = new GameObject(objectName);
        go.AddComponent<T>();
    }
}
#endif
