using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    [Serializable]
    public class MenuItemEntry
    {
        public MenuTab   tab;
        public Button    button;
        public Image     icon;
        public Sprite    defaultSprite;
        public Sprite    hoverSprite;
        public Sprite    currentSprite;
        public string    targetScene;
    }

    [SerializeField] private MenuItemEntry[] items;

    private MenuTab _activeTab  = MenuTab.None;
    private MenuTab _hoveredTab = MenuTab.None;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Se o array não foi preenchido no Inspector, tenta montar
        // automaticamente a partir dos filhos que tenham Button + Image.
        if (items == null || items.Length == 0)
            AutoWire();

        foreach (var item in items)
        {
            if (item.button == null) continue;
            var captured = item;
            item.button.onClick.AddListener(() => HandleItemClick(captured));
        }

        RefreshAll();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    public void SetActiveTab(MenuTab tab)
    {
        _activeTab  = tab;
        _hoveredTab = MenuTab.None;
        RefreshAll();
    }

    public void OnItemHoverEnter(MenuTab tab)
    {
        if (tab == _activeTab) return;
        _hoveredTab = tab;
        RefreshAll();
    }

    public void OnItemHoverExit(MenuTab tab)
    {
        if (_hoveredTab == tab) _hoveredTab = MenuTab.None;
        RefreshAll();
    }

    // ── Internos ──────────────────────────────────────────────────────────────

    private void HandleItemClick(MenuItemEntry item)
    {
        if (string.IsNullOrWhiteSpace(item.targetScene)) return;
        if (SceneManager.GetActiveScene().name == item.targetScene) return;

        SetActiveTab(item.tab);
        SceneManager.LoadScene(item.targetScene);
    }

    private void RefreshAll()
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item.icon == null) continue;

            if (item.tab == _activeTab)
                item.icon.sprite = item.currentSprite;
            else if (item.tab == _hoveredTab)
                item.icon.sprite = item.hoverSprite;
            else
                item.icon.sprite = item.defaultSprite;
        }
    }

    // Descobre os botões filhos em ordem e preenche o array em runtime.
    // Requer que cada MenuitemX tenha um Button e um filho Image.
    // Os sprites e a cena precisam ser configurados no Inspector OU via
    // MenuItemHover (que já guarda o tab).
    private void AutoWire()
    {
        var hovers = GetComponentsInChildren<MenuItemHover>(true);
        items = new MenuItemEntry[hovers.Length];

        for (int i = 0; i < hovers.Length; i++)
        {
            var hov  = hovers[i];
            var btn  = hov.GetComponent<Button>();
            var icon = FindChildImage(hov.transform);

            items[i] = new MenuItemEntry
            {
                tab           = hov.Tab,
                button        = btn,
                icon          = icon,
                targetScene   = hov.TargetScene,
                defaultSprite = hov.DefaultSprite,
                hoverSprite   = hov.HoverSprite,
                currentSprite = hov.CurrentSprite,
            };

            Debug.Log($"[MainMenu] AutoWire: {hov.Tab} | btn={btn != null} | icon={icon != null} | scene='{hov.TargetScene}'");
        }
    }

    // Retorna a Image de um filho direto, ignorando a Image do próprio objeto.
    private static Image FindChildImage(Transform parent)
    {
        foreach (Transform child in parent)
        {
            var img = child.GetComponent<Image>();
            if (img != null) return img;
        }
        return null;
    }
}
