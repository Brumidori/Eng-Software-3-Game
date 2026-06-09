using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DueloScreenController : MonoBehaviour
{
    public enum DuelMode { None, Public, Private }

    [Header("Cards")]
    [SerializeField] private Button cardSalaPublica;
    [SerializeField] private Button cardSalaPrivada;
    [SerializeField] private CanvasGroup cardPublicaGroup;
    [SerializeField] private CanvasGroup cardPrivadaGroup;

    [Header("Botoes de acao")]
    [SerializeField] private Button btnSelecionarPublico;
    [SerializeField] private Button btnCriarPrivado;
    [SerializeField] private Button btnIniciarDuelo;

    [Header("Sala Privada")]
    [SerializeField] private InputField codigoInput;
    [SerializeField] private Button btnEntrar;

    [Header("Cenas")]
    [SerializeField] private string cenaPublica  = "MatchMaking";
    [SerializeField] private string cenaPrivada  = "MatchMakingPrivate";

    [Header("Visual Selecao")]
    [SerializeField] private Color corSelecionada = new Color(0.204f, 0.596f, 0.859f, 1f); // #3498DB
    [SerializeField] private Color corNormal = Color.white;

    private DuelMode _modo = DuelMode.None;
    private string   _codigoSala = string.Empty;

    private Image _imgCardPublica;
    private Image _imgCardPrivada;

    private const float AlphaAtivo   = 1f;
    private const float AlphaInativo = 0.75f;

    private void Awake()
    {
        if (cardSalaPublica != null) _imgCardPublica = cardSalaPublica.GetComponent<Image>();
        if (cardSalaPrivada != null) _imgCardPrivada = cardSalaPrivada.GetComponent<Image>();

        NeutralizeSelectedColor(btnSelecionarPublico);
        NeutralizeSelectedColor(btnCriarPrivado);
    }

    private static void NeutralizeSelectedColor(Button btn)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.selectedColor = cb.normalColor;
        btn.colors = cb;
    }

    private void Start()
    {
        MainMenuController.Instance?.SetActiveTab(MenuTab.Duelo);
        AtualizarVisual();
    }

    private void OnEnable()
    {
        cardSalaPublica.onClick.AddListener(OnCardPublicoClick);
        cardSalaPrivada.onClick.AddListener(OnCardPrivadoClick);
        btnSelecionarPublico.onClick.AddListener(OnCardPublicoClick);
        btnCriarPrivado.onClick.AddListener(OnCriarSalaPrivadaClick);
        btnEntrar.onClick.AddListener(OnEntrarSalaClick);
        btnIniciarDuelo.onClick.AddListener(OnIniciarDueloClick);
        codigoInput.onValueChanged.AddListener(OnCodigoAlterado);
    }

    private void OnDisable()
    {
        cardSalaPublica.onClick.RemoveListener(OnCardPublicoClick);
        cardSalaPrivada.onClick.RemoveListener(OnCardPrivadoClick);
        btnSelecionarPublico.onClick.RemoveListener(OnCardPublicoClick);
        btnCriarPrivado.onClick.RemoveListener(OnCriarSalaPrivadaClick);
        btnEntrar.onClick.RemoveListener(OnEntrarSalaClick);
        btnIniciarDuelo.onClick.RemoveListener(OnIniciarDueloClick);
        codigoInput.onValueChanged.RemoveListener(OnCodigoAlterado);
    }

    // --- handlers ---

    private void OnCardPublicoClick()
    {
        _modo = DuelMode.Public;
        AtualizarVisual();
    }

    private void OnCardPrivadoClick()
    {
        _modo = DuelMode.Private;
        AtualizarVisual();
    }

    private void OnCriarSalaPrivadaClick()
    {
        _modo = DuelMode.Private;
        _codigoSala = GenerateRandomCode(6);
        AtualizarVisual();
        
        // Inicia o matchmaking privado e vai pra cena
        GetOrCreateMatchmakingService().StartPrivateMatchmaking("BrainDuelPrivateQueue", _codigoSala);
        SceneManager.LoadScene(cenaPrivada);
    }

    private MatchmakingService GetOrCreateMatchmakingService()
    {
        if (MatchmakingService.Instance == null)
        {
            var go = new GameObject("MatchmakingService");
            go.AddComponent<MatchmakingService>();
        }
        return MatchmakingService.Instance;
    }

    private void OnEntrarSalaClick()
    {
        _codigoSala = codigoInput != null ? codigoInput.text.Trim().ToUpperInvariant() : string.Empty;
        _modo = DuelMode.Private;
        AtualizarVisual();

        if (!string.IsNullOrWhiteSpace(_codigoSala))
        {
            GetOrCreateMatchmakingService().StartPrivateMatchmaking("BrainDuelPrivateQueue", _codigoSala);
            SceneManager.LoadScene(cenaPrivada);
        }
    }

    private string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] stringChars = new char[length];
        var random = new System.Random();

        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[random.Next(chars.Length)];
        }

        return new string(stringChars);
    }

    private void OnCodigoAlterado(string value)
    {
        if (_modo == DuelMode.Private)
            AtualizarVisual();
    }

    private void OnIniciarDueloClick()
    {
        if (_modo == DuelMode.Public)
        {
            SceneManager.LoadScene(cenaPublica);
            return;
        }

        if (_modo == DuelMode.Private && !string.IsNullOrWhiteSpace(_codigoSala))
        {
            GetOrCreateMatchmakingService().StartPrivateMatchmaking("BrainDuelPrivateQueue", _codigoSala);
            SceneManager.LoadScene(cenaPrivada);
        }
    }

    // --- visual ---

    private void AtualizarVisual()
    {
        bool publicoAtivo = _modo == DuelMode.Public;
        bool privadoAtivo = _modo == DuelMode.Private;
        bool nenhum       = _modo == DuelMode.None;

        // Alpha: selecionado fica cheio, outro dimma — ambos sempre clicáveis
        if (cardPublicaGroup != null)
        {
            cardPublicaGroup.alpha          = publicoAtivo || nenhum ? AlphaAtivo : AlphaInativo;
            cardPublicaGroup.interactable   = true;
            cardPublicaGroup.blocksRaycasts = true;
        }
        if (cardPrivadaGroup != null)
        {
            cardPrivadaGroup.alpha          = privadoAtivo || nenhum ? AlphaAtivo : AlphaInativo;
            cardPrivadaGroup.interactable   = true;
            cardPrivadaGroup.blocksRaycasts = true;
        }

        // Cor: selecionado → #3498DB, demais → branco
        if (_imgCardPublica != null) _imgCardPublica.color = publicoAtivo ? corSelecionada : corNormal;
        if (_imgCardPrivada != null) _imgCardPrivada.color = privadoAtivo ? corSelecionada : corNormal;

        if (btnIniciarDuelo != null)
            btnIniciarDuelo.interactable = publicoAtivo;
    }
}
