using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AdminDeckSceneController : MonoBehaviour
{
    public enum SceneMode
    {
        Menu = 0,
        Catalog = 1,
        Create = 2,
        Edit = 3,
        Delete = 4
    }

    private const string LoginSceneDefault = "Login";
    private const string MenuSceneDefault = "DeckAdminMenu";
    private const string CatalogSceneDefault = "DeckAdminCatalog";
    private const string CreateSceneDefault = "DeckAdminCreate";
    private const string EditSceneDefault = "DeckAdminEdit";
    private const string DeleteSceneDefault = "DeckAdminDelete";

    public static class SceneTransferState
    {
        public static string PendingKey = string.Empty;
    }

    [Header("Scene")]
    [SerializeField] private SceneMode sceneMode = SceneMode.Menu;
    [SerializeField] private string loginSceneName = LoginSceneDefault;
    [SerializeField] private string menuSceneName = MenuSceneDefault;
    [SerializeField] private string catalogSceneName = CatalogSceneDefault;
    [SerializeField] private string createSceneName = CreateSceneDefault;
    [SerializeField] private string editSceneName = EditSceneDefault;
    [SerializeField] private string deleteSceneName = DeleteSceneDefault;

    [Header("Layout")]
    [SerializeField, Range(1f, 3f)] private float uiScaleBoost = 4f;
    [SerializeField, Range(1f, 3f)] private float maxUiScale = 10f;
    [SerializeField] private float topMargin = 34f;
    [SerializeField] private float sideMargin = 18f;
    [SerializeField] private float bottomMargin = 18f;

    private readonly List<DeckCatalogItem> catalogItems = new List<DeckCatalogItem>();
    private Vector2 menuScroll;
    private Vector2 catalogScroll;
    private Vector2 editorScroll;
    private Vector2 questionScroll;
    private Vector2 deleteScroll;

    private DeckSchemaV2 currentDeck;
    private string currentKey = string.Empty;
    private string categoryName = string.Empty;
    private string statusMessage = string.Empty;
    private bool statusIsError;
    private bool deleteConfirmed;
    private string deleteConfirmText = string.Empty;

    private string menuButtonStyleName = string.Empty;

    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle labelStyle;
    private GUIStyle statusStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallButtonStyle;
    private GUIStyle textFieldStyle;
    private GUIStyle textAreaStyle;
    private bool stylesBuilt;

    private void Awake()
    {
        EnsureServices();
        EnsureDraft();
    }

    private void Start()
    {
        if (sceneMode == SceneMode.Catalog)
        {
            LoadCatalog();
        }

        if (sceneMode == SceneMode.Edit && string.IsNullOrWhiteSpace(currentKey))
        {
            currentKey = SceneTransferState.PendingKey;
        }

        if ((sceneMode == SceneMode.Edit || sceneMode == SceneMode.Delete) && !string.IsNullOrWhiteSpace(SceneTransferState.PendingKey))
        {
            currentKey = SceneTransferState.PendingKey;
            if (sceneMode == SceneMode.Edit)
            {
                LoadDeckByKey(currentKey);
            }
        }

        if (sceneMode == SceneMode.Delete)
        {
            deleteConfirmText = string.Empty;
        }
    }

    private void BuildStyles()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        sectionStyle = new GUIStyle(GUI.skin.box)
        {
            font = font,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(14, 14, 14, 14)
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = 16,
            normal = { textColor = Color.white }
        };

        statusStyle = new GUIStyle(labelStyle)
        {
            fontSize = 15,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            font = font,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            fixedHeight = 38,
            padding = new RectOffset(14, 14, 8, 8)
        };

        smallButtonStyle = new GUIStyle(buttonStyle)
        {
            fontSize = 14,
            fixedHeight = 30
        };

        textFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            font = font,
            fontSize = 15,
            fixedHeight = 30,
            padding = new RectOffset(8, 8, 6, 6)
        };

        textAreaStyle = new GUIStyle(GUI.skin.textArea)
        {
            font = font,
            fontSize = 15,
            wordWrap = true,
            padding = new RectOffset(8, 8, 8, 8)
        };
    }

    private void OnGUI()
    {
        if (!stylesBuilt)
        {
            BuildStyles();
            stylesBuilt = true;
        }

        var uiScale = CalculateUiScale();
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

        var logicalWidth = Screen.width / uiScale;
        var logicalHeight = Screen.height / uiScale;

        var safeArea = Screen.safeArea;
        var leftSafeInset = safeArea.xMin / uiScale;
        var rightSafeInset = (Screen.width - safeArea.xMax) / uiScale;
        var topSafeInset = (Screen.height - safeArea.yMax) / uiScale;

        var background = new Rect(0, 0, logicalWidth, logicalHeight);
        GUI.Box(background, GUIContent.none);

        var outerX = leftSafeInset + sideMargin;
        var outerY = topSafeInset + topMargin;
        var outerWidth = logicalWidth - leftSafeInset - rightSafeInset - (sideMargin * 2f);
        var outerHeight = logicalHeight - topSafeInset - topMargin - bottomMargin;
        var outer = new Rect(outerX, outerY, Mathf.Max(120f, outerWidth), Mathf.Max(160f, outerHeight));
        GUILayout.BeginArea(outer);

        DrawHeader();
        GUILayout.Space(10);

        switch (sceneMode)
        {
            case SceneMode.Menu:
                DrawMenu();
                break;
            case SceneMode.Catalog:
                DrawCatalog();
                break;
            case SceneMode.Create:
                DrawEditor(true, false);
                break;
            case SceneMode.Edit:
                DrawEditor(false, true);
                break;
            case SceneMode.Delete:
                DrawDelete();
                break;
        }

        GUILayout.FlexibleSpace();
        GUILayout.Space(6);
        DrawStatusBar();
        GUILayout.EndArea();

        GUI.matrix = previousMatrix;
    }

    private float CalculateUiScale()
    {
        var clampedBoost = Mathf.Clamp(uiScaleBoost, 1f, 3f);
        var clampedMax = Mathf.Clamp(maxUiScale, 1f, 3f);
        var shortSide = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        var adaptiveScale = 1080f / shortSide;
        return Mathf.Clamp(adaptiveScale * clampedBoost, 1f, clampedMax);
    }

    private void DrawHeader()
    {
        GUILayout.Label(GetSceneTitle(), titleStyle);
        GUILayout.Label(GetSceneSubtitle(), labelStyle);
    }

    private void DrawMenu()
    {
        menuScroll = GUILayout.BeginScrollView(menuScroll, GUILayout.ExpandHeight(true));

        GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("Navegação", labelStyle);
        GUILayout.Space(8);

        DrawNavButton("Catálogo", catalogSceneName);
        DrawNavButton("Criar deck", createSceneName);
        DrawNavButton("Editar deck", editSceneName);
        DrawNavButton("Excluir deck", deleteSceneName);

        GUILayout.Space(12);
        if (GUILayout.Button("Voltar para Login", buttonStyle))
        {
            LoadSceneSafe(loginSceneName);
        }

        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("Como usar", labelStyle);
        GUILayout.Space(8);
        GUILayout.Label("Use o menu para escolher uma tarefa. Cada tela permite editar deck_id, tema, perguntas, opcoes e tempo sem mexer em JSON bruto.", statusStyle);
        GUILayout.Space(8);
        GUILayout.Label("Fluxo sugerido: Catalogo para localizar o deck, Editar para alterar, Criar para novos decks e Excluir para remoção controlada.", statusStyle);
        GUILayout.EndVertical();

        GUILayout.EndScrollView();
    }

    private void DrawCatalog()
    {
        GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (GUILayout.Button("Atualizar catalogo", buttonStyle))
        {
            LoadCatalog();
        }

        if (GUILayout.Button("Menu", buttonStyle))
        {
            LoadSceneSafe(menuSceneName);
        }

        GUILayout.Space(8);

        catalogScroll = GUILayout.BeginScrollView(catalogScroll, GUILayout.ExpandHeight(true));
        if (catalogItems.Count == 0)
        {
            GUILayout.Label("Nenhuma categoria encontrada.", labelStyle);
        }
        else
        {
            for (var i = 0; i < catalogItems.Count; i++)
            {
                var item = catalogItems[i];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(item.nome + "  -  " + item.key, labelStyle);
                if (GUILayout.Button("Abrir edicao", smallButtonStyle))
                {
                    SceneTransferState.PendingKey = item.key;
                    LoadSceneSafe(editSceneName);
                }

                if (GUILayout.Button("Excluir", smallButtonStyle))
                {
                    SceneTransferState.PendingKey = item.key;
                    LoadSceneSafe(deleteSceneName);
                }
                GUILayout.EndVertical();
                GUILayout.Space(6);
            }
        }
        GUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    private void DrawEditor(bool createMode, bool loadByKeyVisible)
    {
        editorScroll = GUILayout.BeginScrollView(editorScroll, GUILayout.ExpandHeight(true));

        GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label(createMode ? "Novo deck" : "Editar deck", labelStyle);
        GUILayout.Space(8);

        GUILayout.Label("Key do deck", labelStyle);
        currentKey = GUILayout.TextField(currentKey ?? string.Empty, textFieldStyle);

        GUILayout.Label("Nome da categoria", labelStyle);
        categoryName = GUILayout.TextField(categoryName ?? string.Empty, textFieldStyle);

        GUILayout.Label("deck_id", labelStyle);
        currentDeck.deck_id = GUILayout.TextField(currentDeck.deck_id ?? string.Empty, textFieldStyle);

        GUILayout.Label("Tema", labelStyle);
        currentDeck.theme = GUILayout.TextField(currentDeck.theme ?? string.Empty, textFieldStyle);

        GUILayout.Space(8);
        if (loadByKeyVisible && GUILayout.Button("Carregar deck pela key", buttonStyle))
        {
            LoadDeckByKey(currentKey);
        }

        if (GUILayout.Button("Adicionar pergunta", buttonStyle))
        {
            AddQuestion();
        }

        if (GUILayout.Button("Validar", buttonStyle))
        {
            ValidateCurrentDeck();
        }

        if (GUILayout.Button(createMode ? "Criar deck" : "Salvar alteracoes", buttonStyle))
        {
            if (createMode)
            {
                CreateCurrentDeck();
            }
            else
            {
                UpdateCurrentDeck();
            }
        }

        if (GUILayout.Button("Menu", buttonStyle))
        {
            LoadSceneSafe(menuSceneName);
        }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("Perguntas", labelStyle);
        GUILayout.Space(8);

        questionScroll = GUILayout.BeginScrollView(questionScroll, GUILayout.Height(Mathf.Max(320f, Screen.height * 0.5f)));
        for (var i = 0; i < currentDeck.questions.Count; i++)
        {
            DrawQuestionPanel(i);
            GUILayout.Space(8);
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndScrollView();
    }

    private void DrawQuestionPanel(int questionIndex)
    {
        var question = currentDeck.questions[questionIndex];
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Pergunta " + (questionIndex + 1), labelStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Remover pergunta", smallButtonStyle, GUILayout.Width(150)))
        {
            RemoveQuestion(questionIndex);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return;
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("ID", labelStyle);
        question.id = GUILayout.TextField(question.id ?? string.Empty, textFieldStyle);

        GUILayout.Label("Texto", labelStyle);
        question.text = GUILayout.TextArea(question.text ?? string.Empty, textAreaStyle, GUILayout.MinHeight(70));

        GUILayout.Label("Tempo limite (segundos)", labelStyle);
        var timeValue = GUILayout.TextField(question.time_limit.ToString(), textFieldStyle);
        if (int.TryParse(timeValue, out var parsedTime) && parsedTime >= 1)
        {
            question.time_limit = parsedTime;
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Opcoes", labelStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Adicionar opcao", smallButtonStyle, GUILayout.Width(130)))
        {
            AddOption(questionIndex);
        }
        GUILayout.EndHorizontal();

        for (var i = 0; i < question.options.Count; i++)
        {
            DrawOptionRow(questionIndex, i);
        }

        var correctCount = CountCorrectOptions(question);
        if (correctCount != 1)
        {
            GUILayout.Label("Cada pergunta precisa ter exatamente 1 opcao correta. Atual: " + correctCount, statusStyle);
        }

        GUILayout.EndVertical();
    }

    private void DrawOptionRow(int questionIndex, int optionIndex)
    {
        var option = currentDeck.questions[questionIndex].options[optionIndex];

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Opcao " + (optionIndex + 1), labelStyle);
        option.text = GUILayout.TextField(option.text ?? string.Empty, textFieldStyle, GUILayout.ExpandWidth(true));

        var corrected = GUILayout.Toggle(option.is_correct, "Correta", smallButtonStyle);
        if (corrected && !option.is_correct)
        {
            SetCorrectOption(questionIndex, optionIndex);
        }

        if (GUILayout.Button("Remover", smallButtonStyle))
        {
            RemoveOption(questionIndex, optionIndex);
            GUILayout.EndVertical();
            return;
        }
        GUILayout.EndVertical();
    }

    private void DrawDelete()
    {
        deleteScroll = GUILayout.BeginScrollView(deleteScroll, GUILayout.ExpandHeight(true));

        GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("Excluir deck", labelStyle);
        GUILayout.Space(8);

        GUILayout.Label("Key do deck", labelStyle);
        currentKey = GUILayout.TextField(currentKey ?? string.Empty, textFieldStyle);

        GUILayout.Label("Confirmar digitando a mesma key", labelStyle);
        deleteConfirmText = GUILayout.TextField(deleteConfirmText ?? string.Empty, textFieldStyle);

        deleteConfirmed = string.Equals(deleteConfirmText?.Trim(), currentKey?.Trim(), StringComparison.OrdinalIgnoreCase);
        GUILayout.Label(deleteConfirmed ? "Confirmacao valida." : "Confirmacao invalida.", labelStyle);

        GUILayout.Space(8);
        if (GUILayout.Button("Carregar catalogo", buttonStyle))
        {
            LoadCatalog();
        }

        if (GUILayout.Button("Excluir", buttonStyle))
        {
            DeleteCurrentDeck();
        }

        if (GUILayout.Button("Menu", buttonStyle))
        {
            LoadSceneSafe(menuSceneName);
        }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("Catalogo", labelStyle);
        GUILayout.Space(8);

        catalogScroll = GUILayout.BeginScrollView(catalogScroll, GUILayout.Height(Mathf.Max(260f, Screen.height * 0.45f)));
        for (var i = 0; i < catalogItems.Count; i++)
        {
            var item = catalogItems[i];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(item.nome + " - " + item.key, labelStyle);
            if (GUILayout.Button("Usar", smallButtonStyle))
            {
                currentKey = item.key;
                deleteConfirmText = item.key;
            }
            GUILayout.EndVertical();
            GUILayout.Space(4);
        }
        GUILayout.EndScrollView();

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
    }

    private void DrawNavButton(string label, string sceneName)
    {
        if (GUILayout.Button(label, buttonStyle))
        {
            LoadSceneSafe(sceneName);
        }
        GUILayout.Space(6);
    }

    private void DrawStatusBar()
    {
        var background = statusIsError ? new Color(0.45f, 0.14f, 0.14f, 0.95f) : new Color(0.12f, 0.25f, 0.18f, 0.95f);
        var previous = GUI.color;
        GUI.color = background;
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(78));
        GUI.color = previous;
        GUILayout.Label(statusMessage, statusStyle);
        GUILayout.EndVertical();
    }

    private string GetSceneTitle()
    {
        switch (sceneMode)
        {
            case SceneMode.Menu:
                return "Deck Admin";
            case SceneMode.Catalog:
                return "Catalogo de decks";
            case SceneMode.Create:
                return "Criar deck";
            case SceneMode.Edit:
                return "Editar deck";
            case SceneMode.Delete:
                return "Excluir deck";
            default:
                return "Deck Admin";
        }
    }

    private string GetSceneSubtitle()
    {
        switch (sceneMode)
        {
            case SceneMode.Menu:
                return "Escolha a funcionalidade que deseja executar.";
            case SceneMode.Catalog:
                return "Visualize os decks cadastrados e abra a edicao ou exclusao.";
            case SceneMode.Create:
                return "Preencha os campos para gerar um novo deck sem editar JSON.";
            case SceneMode.Edit:
                return "Carregue um deck existente pela key e altere os campos desejados.";
            case SceneMode.Delete:
                return "Exclusao controlada com confirmacao manual da key.";
            default:
                return string.Empty;
        }
    }

    private void LoadCatalog()
    {
        SetStatus("Carregando catalogo...", false);
        AdminDeckCloudScriptService.Instance.ListCatalog(result =>
        {
            if (!result.success)
            {
                SetStatus("Falha ao listar catalogo: " + result.error, true);
                return;
            }

            catalogItems.Clear();
            if (result.raw != null && result.raw.TryGetValue("deckIndex", out var indexObj) && indexObj is IDictionary<string, object> deckIndex)
            {
                if (deckIndex.TryGetValue("categorias", out var categoriasObj))
                {
                    ExtractCatalogEntries(categoriasObj, catalogItems);
                }
            }

            SetStatus("Catalogo carregado com sucesso.", false);
        });
    }

    private void LoadDeckByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Informe a key do deck.", true);
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

            if (result.raw != null)
            {
                if (result.raw.TryGetValue("nome", out var nomeObj) && nomeObj != null)
                {
                    categoryName = nomeObj.ToString();
                }

                if (result.raw.TryGetValue("deckJson", out var deckJsonObj) && deckJsonObj != null)
                {
                    var deckJson = deckJsonObj.ToString();
                    if (!string.IsNullOrWhiteSpace(deckJson))
                    {
                        LoadDraftFromJson(deckJson);
                        SetStatus("Deck carregado com sucesso.", false);
                        return;
                    }
                }
            }

            SetStatus("Deck carregado sem payload utilizavel.", true);
        });
    }

    private void LoadDraftFromJson(string deckJson)
    {
        var deck = JsonUtility.FromJson<DeckSchemaV2>(deckJson);
        if (deck == null)
        {
            SetStatus("Nao foi possivel interpretar o JSON do deck.", true);
            return;
        }

        currentDeck = deck;
        EnsureDraft();
    }

    private void ValidateCurrentDeck()
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

    private void CreateCurrentDeck()
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
            LoadCatalog();
        });
    }

    private void UpdateCurrentDeck()
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
            LoadCatalog();
        });
    }

    private void DeleteCurrentDeck()
    {
        if (string.IsNullOrWhiteSpace(currentKey))
        {
            SetStatus("Informe a key do deck.", true);
            return;
        }

        if (!deleteConfirmed)
        {
            SetStatus("Confirme a exclusao digitando a mesma key.", true);
            return;
        }

        SetStatus("Excluindo deck...", false);
        AdminDeckCloudScriptService.Instance.DeleteDeck(currentKey.Trim(), true, result =>
        {
            if (!result.success)
            {
                SetStatus("Falha ao excluir deck: " + result.error, true);
                return;
            }

            SetStatus("Deck excluido com sucesso.", false);
            deleteConfirmText = string.Empty;
            LoadCatalog();
        });
    }

    private bool TryBuildRequest(out AdminDeckRequestDto request)
    {
        request = null;

        EnsureDraft();

        if (string.IsNullOrWhiteSpace(currentKey))
        {
            SetStatus("Informe a key do deck.", true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            categoryName = string.IsNullOrWhiteSpace(currentDeck.theme) ? currentKey.Trim() : currentDeck.theme.Trim();
        }

        if (string.IsNullOrWhiteSpace(currentDeck.deck_id))
        {
            currentDeck.deck_id = currentKey.Trim() + "_01";
        }

        if (string.IsNullOrWhiteSpace(currentDeck.theme))
        {
            currentDeck.theme = categoryName.Trim();
        }

        request = new AdminDeckRequestDto
        {
            nome = categoryName.Trim(),
            key = currentKey.Trim(),
            deck = currentDeck
        };

        return true;
    }

    private void EnsureDraft()
    {
        if (currentDeck == null)
        {
            currentDeck = new DeckSchemaV2();
        }

        if (currentDeck.questions == null)
        {
            currentDeck.questions = new List<DeckQuestionV2>();
        }

        if (currentDeck.questions.Count == 0)
        {
            currentDeck.questions.Add(CreateQuestionTemplate(1));
        }

        for (var i = 0; i < currentDeck.questions.Count; i++)
        {
            EnsureQuestionTemplate(currentDeck.questions[i]);
        }
    }

    private void EnsureQuestionTemplate(DeckQuestionV2 question)
    {
        if (question.options == null)
        {
            question.options = new List<DeckOptionV2>();
        }

        while (question.options.Count < 4)
        {
            question.options.Add(CreateOptionTemplate(question.options.Count == 0));
        }

        var correctCount = CountCorrectOptions(question);
        if (correctCount == 0 && question.options.Count > 0)
        {
            question.options[0].is_correct = true;
        }
    }

    private DeckQuestionV2 CreateQuestionTemplate(int index)
    {
        return new DeckQuestionV2
        {
            id = "q_" + index.ToString("D3"),
            text = string.Empty,
            options = new List<DeckOptionV2>
            {
                CreateOptionTemplate(true),
                CreateOptionTemplate(false),
                CreateOptionTemplate(false),
                CreateOptionTemplate(false)
            },
            time_limit = 20
        };
    }

    private DeckOptionV2 CreateOptionTemplate(bool isCorrect)
    {
        return new DeckOptionV2
        {
            text = string.Empty,
            is_correct = isCorrect
        };
    }

    private void AddQuestion()
    {
        EnsureDraft();
        currentDeck.questions.Add(CreateQuestionTemplate(currentDeck.questions.Count + 1));
    }

    private void RemoveQuestion(int index)
    {
        if (index < 0 || index >= currentDeck.questions.Count)
        {
            return;
        }

        currentDeck.questions.RemoveAt(index);
        if (currentDeck.questions.Count == 0)
        {
            currentDeck.questions.Add(CreateQuestionTemplate(1));
        }
    }

    private void AddOption(int questionIndex)
    {
        var question = currentDeck.questions[questionIndex];
        question.options.Add(CreateOptionTemplate(question.options.Count == 0));
    }

    private void RemoveOption(int questionIndex, int optionIndex)
    {
        var question = currentDeck.questions[questionIndex];
        if (question.options.Count <= 4)
        {
            SetStatus("Cada pergunta precisa manter ao menos 4 opcoes.", true);
            return;
        }

        question.options.RemoveAt(optionIndex);
        if (CountCorrectOptions(question) == 0 && question.options.Count > 0)
        {
            question.options[0].is_correct = true;
        }
    }

    private void SetCorrectOption(int questionIndex, int optionIndex)
    {
        var question = currentDeck.questions[questionIndex];
        for (var i = 0; i < question.options.Count; i++)
        {
            question.options[i].is_correct = i == optionIndex;
        }
    }

    private static int CountCorrectOptions(DeckQuestionV2 question)
    {
        var count = 0;
        if (question?.options == null)
        {
            return count;
        }

        foreach (var option in question.options)
        {
            if (option != null && option.is_correct)
            {
                count++;
            }
        }

        return count;
    }

    private static void ExtractCatalogEntries(object categoriesObj, List<DeckCatalogItem> entries)
    {
        entries.Clear();

        if (categoriesObj is IEnumerable<object> enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is IDictionary<string, object> map)
                {
                    TryAppendCatalogItem(map, entries);
                }
            }
            return;
        }

        if (categoriesObj is System.Collections.IEnumerable rawEnumerable)
        {
            foreach (var item in rawEnumerable)
            {
                if (item is IDictionary<string, object> map)
                {
                    TryAppendCatalogItem(map, entries);
                }
                else if (item is System.Collections.IDictionary rawMap)
                {
                    var converted = new Dictionary<string, object>();
                    foreach (System.Collections.DictionaryEntry entry in rawMap)
                    {
                        if (entry.Key != null)
                        {
                            converted[entry.Key.ToString()] = entry.Value;
                        }
                    }
                    TryAppendCatalogItem(converted, entries);
                }
            }
        }
    }

    private static void TryAppendCatalogItem(IDictionary<string, object> map, List<DeckCatalogItem> entries)
    {
        if (!map.TryGetValue("nome", out var nomeObj) || !map.TryGetValue("key", out var keyObj))
        {
            return;
        }

        var nome = nomeObj == null ? string.Empty : nomeObj.ToString();
        var key = keyObj == null ? string.Empty : keyObj.ToString();

        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        entries.Add(new DeckCatalogItem
        {
            nome = nome,
            key = key
        });
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            SetStatus("Cena de destino nao configurada.", true);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void EnsureServices()
    {
        if (PlayFabService.Instance == null)
        {
            var playFabServiceGO = new GameObject("PlayFabService");
            playFabServiceGO.AddComponent<PlayFabService>();
        }

        if (AuthorizationService.Instance == null)
        {
            var authorizationGO = new GameObject("AuthorizationService");
            authorizationGO.AddComponent<AuthorizationService>();
        }

        if (AdminDeckCloudScriptService.Instance == null)
        {
            var cloudScriptGO = new GameObject("AdminDeckCloudScriptService");
            cloudScriptGO.AddComponent<AdminDeckCloudScriptService>();
        }
    }

    private void SetStatus(string message, bool isError)
    {
        statusMessage = message;
        statusIsError = isError;
    }

    private string GetDefaultCategoryName()
    {
        return string.IsNullOrWhiteSpace(categoryName) ? currentDeck.theme : categoryName;
    }

    private string GetDefaultDeckId()
    {
        return string.IsNullOrWhiteSpace(currentDeck.deck_id) ? currentKey + "_01" : currentDeck.deck_id;
    }

    private class DeckCatalogItem
    {
        public string nome;
        public string key;
    }
}
