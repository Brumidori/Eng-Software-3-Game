using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MatchMakingScreenController : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private string cenaHomeScreen    = "HomeScreen";
    [SerializeField] private string cenaPartida       = "BrainDuelArena";
    [SerializeField] private int    contagemRegressiva = 3;

    [Header("Visual")]
    [SerializeField] private int tamanhoFonte = 48;

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
        BuscarBotaoCancelar();
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
        foreach (var c in FindObjectsOfType<Canvas>())
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

        // Sombra leve para legibilidade
        var shadow            = go.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);

        container.transform.SetAsLastSibling();
    }

    // ────────────────────────────────────────────────────────
    // Busca botão cancelar pelo nome
    // ────────────────────────────────────────────────────────

    private void BuscarBotaoCancelar()
    {
        var goBtn = GameObject.Find("BtnCancelarEspera");
        if (goBtn == null)
        {
            Debug.LogWarning("[MatchMaking] BtnCancelarEspera não encontrado na cena.");
            return;
        }

        _btnCancelar = goBtn.GetComponent<Button>();
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
        StopAnimations();
        _contagemCoroutine = StartCoroutine(ContagemRegressiva());
    }

    private void HandleMatchFailed(PlayFab.PlayFabError _)
    {
        StopAnimations();
        SetTexto(TextoErro);
    }

    // ────────────────────────────────────────────────────────
    // Botão cancelar
    // ────────────────────────────────────────────────────────

    private void OnCancelarClick()
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
