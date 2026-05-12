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
    [SerializeField] private string cenaPublica  = "DueloPublico";
    [SerializeField] private string cenaPrivada  = "DueloPrivado";

    private DuelMode _modo = DuelMode.None;
    private string   _codigoSala = string.Empty;

    private const float AlphaAtivo    = 1f;
    private const float AlphaInativo  = 0.5f;

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
        _codigoSala = string.Empty;
        AtualizarVisual();
        // TODO: chamar servico de criacao de sala e popular codigoInput com o codigo gerado
    }

    private void OnEntrarSalaClick()
    {
        _codigoSala = codigoInput != null ? codigoInput.text.Trim().ToUpperInvariant() : string.Empty;
        _modo = DuelMode.Private;
        AtualizarVisual();
    }

    private void OnCodigoAlterado(string value)
    {
        if (_modo == DuelMode.Private)
            AtualizarVisual();
    }

    private void OnIniciarDueloClick()
    {
        if (!PodeIniciar()) return;

        if (_modo == DuelMode.Public)
        {
            SceneManager.LoadScene(cenaPublica);
            return;
        }

        if (_modo == DuelMode.Private && !string.IsNullOrWhiteSpace(_codigoSala))
        {
            // TODO: passar codigo para o servico de sala antes de carregar a cena
            SceneManager.LoadScene(cenaPrivada);
        }
    }

    // --- logica de estado ---

    private bool PodeIniciar()
    {
        if (_modo == DuelMode.Public)  return true;
        if (_modo == DuelMode.Private) return !string.IsNullOrWhiteSpace(_codigoSala);
        return false;
    }

    private void AtualizarVisual()
    {
        bool publicoAtivo  = _modo == DuelMode.Public;
        bool privadoAtivo  = _modo == DuelMode.Private;

        if (cardPublicaGroup != null) cardPublicaGroup.alpha = publicoAtivo ? AlphaAtivo : (_modo == DuelMode.None ? AlphaAtivo : AlphaInativo);
        if (cardPrivadaGroup != null) cardPrivadaGroup.alpha = privadoAtivo ? AlphaAtivo : (_modo == DuelMode.None ? AlphaAtivo : AlphaInativo);

        if (btnIniciarDuelo != null)
            btnIniciarDuelo.interactable = PodeIniciar();
    }
}
