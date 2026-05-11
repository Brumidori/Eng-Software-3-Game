using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RegisterSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Register.unity";

    // Mesmos GUIDs do LoginSceneSetup — mesmos prefabs, posicoes diferentes
    private const string BgGuid     = "688b71fb6ff6645c787212454ac6213f";
    private const string CardGuid   = "22df458273f0c4b96a19ef51785965a6";
    private const string BrainGuid  = "c2e2b41bd5f064d1f9225890da89b6be";
    private const string TituloGuid = "ba97b1f4d80634df4bf5e87946e55adb";
    private const string InputGuid  = "2405b64fbd7314657b044c72655a81df";
    private const string BotaoGuid  = "88f73d385304b47e5998ce996e5765dc";
    private const string Botao2Guid = "2cb83a053c1a544cfb5a0c61e5d46dc3";

    [MenuItem("BrainDuel/Montar Cena de Registro")]
    static void Setup()
    {
        var scene = System.IO.File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var canvas = GetOrCreateCanvas();
        ClearCanvasChildren(canvas);
        EnsureEventSystem();

        var bg     = LoadSprite(BgGuid);
        var card   = LoadSprite(CardGuid);
        var brain  = LoadSprite(BrainGuid);
        var titulo = LoadSprite(TituloGuid);
        var input  = LoadSprite(InputGuid);
        var botao  = LoadSprite(BotaoGuid);
        var botao2 = LoadSprite(Botao2Guid);

        // Background
        CreateImage("Background", canvas.transform, bg,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // Brain — esquerda, grande (fora do card, ao contrário do login)
        CreateImage("BrainLogo", canvas.transform, brain,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-340f, 80f), new Vector2(260f, 250f));

        // BRAINDUEL logo — esquerda, abaixo do brain
        CreateImage("TituloLogo", canvas.transform, titulo,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-330f, -155f), new Vector2(560f, 150f));

        // Card de cadastro — direita (espelho do card de login)
        var cardObj = CreateImage("CardRegister", canvas.transform, card,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(320f, 0f), new Vector2(520f, 680f));
        cardObj.GetComponent<Image>().type = Image.Type.Sliced;

        var cardTr = cardObj.transform;

        // Titulo "Cadastro"
        CreateLabel("TituloCard", cardTr, "Cadastro",
            new Vector2(0f, 265f), new Vector2(460f, 60f), 42, FontStyle.Bold,
            new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleCenter);

        // Usuário
        CreateLabel("LabelUsuario", cardTr, "Usuário",
            new Vector2(-180f, 193f), new Vector2(100f, 28f), 20, FontStyle.Normal,
            new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleLeft);

        var nicknameGo = CreateInputField("nicknameInput", cardTr, input,
            new Vector2(0f, 153f), new Vector2(460f, 52f),
            "meu-usuario", InputField.ContentType.Standard);

        // E-mail
        CreateLabel("LabelEmail", cardTr, "E-mail",
            new Vector2(-193f, 82f), new Vector2(80f, 28f), 20, FontStyle.Normal,
            new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleLeft);

        var emailGo = CreateInputField("emailInput", cardTr, input,
            new Vector2(0f, 42f), new Vector2(460f, 52f),
            "", InputField.ContentType.EmailAddress);

        // Senha
        CreateLabel("LabelSenha", cardTr, "Senha",
            new Vector2(-198f, -30f), new Vector2(80f, 28f), 20, FontStyle.Normal,
            new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleLeft);

        var senhaGo = CreateInputField("senhaInput", cardTr, input,
            new Vector2(0f, -70f), new Vector2(460f, 52f),
            "", InputField.ContentType.Password);

        // Confirmar senha
        CreateLabel("LabelConfirmarSenha", cardTr, "Confirmar senha",
            new Vector2(-133f, -142f), new Vector2(214f, 28f), 20, FontStyle.Normal,
            new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleLeft);

        var confirmSenhaGo = CreateInputField("confirmSenhaInput", cardTr, input,
            new Vector2(0f, -182f), new Vector2(460f, 52f),
            "", InputField.ContentType.Password);

        // Botão JOGAR (teal, largura total)
        var jogarGo = CreateButton("jogarBtn", cardTr, botao, "JOGAR",
            new Vector2(0f, -258f), new Vector2(460f, 65f),
            new Color(0f, 0.6f, 0.55f), 30, FontStyle.Bold, Color.white);

        // Botão Voltar (pequeno, laranja — sem texto, só ícone/cor)
        var voltarGo = CreateButton("voltarBtn", cardTr, botao2, "",
            new Vector2(-152f, -313f), new Vector2(120f, 48f),
            new Color(0.78f, 0.58f, 0.16f), 0, FontStyle.Normal, Color.white);

        // Texto "Voltar para o Login" ao lado do botão voltar
        CreateLabel("TextoVoltar", cardTr, "Voltar para o Login",
            new Vector2(63f, -313f), new Vector2(230f, 48f), 18, FontStyle.Bold,
            new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft);

        // Feedback de erro/sucesso
        CreateLabel("registerFeedback", cardTr, "",
            new Vector2(0f, -358f), new Vector2(460f, 35f), 17, FontStyle.Normal,
            Color.red, TextAnchor.MiddleCenter);

        // Controller com RegisterScreenHandler — atribui todas as refs automaticamente
        var controllerGo = new GameObject("RegisterController");
        controllerGo.transform.SetParent(canvas.transform, false);
        var handler = controllerGo.AddComponent<RegisterScreenHandler>();

        var so = new SerializedObject(handler);
        so.FindProperty("nicknameInput").objectReferenceValue    = nicknameGo.GetComponent<InputField>();
        so.FindProperty("emailInput").objectReferenceValue       = emailGo.GetComponent<InputField>();
        so.FindProperty("senhaInput").objectReferenceValue       = senhaGo.GetComponent<InputField>();
        so.FindProperty("confirmSenhaInput").objectReferenceValue = confirmSenhaGo.GetComponent<InputField>();
        so.FindProperty("jogarBtn").objectReferenceValue         = jogarGo.GetComponent<Button>();
        so.FindProperty("voltarBtn").objectReferenceValue        = voltarGo.GetComponent<Button>();
        so.FindProperty("feedbackText").objectReferenceValue     = cardTr.Find("registerFeedback").GetComponent<Text>();
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[BrainDuel] Cena de Registro montada com sucesso.");
    }

    // ── Helpers (mesmo padrao do LoginSceneSetup) ─────────────────────────────

    static Canvas GetOrCreateCanvas()
    {
        var existing = Object.FindFirstObjectByType<Canvas>();
        if (existing != null) return existing;

        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void ClearCanvasChildren(Canvas canvas)
    {
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in canvas.transform)
            children.Add(child.gameObject);
        foreach (var c in children)
            Object.DestroyImmediate(c);
    }

    static void EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return;
        }
        if (es.GetComponent<StandaloneInputModule>() == null)
            es.gameObject.AddComponent<StandaloneInputModule>();
    }

    static Sprite LoadSprite(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is Sprite s) return s;
        Debug.LogWarning($"[RegisterSceneSetup] Sprite nao encontrado: {path}");
        return null;
    }

    static GameObject CreateImage(string name, Transform parent, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return go;
    }

    static void CreateLabel(string name, Transform parent, string text,
        Vector2 anchoredPos, Vector2 sizeDelta, int fontSize,
        FontStyle style, Color color, TextAnchor alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = alignment;
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    static GameObject CreateInputField(string name, Transform parent, Sprite bgSprite,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string placeholder, InputField.ContentType contentType)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var bg = go.AddComponent<Image>();
        bg.sprite = bgSprite;
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(go.transform, false);
        var phText = phGo.AddComponent<Text>();
        phText.text = placeholder;
        phText.font = font;
        phText.fontSize = 18;
        phText.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        phText.alignment = TextAnchor.MiddleLeft;
        var phRt = phGo.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(10, 0); phRt.offsetMax = new Vector2(-10, 0);

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = 18;
        txt.color = new Color(0.1f, 0.1f, 0.1f);
        txt.alignment = TextAnchor.MiddleLeft;
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(10, 0); txtRt.offsetMax = new Vector2(-10, 0);

        var field = go.AddComponent<InputField>();
        field.targetGraphic = bg;
        field.textComponent = txt;
        field.placeholder = phText;
        field.contentType = contentType;

        return go;
    }

    static GameObject CreateButton(string name, Transform parent, Sprite sprite, string label,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Color tint, int fontSize, FontStyle fontStyle, Color textColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = tint;
        img.type = Image.Type.Sliced;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = tint;
        colors.highlightedColor = tint * 1.15f;
        colors.pressedColor = tint * 0.85f;
        btn.colors = colors;

        if (!string.IsNullOrEmpty(label))
        {
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = fontStyle;
            txt.color = textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
        }

        return go;
    }
}
