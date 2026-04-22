using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AdminDeckCrudController : MonoBehaviour
{
    private const string NomeInputObjectName = "adminNomeInput";
    private const string KeyInputObjectName = "adminKeyInput";
    private const string DeckJsonInputObjectName = "adminDeckJsonInput";
    private const string ClearToggleObjectName = "adminClearDeckToggle";
    private const string StatusTextObjectName = "adminStatusText";
    private const string ListButtonObjectName = "adminListBtn";
    private const string LoadButtonObjectName = "adminLoadBtn";
    private const string ValidateButtonObjectName = "adminValidateBtn";
    private const string CreateButtonObjectName = "adminCreateBtn";
    private const string UpdateButtonObjectName = "adminUpdateBtn";
    private const string DeleteButtonObjectName = "adminDeleteBtn";

    private static bool sceneHookRegistered;

    [Header("Inputs")]
    [SerializeField] private InputField nomeInput;
    [SerializeField] private InputField keyInput;
    [SerializeField] private InputField deckJsonInput;
    [SerializeField] private Toggle clearDeckContentToggle;

    [Header("Feedback")]
    [SerializeField] private Text statusText;

    [Header("Actions")]
    [SerializeField] private Button listButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button validateButton;
    [SerializeField] private Button createButton;
    [SerializeField] private Button updateButton;
    [SerializeField] private Button deleteButton;

    [Header("Runtime Admin Panel")]
    [SerializeField] private bool showRuntimeAdminPanel = true;

    private string panelNome = string.Empty;
    private string panelKey = string.Empty;
    private string panelDeckJson = string.Empty;
    private bool panelClearDeckContent;
    private Vector2 panelScroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterAutoAttach()
    {
        if (!sceneHookRegistered)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => TryAttachToScene();
            sceneHookRegistered = true;
        }

        TryAttachToScene();
    }

    private static void TryAttachToScene()
    {
        if (Object.FindFirstObjectByType<AdminDeckCrudController>() != null)
        {
            return;
        }

        var host = GameObject.Find(ListButtonObjectName);
        if (host == null)
        {
            return;
        }

        var controller = host.GetComponent<AdminDeckCrudController>();
        if (controller == null)
        {
            controller = host.AddComponent<AdminDeckCrudController>();
        }

        if (host.GetComponent<AdminAccessGuard>() == null)
        {
            host.AddComponent<AdminAccessGuard>();
        }

        controller.TryAutoBindElements();
    }

    private void Awake()
    {
        EnsureCloudScriptService();
        TryAutoBindElements();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void Start()
    {
        SyncPanelFromInputs();
        ListCatalog();
        SetStatus("Atalhos: F1 List, F2 Load, F3 Validate, F4 Create, F5 Update, F6 Delete.", false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ListCatalog();
        }
        else if (Input.GetKeyDown(KeyCode.F2))
        {
            LoadDeck();
        }
        else if (Input.GetKeyDown(KeyCode.F3))
        {
            ValidateDeck();
        }
        else if (Input.GetKeyDown(KeyCode.F4))
        {
            CreateDeck();
        }
        else if (Input.GetKeyDown(KeyCode.F5))
        {
            UpdateDeck();
        }
        else if (Input.GetKeyDown(KeyCode.F6))
        {
            DeleteDeck();
        }
    }

    public void ListCatalog()
    {
        SetStatus("Carregando catalogo...", false);
        AdminDeckCloudScriptService.Instance.ListCatalog(result =>
        {
            if (!result.success)
            {
                SetStatus("Falha ao listar catalogo: " + result.error, true);
                return;
            }

            SetStatus("Catalogo carregado com sucesso.", false);
        });
    }

    public void LoadDeck()
    {
        var key = GetKeyValue();
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Informe a key do deck para carregar.", true);
            return;
        }

        SetStatus("Carregando deck...", false);
        AdminDeckCloudScriptService.Instance.GetDeck(key, result =>
        {
            if (!result.success)
            {
                SetStatus("Falha ao carregar deck: " + result.error, true);
                return;
            }

            if (result.raw != null && result.raw.TryGetValue("nome", out var nome) && nomeInput != null)
            {
                nomeInput.text = nome == null ? string.Empty : nome.ToString();
            }

            panelNome = result.raw != null && result.raw.TryGetValue("nome", out var nomePanel) && nomePanel != null
                ? nomePanel.ToString()
                : panelNome;

            if (result.raw != null && result.raw.TryGetValue("deckJson", out var deckJsonObj) && deckJsonInput != null)
            {
                deckJsonInput.text = deckJsonObj == null ? string.Empty : deckJsonObj.ToString();
                panelDeckJson = deckJsonInput.text;
                SetStatus("Deck carregado. Edite o JSON e execute Validate/Create/Update.", false);
            }
            else if (result.raw != null && result.raw.TryGetValue("deckJson", out var rawDeckJsonOnly) && rawDeckJsonOnly != null)
            {
                panelDeckJson = rawDeckJsonOnly.ToString();
                SetStatus("Deck carregado. Edite o JSON e execute Validate/Create/Update.", false);
            }
            else
            {
                SetStatus("Deck carregado sem payload no retorno.", true);
            }
        });
    }

    public void ValidateDeck()
    {
        if (!TryBuildRequest(out var request))
        {
            return;
        }

        SetStatus("Validando payload...", false);
        AdminDeckCloudScriptService.Instance.ValidateDeckPayload(request, result =>
        {
            if (!result.success)
            {
                SetStatus("Payload invalido: " + result.error, true);
                return;
            }

            SetStatus("Payload valido.", false);
        });
    }

    public void CreateDeck()
    {
        if (!TryBuildRequest(out var request))
        {
            return;
        }

        SetStatus("Criando deck...", false);
        AdminDeckCloudScriptService.Instance.CreateDeck(request, result =>
        {
            if (!result.success)
            {
                SetStatus("Falha ao criar deck: " + result.error, true);
                return;
            }

            SetStatus("Deck criado com sucesso.", false);
            ListCatalog();
        });
    }

    public void UpdateDeck()
    {
        if (!TryBuildRequest(out var request))
        {
            return;
        }

        SetStatus("Atualizando deck...", false);
        AdminDeckCloudScriptService.Instance.UpdateDeck(request, result =>
        {
            if (!result.success)
            {
                SetStatus("Falha ao atualizar deck: " + result.error, true);
                return;
            }

            SetStatus("Deck atualizado com sucesso.", false);
            ListCatalog();
        });
    }

    public void DeleteDeck()
    {
        var key = GetKeyValue();
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Informe a key para excluir.", true);
            return;
        }

        var clear = GetClearDeckContentValue();

        SetStatus("Excluindo deck...", false);
        AdminDeckCloudScriptService.Instance.DeleteDeck(key, clear, result =>
        {
            if (!result.success)
            {
                SetStatus("Falha ao excluir deck: " + result.error, true);
                return;
            }

            SetStatus("Deck excluido com sucesso.", false);
            ListCatalog();
        });
    }

    private bool TryBuildRequest(out AdminDeckRequestDto request)
    {
        request = null;

        var nome = GetNomeValue();
        var key = GetKeyValue();
        var json = GetDeckJsonValue();

        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Informe key.", true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            SetStatus("Informe o JSON do deck.", true);
            return false;
        }

        DeckSchemaV2 deck;
        try
        {
            deck = JsonUtility.FromJson<DeckSchemaV2>(json);
        }
        catch
        {
            SetStatus("JSON invalido para o schema do deck.", true);
            return false;
        }

        if (deck == null)
        {
            SetStatus("Nao foi possivel desserializar o JSON do deck.", true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            nome = string.IsNullOrWhiteSpace(deck.theme) ? key : deck.theme;
        }

        request = new AdminDeckRequestDto
        {
            nome = nome,
            key = key,
            deck = deck
        };

        return true;
    }

    private static string SafeText(InputField input)
    {
        return input == null ? string.Empty : input.text.Trim();
    }

    private string GetNomeValue()
    {
        var value = SafeText(nomeInput);
        return string.IsNullOrWhiteSpace(value) ? panelNome.Trim() : value;
    }

    private string GetKeyValue()
    {
        var value = SafeText(keyInput);
        return string.IsNullOrWhiteSpace(value) ? panelKey.Trim() : value;
    }

    private string GetDeckJsonValue()
    {
        var value = SafeText(deckJsonInput);
        return string.IsNullOrWhiteSpace(value) ? panelDeckJson.Trim() : value;
    }

    private bool GetClearDeckContentValue()
    {
        if (clearDeckContentToggle != null)
        {
            return clearDeckContentToggle.isOn;
        }

        return panelClearDeckContent;
    }

    private void SyncPanelFromInputs()
    {
        if (nomeInput != null)
        {
            panelNome = nomeInput.text;
        }

        if (keyInput != null)
        {
            panelKey = keyInput.text;
        }

        if (deckJsonInput != null)
        {
            panelDeckJson = deckJsonInput.text;
        }

        if (clearDeckContentToggle != null)
        {
            panelClearDeckContent = clearDeckContentToggle.isOn;
        }
    }

    private void BindButtons()
    {
        listButton?.onClick.AddListener(ListCatalog);
        loadButton?.onClick.AddListener(LoadDeck);
        validateButton?.onClick.AddListener(ValidateDeck);
        createButton?.onClick.AddListener(CreateDeck);
        updateButton?.onClick.AddListener(UpdateDeck);
        deleteButton?.onClick.AddListener(DeleteDeck);
    }

    private void UnbindButtons()
    {
        listButton?.onClick.RemoveListener(ListCatalog);
        loadButton?.onClick.RemoveListener(LoadDeck);
        validateButton?.onClick.RemoveListener(ValidateDeck);
        createButton?.onClick.RemoveListener(CreateDeck);
        updateButton?.onClick.RemoveListener(UpdateDeck);
        deleteButton?.onClick.RemoveListener(DeleteDeck);
    }

    private void TryAutoBindElements()
    {
        if (nomeInput == null)
        {
            var go = GameObject.Find(NomeInputObjectName);
            if (go != null)
            {
                nomeInput = go.GetComponent<InputField>();
            }
        }

        if (keyInput == null)
        {
            var go = GameObject.Find(KeyInputObjectName);
            if (go != null)
            {
                keyInput = go.GetComponent<InputField>();
            }
        }

        if (deckJsonInput == null)
        {
            var go = GameObject.Find(DeckJsonInputObjectName);
            if (go != null)
            {
                deckJsonInput = go.GetComponent<InputField>();
            }
        }

        if (clearDeckContentToggle == null)
        {
            var go = GameObject.Find(ClearToggleObjectName);
            if (go != null)
            {
                clearDeckContentToggle = go.GetComponent<Toggle>();
            }
        }

        if (statusText == null)
        {
            var go = GameObject.Find(StatusTextObjectName);
            if (go != null)
            {
                statusText = go.GetComponent<Text>();
            }
        }

        if (listButton == null)
        {
            var go = GameObject.Find(ListButtonObjectName);
            if (go != null)
            {
                listButton = go.GetComponent<Button>();
            }
        }

        if (loadButton == null)
        {
            var go = GameObject.Find(LoadButtonObjectName);
            if (go != null)
            {
                loadButton = go.GetComponent<Button>();
            }
        }

        if (validateButton == null)
        {
            var go = GameObject.Find(ValidateButtonObjectName);
            if (go != null)
            {
                validateButton = go.GetComponent<Button>();
            }
        }

        if (createButton == null)
        {
            var go = GameObject.Find(CreateButtonObjectName);
            if (go != null)
            {
                createButton = go.GetComponent<Button>();
            }
        }

        if (updateButton == null)
        {
            var go = GameObject.Find(UpdateButtonObjectName);
            if (go != null)
            {
                updateButton = go.GetComponent<Button>();
            }
        }

        if (deleteButton == null)
        {
            var go = GameObject.Find(DeleteButtonObjectName);
            if (go != null)
            {
                deleteButton = go.GetComponent<Button>();
            }
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusText == null)
        {
            Debug.Log(isError ? "[AdminDeckCrudController] " + message : "[AdminDeckCrudController] " + message);
            return;
        }

        statusText.text = message;
        statusText.color = isError ? Color.red : Color.white;
    }

    private static void EnsureCloudScriptService()
    {
        if (AdminDeckCloudScriptService.Instance != null)
        {
            return;
        }

        var go = new GameObject("AdminDeckCloudScriptService");
        go.AddComponent<AdminDeckCloudScriptService>();
    }

    private void OnGUI()
    {
        if (!showRuntimeAdminPanel)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name != "DeckAdmin")
        {
            return;
        }

        const int panelWidth = 640;
        const int panelHeight = 620;
        var area = new Rect(20, 20, panelWidth, panelHeight);

        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label("Deck Admin CRUD");
        GUILayout.Label("Se esta tela apareceu, voce ja esta na cena DeckAdmin.");

        GUILayout.Label("Nome (categoria)");
        panelNome = GUILayout.TextField(panelNome ?? string.Empty, 256);

        GUILayout.Label("Key (ex: cartas_matematica)");
        panelKey = GUILayout.TextField(panelKey ?? string.Empty, 256);

        panelClearDeckContent = GUILayout.Toggle(panelClearDeckContent, "Limpar conteudo da key ao excluir");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Listar catalogo", GUILayout.Height(32)))
        {
            ListCatalog();
        }

        if (GUILayout.Button("Carregar deck", GUILayout.Height(32)))
        {
            LoadDeck();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Validar", GUILayout.Height(32)))
        {
            ValidateDeck();
        }

        if (GUILayout.Button("Criar", GUILayout.Height(32)))
        {
            CreateDeck();
        }

        if (GUILayout.Button("Atualizar", GUILayout.Height(32)))
        {
            UpdateDeck();
        }

        if (GUILayout.Button("Excluir", GUILayout.Height(32)))
        {
            DeleteDeck();
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Deck JSON");
        panelScroll = GUILayout.BeginScrollView(panelScroll, GUILayout.Height(320));
        panelDeckJson = GUILayout.TextArea(panelDeckJson ?? string.Empty, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }
}
