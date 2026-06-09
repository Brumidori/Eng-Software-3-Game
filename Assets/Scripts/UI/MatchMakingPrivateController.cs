using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.MultiplayerModels;
using TMPro;

public class MatchMakingPrivateController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text gameCodeText;
    [SerializeField] private Button btnCloseRoom;
    [SerializeField] private Image playerSkinImage;
    [SerializeField] private TMP_Text statusText;

    [Header("Configuração")]
    [SerializeField] private string cenaHomeScreen = "HomeScreen";
    [SerializeField] private string cenaPartida = "BrainDuelArena";
    [SerializeField] private int contagemRegressiva = 3;
    [SerializeField] private string queueName = "BrainDuelPrivateQueue";

    [Header("Avatar Sprites Fallback")]
    [SerializeField] private List<NamedSprite> avatarSprites = new List<NamedSprite>();

    private const string TextoAguardando = "AGUARDANDO OPONENTE...";
    private const string TextoEncontrado = "OPONENTE ENCONTRADO!\nINICIANDO EM {0}...";

    private Coroutine _contagemCoroutine;

    private void Awake()
    {
        if (btnCloseRoom != null)
            btnCloseRoom.onClick.AddListener(OnCloseRoomClick);
    }

    private void OnEnable()
    {
        MatchmakingService.OnMatchFound += HandleMatchFound;
        MatchmakingService.OnMatchmakingFailed += HandleMatchFailed;
    }

    private void OnDisable()
    {
        MatchmakingService.OnMatchFound -= HandleMatchFound;
        MatchmakingService.OnMatchmakingFailed -= HandleMatchFailed;
        
        if (btnCloseRoom != null)
            btnCloseRoom.onClick.RemoveListener(OnCloseRoomClick);

        StopAllCoroutines();
    }

    private void Start()
    {
        // Bind Game Code
        Debug.Log($"[MatchMakingPrivate] MatchmakingService exists: {MatchmakingService.Instance != null}");
        if (MatchmakingService.Instance != null)
        {
            Debug.Log($"[MatchMakingPrivate] CurrentRoomCode is: '{MatchmakingService.Instance.CurrentRoomCode}'");
        }

        if (gameCodeText != null)
        {
            gameCodeText.text = MatchmakingService.Instance?.CurrentRoomCode ?? "------";
            Debug.Log($"[MatchMakingPrivate] Set text to: {gameCodeText.text}");
        }
        else
        {
            Debug.LogWarning("[MatchMakingPrivate] gameCodeText object is null (not assigned in Inspector).");
        }

        // Bind Player Skin
        BindPlayerSkin();

        // Status Text
        if (statusText != null)
        {
            statusText.text = TextoAguardando;
        }
    }

    private void BindPlayerSkin()
    {
        if (playerSkinImage == null)
        {
            Debug.LogWarning("[MatchMakingPrivate] playerSkinImage is null!");
            return;
        }

        var profile = PlayerDataService.Instance?.CurrentProfile;
        if (profile != null && !string.IsNullOrWhiteSpace(profile.avatarId))
        {
            ApplySkin(profile.avatarId);
        }
        else
        {
            Debug.LogWarning("[MatchMakingPrivate] Profile é nulo. Buscando diretamente do PlayFab...");
            PlayFabClientAPI.GetUserData(
                new GetUserDataRequest { Keys = new List<string> { "player_profile" } },
                result =>
                {
                    if (result?.Data != null && result.Data.TryGetValue("player_profile", out var record) && !string.IsNullOrWhiteSpace(record.Value))
                    {
                        var profileData = JsonUtility.FromJson<PlayerProfileData>(record.Value);
                        if (profileData != null && !string.IsNullOrWhiteSpace(profileData.avatarId))
                        {
                            ApplySkin(profileData.avatarId);
                            return;
                        }
                    }
                    ApplySkin("skinDefault"); // fallback caso n tenha nada
                },
                error =>
                {
                    Debug.LogError("[MatchMakingPrivate] Erro ao buscar perfil: " + error.ErrorMessage);
                    ApplySkin("skinDefault");
                }
            );
        }
    }

    private void ApplySkin(string avatarId)
    {
        Debug.Log($"[MatchMakingPrivate] Tentando carregar skin: {avatarId}");
        var sprite = Resources.Load<Sprite>($"AvatarImages/{avatarId}");
        
        if (sprite == null && avatarSprites != null)
        {
            Debug.Log($"[MatchMakingPrivate] Skin não achada no Resources. Procurando nos {avatarSprites.Count} fallbacks...");
            foreach (var namedSprite in avatarSprites)
            {
                if (namedSprite != null && namedSprite.IsMatch(avatarId))
                {
                    sprite = namedSprite.sprite;
                    Debug.Log($"[MatchMakingPrivate] Encontrado no fallback! Sprite: {sprite.name}");
                    break;
                }
            }
        }

        if (sprite != null)
        {
            playerSkinImage.sprite = sprite;
            Debug.Log("[MatchMakingPrivate] Skin aplicada com sucesso na UI.");
        }
        else
        {
            Debug.LogWarning("[MatchMakingPrivate] Nenhum sprite correspondente foi encontrado para o avatarId!");
        }
    }

    private void OnCloseRoomClick()
    {
        MatchmakingService.Instance?.CancelCurrentSearch();
        SceneManager.LoadScene(cenaHomeScreen);
    }

    private void HandleMatchFailed(PlayFabError error)
    {
        if (statusText != null)
        {
            statusText.text = "ERRO NA SALA PRIVADA. VOLTANDO...";
        }
        Invoke(nameof(OnCloseRoomClick), 3f);
    }

    private void HandleMatchFound(string matchId)
    {
        BrainDuel.Match.Core.MatchSessionData.MatchId = matchId;
        BrainDuel.Match.Core.MatchSessionData.LocalPlayerId = PlayFabSettings.staticPlayer?.PlayFabId;
        
        _contagemCoroutine = StartCoroutine(BuscarNomeEIniciar());
    }

    private IEnumerator BuscarNomeEIniciar()
    {
        string opponentPlayFabId = string.Empty;
        bool matchFetched = false;

        PlayFabMultiplayerAPI.GetMatch(
            new GetMatchRequest
            {
                QueueName = queueName,
                MatchId = BrainDuel.Match.Core.MatchSessionData.MatchId,
                ReturnMemberAttributes = true
            },
            result =>
            {
                BrainDuel.Match.Core.MatchSessionData.IsRealMatch = true;
                var myEntityId = PlayFabSettings.staticPlayer?.EntityId ?? string.Empty;
                
                if (result.Members != null)
                {
                    foreach (var m in result.Members)
                    {
                        if (m.Entity != null && m.Entity.Id != myEntityId)
                        {
                            try
                            {
                                var raw = PlayFab.Json.PlayFabSimpleJson.SerializeObject(m.Attributes?.DataObject);
                                var attrs = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(raw);
                                if (attrs != null && attrs.TryGetValue("PlayFabId", out var pfId))
                                {
                                    opponentPlayFabId = pfId?.ToString() ?? string.Empty;
                                }
                            }
                            catch { }
                            break;
                        }
                    }
                }
                matchFetched = true;
            },
            _ => { matchFetched = true; }
        );

        while (!matchFetched) yield return null;

        if (BrainDuel.Match.Core.MatchSessionData.IsRealMatch)
        {
            string localPlayFabId = PlayFabSettings.staticPlayer?.PlayFabId ?? string.Empty;
            bool matchCreated = false;
            
            PlayFabClientAPI.ExecuteCloudScript(
                new ExecuteCloudScriptRequest
                {
                    FunctionName = "CreateMatch",
                    FunctionParameter = new
                    {
                        matchId = BrainDuel.Match.Core.MatchSessionData.MatchId,
                        player1Id = localPlayFabId,
                        player2Id = opponentPlayFabId
                    },
                    GeneratePlayStreamEvent = false
                },
                _ => matchCreated = true,
                err => { matchCreated = true; }
            );

            while (!matchCreated) yield return null;
        }

        bool doneInfo = false;
        PlayFabClientAPI.GetAccountInfo(
            new GetAccountInfoRequest(),
            result =>
            {
                var name = result?.AccountInfo?.TitleInfo?.DisplayName;
                if (!string.IsNullOrEmpty(name))
                    BrainDuel.Match.Core.MatchSessionData.LocalDisplayName = name;

                var profile = PlayerDataService.Instance?.CurrentProfile;
                if (profile != null && profile.level > 0)
                    BrainDuel.Match.Core.MatchSessionData.LocalLevel = profile.level;

                doneInfo = true;
            },
            _ => doneInfo = true
        );

        while (!doneInfo) yield return null;

        yield return ContagemRegressiva();
    }

    private IEnumerator ContagemRegressiva()
    {
        for (int i = contagemRegressiva; i > 0; i--)
        {
            if (statusText != null)
                statusText.text = string.Format(TextoEncontrado, i);
            yield return new WaitForSeconds(1f);
        }
        SceneManager.LoadScene(cenaPartida);
    }
}
