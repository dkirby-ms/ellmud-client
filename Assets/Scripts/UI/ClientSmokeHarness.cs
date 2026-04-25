using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lightweight in-editor/runtime smoke checks for login -> world entry -> command -> reconnect.
/// </summary>
public class ClientSmokeHarness : MonoBehaviour
{
    [SerializeField] private bool autoRunOnStart;
    [SerializeField] private string username = "";
    [SerializeField] private string password = "";
    [SerializeField] private bool registerIfLoginFails = false;
    [SerializeField] private string startingZoneSlug = "the-reliquary";

    private async void Start()
    {
        if (autoRunOnStart)
            await RunSmokeTest();
    }

    [ContextMenu("Run Client Smoke Test")]
    public async Task RunSmokeTest()
    {
        try
        {
            await ExecuteSmokeFlow();
            Debug.Log("[Ellmud Smoke] PASS");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Ellmud Smoke] FAIL: {ex.Message}");
        }
    }

    private async Task ExecuteSmokeFlow()
    {
        if (AuthService.Instance == null)
            throw new InvalidOperationException("AuthService not found.");
        if (EllmudNetworkManager.Instance == null)
            throw new InvalidOperationException("EllmudNetworkManager not found.");

        if (!AuthService.Instance.IsAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Not authenticated and no smoke credentials configured.");

            var loginOk = await AuthService.Instance.Login(username, password);
            if (!loginOk && registerIfLoginFails)
                loginOk = await AuthService.Instance.Register(username, password);

            if (!loginOk)
                throw new InvalidOperationException("Unable to authenticate smoke user.");
        }

        var characters = await AuthService.Instance.FetchCharacters();
        if (characters == null || characters.Length == 0)
        {
            var generatedName = $"Smoker{UnityEngine.Random.Range(100, 999)}";
            await AuthService.Instance.CreateCharacter(generatedName, startingZoneSlug);
            characters = await AuthService.Instance.FetchCharacters();
        }

        if (characters == null || characters.Length == 0)
            throw new InvalidOperationException("No character available for smoke flow.");

        var active = AuthService.Instance.ActiveCharacter ?? characters[0];
        await AuthService.Instance.SelectCharacter(active.id);
        await EllmudNetworkManager.Instance.EnterActiveCharacterWorld(active.id);

        if (!EllmudNetworkManager.Instance.IsConnected)
            throw new InvalidOperationException("Failed to connect after world entry.");

        EllmudNetworkManager.Instance.SendCommand("look");
        await Task.Delay(200);

        await EllmudNetworkManager.Instance.LeaveCurrentRoom();
        await Task.Delay(200);

        var reconnected = await EllmudNetworkManager.Instance.ReconnectToLastRoom(2);
        if (!reconnected)
            throw new InvalidOperationException("Reconnect check failed.");
    }
}
