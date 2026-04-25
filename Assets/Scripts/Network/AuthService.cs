using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class AuthService : MonoBehaviour
{
    public static AuthService Instance { get; private set; }

    [SerializeField] private string serverUrl = "http://localhost:2567";

    public string Token { get; private set; }
    public string Username { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action OnLogout;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async Task<bool> Register(string username, string password)
    {
        var body = JsonUtility.ToJson(new AuthRequest
        {
            username = username,
            password = password
        });

        using var request = new UnityWebRequest(
            $"{serverUrl}/auth/register", "POST");
        request.uploadHandler = new UploadHandlerRaw(
            Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            OnLoginFailed?.Invoke(
                $"Registration failed: {request.downloadHandler.text}");
            return false;
        }
        // Auto-login after successful registration
        return await Login(username, password);
    }

    public async Task<bool> Login(string username, string password)
    {
        var body = JsonUtility.ToJson(new AuthRequest
        {
            username = username,
            password = password
        });

        using var request = new UnityWebRequest(
            $"{serverUrl}/auth/login", "POST");
        request.uploadHandler = new UploadHandlerRaw(
            Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            OnLoginFailed?.Invoke(
                $"Login failed: {request.downloadHandler.text}");
            return false;
        }

        var response = JsonUtility.FromJson<AuthResponse>(
            request.downloadHandler.text);
        Token = response.token;
        Username = username;

        OnLoginSuccess?.Invoke();
        return true;
    }

    public async Task Logout()
    {
        if (!IsAuthenticated) return;

        using var request = new UnityWebRequest(
            $"{serverUrl}/auth/logout", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", $"Bearer {Token}");

        await request.SendWebRequest();

        Token = null;
        Username = null;
        OnLogout?.Invoke();
    }

    [Serializable]
    private class AuthRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class AuthResponse
    {
        public string token;
    }
}