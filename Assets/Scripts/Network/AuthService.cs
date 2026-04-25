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
    public string PlayerId { get; private set; }
    public string Username { get; private set; }
    public string Role { get; private set; } = "player";
    public CharacterSummary[] Characters { get; private set; } = Array.Empty<CharacterSummary>();
    public CharacterSummary ActiveCharacter { get; private set; }
    public SpawnZoneInfo LastSpawnZone { get; private set; }
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
        try
        {
            var response = await SendRequest<AuthResponse>(
                "/auth/register",
                UnityWebRequest.kHttpVerbPOST,
                new AuthRequest
                {
                    username = username,
                    password = password,
                });

            ApplyAuthResponse(response, username);
            await RefreshProfile();
            OnLoginSuccess?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnLoginFailed?.Invoke($"Registration failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> Login(string username, string password)
    {
        try
        {
            var response = await SendRequest<AuthResponse>(
                "/auth/login",
                UnityWebRequest.kHttpVerbPOST,
                new AuthRequest
                {
                    username = username,
                    password = password,
                });

            ApplyAuthResponse(response, username);
            await RefreshProfile();
            OnLoginSuccess?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnLoginFailed?.Invoke($"Login failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads the authenticated player's profile from <c>/auth/me</c>.
    /// </summary>
    /// <returns>The current authenticated profile.</returns>
    public async Task<AuthProfile> RefreshProfile()
    {
        EnsureAuthenticated();

        var response = await SendRequest<AuthProfileResponse>(
            "/auth/me",
            UnityWebRequest.kHttpVerbGET,
            authorize: true);

        PlayerId = response.playerId;
        Username = string.IsNullOrEmpty(response.username)
            ? Username
            : response.username;
        Role = string.IsNullOrEmpty(response.role)
            ? "player"
            : response.role;

        return new AuthProfile
        {
            playerId = PlayerId,
            username = Username,
            role = Role,
        };
    }

    /// <summary>
    /// Lists all server-authoritative characters for the authenticated player.
    /// </summary>
    /// <returns>The full character list.</returns>
    public async Task<CharacterSummary[]> FetchCharacters()
    {
        EnsureAuthenticated();

        var response = await SendRequest<CharacterListResponse>(
            "/api/characters",
            UnityWebRequest.kHttpVerbGET,
            authorize: true);

        Characters = response?.characters ?? Array.Empty<CharacterSummary>();
        ActiveCharacter = FindActiveCharacter(Characters);
        return Characters;
    }

    /// <summary>
    /// Creates a new character through the server REST API.
    /// </summary>
    /// <param name="name">The character name.</param>
    /// <param name="startingZoneSlug">The selected starting zone slug.</param>
    /// <returns>The created character summary.</returns>
    public async Task<CharacterSummary> CreateCharacter(
        string name,
        string startingZoneSlug)
    {
        EnsureAuthenticated();

        var response = await SendRequest<CharacterResponse>(
            "/api/characters",
            UnityWebRequest.kHttpVerbPOST,
            new CharacterCreateMessage
            {
                name = name,
                startingZoneSlug = startingZoneSlug,
            },
            authorize: true);

        await FetchCharacters();
        return FindCharacter(response.character?.id) ?? response.character;
    }

    /// <summary>
    /// Selects the active character through the server REST API.
    /// </summary>
    /// <param name="characterId">The character identifier to activate.</param>
    /// <returns>The selected character summary.</returns>
    public async Task<CharacterSummary> SelectCharacter(string characterId)
    {
        EnsureAuthenticated();

        await SendRequest<MessageResponse>(
            $"/api/characters/{characterId}/select",
            UnityWebRequest.kHttpVerbPUT,
            authorize: true);

        var characters = await FetchCharacters();
        return FindCharacter(characterId) ?? FindActiveCharacter(characters);
    }

    /// <summary>
    /// Deletes a character through the server REST API.
    /// </summary>
    /// <param name="characterId">The character identifier to delete.</param>
    public async Task DeleteCharacter(string characterId)
    {
        EnsureAuthenticated();

        await SendRequest<MessageResponse>(
            $"/api/characters/{characterId}",
            UnityWebRequest.kHttpVerbDELETE,
            authorize: true);

        await FetchCharacters();
    }

    /// <summary>
    /// Resolves the server-authoritative spawn-zone bootstrap target.
    /// </summary>
    /// <returns>The zone bootstrap target for world entry.</returns>
    public async Task<SpawnZoneInfo> FetchSpawnZone()
    {
        EnsureAuthenticated();

        LastSpawnZone = await SendRequest<SpawnZoneInfo>(
            "/api/spawn-zone",
            UnityWebRequest.kHttpVerbGET,
            authorize: true);

        return LastSpawnZone;
    }

    public async Task Logout()
    {
        if (!IsAuthenticated) return;

        await SendRequest<MessageResponse>(
            "/auth/logout",
            UnityWebRequest.kHttpVerbPOST,
            authorize: true,
            throwOnFailure: false);

        Token = null;
        PlayerId = null;
        Username = null;
        Role = "player";
        Characters = Array.Empty<CharacterSummary>();
        ActiveCharacter = null;
        LastSpawnZone = null;
        OnLogout?.Invoke();
    }

    private void ApplyAuthResponse(AuthResponse response, string fallbackUsername)
    {
        Token = response.token;
        PlayerId = response.playerId;
        Username = string.IsNullOrEmpty(response.username)
            ? fallbackUsername
            : response.username;
        Role = "player";
    }

    private CharacterSummary FindActiveCharacter(CharacterSummary[] characters)
    {
        if (characters == null)
            return null;

        foreach (var character in characters)
        {
            if (character != null && character.isActive)
                return character;
        }

        return characters.Length > 0 ? characters[0] : null;
    }

    private CharacterSummary FindCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || Characters == null)
            return null;

        foreach (var character in Characters)
        {
            if (character != null && character.id == characterId)
                return character;
        }

        return null;
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("Authentication required.");
    }

    private async Task<TResponse> SendRequest<TResponse>(
        string path,
        string method,
        object body = null,
        bool authorize = false,
        bool throwOnFailure = true)
    {
        using var request = new UnityWebRequest($"{serverUrl}{path}", method);

        if (body != null)
        {
            var json = JsonUtility.ToJson(body);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        request.downloadHandler = new DownloadHandlerBuffer();

        if (authorize)
            request.SetRequestHeader("Authorization", $"Bearer {Token}");

        await request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            if (!throwOnFailure)
                return default;

            throw new InvalidOperationException(FormatErrorMessage(request));
        }

        var responseText = request.downloadHandler?.text;
        if (string.IsNullOrWhiteSpace(responseText))
            return default;

        return JsonUtility.FromJson<TResponse>(responseText);
    }

    private string FormatErrorMessage(UnityWebRequest request)
    {
        var responseText = request.downloadHandler?.text;
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            var errorResponse = JsonUtility.FromJson<ErrorResponse>(responseText);
            if (!string.IsNullOrWhiteSpace(errorResponse?.error))
                return errorResponse.error;

            return responseText;
        }

        return string.IsNullOrWhiteSpace(request.error)
            ? $"Request failed with status {request.responseCode}."
            : request.error;
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
        public string playerId;
        public string username;
        public string email;
    }

    [Serializable]
    public class AuthProfile
    {
        public string playerId;
        public string username;
        public string role;
    }

    [Serializable]
    private class AuthProfileResponse
    {
        public string playerId;
        public string username;
        public string role;
    }

    [Serializable]
    private class CharacterResponse
    {
        public CharacterSummary character;
    }

    [Serializable]
    private class MessageResponse
    {
        public string message;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string error;
    }

    [Serializable]
    public class SpawnZoneInfo
    {
        public string target;
        public string zoneSlug;
        public string factionSlug;
    }
}