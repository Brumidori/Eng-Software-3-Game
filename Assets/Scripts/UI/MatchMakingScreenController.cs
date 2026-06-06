using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.MultiplayerModels;

public class MatchMakingScreenController : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private string cenaHomeScreen     = "HomeScreen";
    [SerializeField] private string cenaPartida        = "BrainDuelArena";
    [SerializeField] private int    contagemRegressiva = 3;

    [Header("Matchmaking")]
    [SerializeField] private string queueName            = "BrainDuelPublicQueue";
    [SerializeField] private int    timeoutSeconds       = 60;
    [SerializeField] private float  pollIntervalSeconds  = 3f;

    [Header("Visual")]
    [SerializeField] private int    tamanhoFonte = 48;
    [SerializeField] private Button btnCancelar;

    private const string TextoBuscando   = "BUSCANDO OPONENTE";
    private const string TextoEncontrado = "OPONENTE ENCONTRADO!\nINICIANDO PARTIDA EM {0}...";
    private const string TextoCancelado  = "BUSCA CANCELADA.";
    private const string TextoErro       = "ERRO AO BUSCAR PARTIDA.\nTENTE NOVAMENTE.";

    private Text      _statusText;
    private Button    _btnCancelar;
    private Coroutine _dotsCoroutine;
    private Coroutine _contagemCoroutine;

    // ────────────────────────────────────────────────────────
    // Lifecycle
    // ────────────────────────────────────────────────────────

    private void Awake()
    {
        CriarTextoStatus();
        _btnCancelar = btnCancelar;
    }

    private void OnEnable()
    {
        MatchmakingService.OnStateChanged      += HandleStateChanged;
        MatchmakingService.OnMatchFound        += HandleMatchFound;
        MatchmakingService.OnMatchmakingFailed += HandleMatchFailed;

        if (_btnCancelar != null)
            _btnCancelar.onClick.AddListener(OnCancelarClick);
    }

    private void OnDisable()
    {
        MatchmakingService.OnStateChanged      -= HandleStateChanged;
        MatchmakingService.OnMatchFound        -= HandleMatchFound;
        MatchmakingService.OnMatchmakingFailed -= HandleMatchFailed;

        if (_btnCancelar != null)
            _btnCancelar.onClick.RemoveListener(OnCancelarClick);

        StopAllCoroutines();
    }

    private void Start()
    {
        SetBuscando();

        var service = MatchmakingService.Instance;
        if (service == null)
        {
            var go = new GameObject("MatchmakingService");
            service = go.AddComponent<MatchmakingService>();
        }

        Debug.Log($"[MatchMaking] Iniciando com timeoutSeconds={timeoutSeconds} (valor do Inspector)");
        service.StartSinglePlayerMatchmaking(queueName, timeoutSeconds, pollIntervalSeconds);
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
            MatchmakingService.Instance?.SimularMatchEncontrado();
    }
#endif

    // ────────────────────────────────────────────────────────
    // Criação automática do texto de status
    // ────────────────────────────────────────────────────────

    private void CriarTextoStatus()
    {
        // Busca o Canvas da própria cena ativa, ignorando DontDestroyOnLoad
        Canvas canvas = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.gameObject.scene == gameObject.scene)
            {
                canvas = c;
                break;
            }
        }

        if (canvas == null)
        {
            Debug.LogError("[MatchMaking] Nenhum Canvas encontrado na cena.");
            return;
        }

        // Container com fundo azul escuro transparente
        var container = new GameObject("TxtStatusMatchmaking");
        container.transform.SetParent(canvas.transform, false);

        var containerRt = container.AddComponent<RectTransform>();
        containerRt.anchorMin        = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax        = new Vector2(0.5f, 0.5f);
        containerRt.pivot            = new Vector2(0.5f, 0.5f);
        containerRt.anchoredPosition = Vector2.zero;
        containerRt.sizeDelta        = new Vector2(740f, 120f);

        var bg = container.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.1f, 0.25f, 0.75f);
        bg.raycastTarget = false;

        // Texto filho do container
        var go = new GameObject("Label");
        go.transform.SetParent(container.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = new Vector2(16f, 8f);
        rt.offsetMax  = new Vector2(-16f, -8f);

        // Componente Text
        _statusText                    = go.AddComponent<Text>();
        _statusText.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusText.fontSize           = tamanhoFonte;
        _statusText.fontStyle          = FontStyle.Bold;
        _statusText.alignment          = TextAnchor.MiddleCenter;
        _statusText.color              = Color.white;
        _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _statusText.verticalOverflow   = VerticalWrapMode.Overflow;
        _statusText.raycastTarget      = false;

        // Sombra leve para legibilidade
        var shadow            = go.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);

        container.transform.SetAsLastSibling();
    }

    // ────────────────────────────────────────────────────────
    // Handlers
    // ────────────────────────────────────────────────────────

    private void HandleStateChanged(MatchmakingService.MatchmakingState state)
    {
        switch (state)
        {
            case MatchmakingService.MatchmakingState.Searching:
                SetBuscando();
                break;
            case MatchmakingService.MatchmakingState.Cancelled:
                StopAnimations();
                SetTexto(TextoCancelado);
                break;
            case MatchmakingService.MatchmakingState.TimedOut:
            case MatchmakingService.MatchmakingState.Failed:
                StopAnimations();
                SetTexto(TextoErro);
                break;
        }
    }

    private void HandleMatchFound(string matchId)
    {
        BrainDuel.Match.Core.MatchSessionData.MatchId       = matchId;
        // PlayFabId clássico — necessário para o CloudScript V1 identificar corretamente o jogador
        BrainDuel.Match.Core.MatchSessionData.LocalPlayerId = PlayFab.PlayFabSettings.staticPlayer?.PlayFabId;
        // IsRealMatch é definido em BuscarNomeEIniciar após GetMatch confirmar a partida real

        StopAnimations();
        _contagemCoroutine = StartCoroutine(BuscarNomeEIniciar());
    }

    private IEnumerator BuscarNomeEIniciar()
    {
        // 1. Verifica se é partida real consultando o PlayFab Matchmaking.
        //    ReturnMemberAttributes=true para recuperar o PlayFabId clássico do oponente.
        string opponentPlayFabId = string.Empty;
        bool   matchFetched      = false;

        PlayFabMultiplayerAPI.GetMatch(
            new GetMatchRequest
            {
                QueueName              = queueName,
                MatchId                = BrainDuel.Match.Core.MatchSessionData.MatchId,
                ReturnMemberAttributes = true
            },
            result =>
            {
                // GetMatch bem-sucedido → partida real com oponente
                BrainDuel.Match.Core.MatchSessionData.IsRealMatch = true;

                // Usa EntityId apenas para identificar quem é o oponente na lista
                var myEntityId = PlayFabSettings.staticPlayer?.EntityId ?? string.Empty;
                if (result.Members != null)
                    foreach (var m in result.Members)
                        if (m.Entity != null && m.Entity.Id != myEntityId)
                        {
                            // Extrai o PlayFabId clássico dos atributos do ticket.
                            // DataObject vem como tipo interno do SDK — re-serializa para parsear.
                            try
                            {
                                var raw   = PlayFab.Json.PlayFabSimpleJson.SerializeObject(m.Attributes?.DataObject);
                                var attrs = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<
                                    System.Collections.Generic.Dictionary<string, object>>(raw);
                                if (attrs != null && attrs.TryGetValue("PlayFabId", out var pfId))
                                    opponentPlayFabId = pfId?.ToString() ?? string.Empty;
                            }
                            catch { }
                            break;
                        }
                matchFetched = true;
            },
            _ =>
            {
                // GetMatch falhou → modo stub (matchId simulado ou sem oponente real)
                matchFetched = true;
            });

        while (!matchFetched) yield return null;

        // 2. Inicializa estado no servidor apenas em partida real.
        //    player1Id e player2Id são PlayFabIds clássicos para que o CloudScript V1
        //    possa chamar server.UpdatePlayerStatistics / server.GetUserInventory corretamente.
        if (BrainDuel.Match.Core.MatchSessionData.IsRealMatch)
        {
            string localPlayFabId = PlayFabSettings.staticPlayer?.PlayFabId ?? string.Empty;

            bool matchCreated = false;
            PlayFabClientAPI.ExecuteCloudScript(
                new ExecuteCloudScriptRequest
                {
                    FunctionName      = "CreateMatch",
                    FunctionParameter = new
                    {
                        matchId   = BrainDuel.Match.Core.MatchSessionData.MatchId,
                        player1Id = localPlayFabId,
                        player2Id = opponentPlayFabId
                    },
                    GeneratePlayStreamEvent = false
                },
                _ => matchCreated = true,
                err =>
                {
                    Debug.LogWarning($"[MatchMaking] CreateMatch falhou: {err.ErrorMessage}");
                    matchCreated = true;
                });

            while (!matchCreated) yield return null;
        }

        // 3. Busca nome de exibição e nível local
        bool done = false;

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

                done = true;
            },
            _ => done = true
        );

        while (!done) yield return null;

        yield return ContagemRegressiva();
    }

    private void HandleMatchFailed(PlayFab.PlayFabError _)
    {
        StopAnimations();
        SetTexto(TextoErro);
    }

    // ────────────────────────────────────────────────────────
    // Botão cancelar
    // ────────────────────────────────────────────────────────

    public void OnCancelarClick()
    {
        MatchmakingService.Instance?.CancelCurrentSearch();
        SceneManager.LoadScene(cenaHomeScreen);
    }

    // ────────────────────────────────────────────────────────
    // Animações
    // ────────────────────────────────────────────────────────

    private void SetBuscando()
    {
        StopAnimations();
        _dotsCoroutine = StartCoroutine(AnimarPontos());
    }

    private IEnumerator AnimarPontos()
    {
        int pontos = 0;
        while (true)
        {
            SetTexto(TextoBuscando + new string('.', pontos + 1));
            pontos = (pontos + 1) % 3;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator ContagemRegressiva()
    {
        SetCor(Color.white);
        for (int i = contagemRegressiva; i > 0; i--)
        {
            SetTexto(string.Format(TextoEncontrado, i));
            yield return new WaitForSeconds(1f);
        }
        SceneManager.LoadScene(cenaPartida);
    }

    // ────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────

    private void SetTexto(string texto)
    {
        if (_statusText != null)
            _statusText.text = texto;
    }

    private void SetCor(Color cor)
    {
        if (_statusText != null)
            _statusText.color = cor;
    }

    private void StopAnimations()
    {
        if (_dotsCoroutine     != null) { StopCoroutine(_dotsCoroutine);     _dotsCoroutine     = null; }
        if (_contagemCoroutine != null) { StopCoroutine(_contagemCoroutine); _contagemCoroutine = null; }
    }
}
