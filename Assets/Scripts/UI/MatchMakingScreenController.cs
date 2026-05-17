using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MatchMakingScreenController : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private string cenaHomeScreen    = "HomeScreen";
    [SerializeField] private string cenaPartida       = "GameScene";
    [SerializeField] private int    contagemRegressiva = 3;

    private const string TextoBuscando   = "Buscando oponente";
    private const string TextoEncontrado = "Oponente encontrado!\nIniciando partida em {0}...";
    private const string TextoCancelado  = "Busca cancelada.";
    private const string TextoErro       = "Erro ao buscar partida.\nTente novamente.";

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

    // ────────────────────────────────────────────────────────
    // Criação automática do texto de status
    // ────────────────────────────────────────────────────────

    private void CriarTextoStatus()
    {
        // Procura canvas na cena
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[MatchMaking] Nenhum Canvas encontrado na cena.");
            return;
        }

        // Cria GameObject de texto
        var go = new GameObject("TxtStatusMatchmaking");
        go.transform.SetParent(canvas.transform, false);

        // RectTransform centralizado
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(700f, 200f);

        // Componente Text
        _statusText                 = go.AddComponent<Text>();
        _statusText.font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusText.fontSize        = 32;
        _statusText.fontStyle       = FontStyle.Bold;
        _statusText.alignment       = TextAnchor.MiddleCenter;
        _statusText.color           = Color.white;
        _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _statusText.verticalOverflow   = VerticalWrapMode.Overflow;

        // Sombra leve para legibilidade
        var shadow       = go.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);

        // Garante que fique sobre o background
        go.transform.SetAsLastSibling();
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

    private void StopAnimations()
    {
        if (_dotsCoroutine     != null) { StopCoroutine(_dotsCoroutine);     _dotsCoroutine     = null; }
        if (_contagemCoroutine != null) { StopCoroutine(_contagemCoroutine); _contagemCoroutine = null; }
    }
}
