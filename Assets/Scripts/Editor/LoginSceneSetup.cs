using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LoginSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Login.unity";

    private const string BgGuid       = "688b71fb6ff6645c787212454ac6213f";
    private const string CardGuid     = "22df458273f0c4b96a19ef51785965a6";
    private const string BrainGuid    = "c2e2b41bd5f064d1f9225890da89b6be";
    private const string TituloGuid   = "ba97b1f4d80634df4bf5e87946e55adb";
    private const string InputGuid    = "2405b64fbd7314657b044c72655a81df";
    private const string BotaoGuid    = "88f73d385304b47e5998ce996e5765dc";
    private const string Botao2Guid   = "2cb83a053c1a544cfb5a0c61e5d46dc3";

    [MenuItem("BrainDuel/Montar Cena de Login")]
    static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = GetOrCreateCanvas();
        ClearCanvasChildren(canvas);
        EnsureEventSystem();

        var bg    = LoadSprite(BgGuid);
        var card  = LoadSprite(CardGuid);
        var brain = LoadSprite(BrainGuid);
        var titulo = LoadSprite(TituloGuid);
        var input  = LoadSprite(InputGuid);
        var botao  = LoadSprite(BotaoGuid);
        var botao2 = LoadSprite(Botao2Guid);

        // Background
        CreateImage("Background", canvas.transform, bg,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // Titulo (BRAINDUEL) — lado direito
        var tituloObj = CreateImage("Titulo", canvas.transform, titulo,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(320f, 40f), new Vector2(650f, 185f));

        // Card de login — lado esquerdo
        var cardObj = CreateImage("CardLogin", canvas.transform, card,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-610f, 0f), new Vector2(385f, 680f));
        cardObj.GetComponent<Image>().type = Image.Type.Sliced;

        var cardTr = cardObj.transform;

        // Brain logo
        CreateImage("BrainLogo", cardTr, brain,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 255f), new Vector2(170f, 160f));

        // Titulo "Faça Login"
        CreateLabel("TituloLogin", cardTr, "Faça Login",
            new Vector2(0f, 147f), new Vector2(340f, 55f), 38, FontStyle.Bold,
            new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleCenter);

        // Label E-mail
        CreateLabel("LabelEmail", cardTr, "E-mail",
            new Vector2(-100f, 73f), new Vector2(140f, 28f), 20, FontStyle.Normal,
            new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleLeft);

        // InputField E-mail (loginInput)
        CreateInputField("loginInput", cardTr, input,
            new Vector2(0f, 33f), new Vector2(318f, 52f),
            "usuario.brainduel@fatec.com.br", InputField.ContentType.EmailAddress);

        // Label Senha
        CreateLabel("LabelSenha", cardTr, "Senha",
            new Vector2(-118f, -42f), new Vector2(100f, 28f), 20, FontStyle.Normal,
            new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleLeft);

        // InputField Senha (senhaInput)
        CreateInputField("senhaInput", cardTr, input,
            new Vector2(0f, -82f), new Vector2(318f, 52f),
            "Senha", InputField.ContentType.Password);

        // Botão LOGIN (loginBtn)
        CreateButton("loginBtn", cardTr, botao, "LOGIN",
            new Vector2(0f, -175f), new Vector2(318f, 62f),
            new Color(0f, 0.6f, 0.55f), 28, FontStyle.Bold, Color.white);

        // Texto "Ainda não tem uma conta?"
        CreateLabel("TextoConta", cardTr, "Ainda não tem uma conta?",
            new Vector2(0f, -268f), new Vector2(340f, 30f), 18, FontStyle.Bold,
            new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleCenter);

        // Botão REGISTRE-SE
        CreateButton("btnRegistro", cardTr, botao2, "REGISTRE-SE",
            new Vector2(0f, -308f), new Vector2(215f, 50f),
            new Color(0.78f, 0.58f, 0.16f), 20, FontStyle.Bold, Color.white);

        // Feedback (loginFeedback)
        CreateLabel("loginFeedback", cardTr, "",
            new Vector2(0f, -362f), new Vector2(340f, 35f), 17, FontStyle.Normal,
            Color.red, TextAnchor.MiddleCenter);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BrainDuel] Cena de Login montada com sucesso.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
        // Garante que StandaloneInputModule existe (necessário para InputField legacy)
        if (es.GetComponent<StandaloneInputModule>() == null)
            es.gameObject.AddComponent<StandaloneInputModule>();
    }

    static Sprite LoadSprite(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        // Multiple sprite sheet: pega o primeiro sub-sprite
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in all)
            if (a is Sprite s) return s;
        Debug.LogWarning($"[LoginSceneSetup] Sprite nao encontrado: {path}");
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

    static void CreateInputField(string name, Transform parent, Sprite bgSprite,
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

        // Placeholder
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

        // Text
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
    }

    static void CreateButton(string name, Transform parent, Sprite sprite, string label,
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
}
