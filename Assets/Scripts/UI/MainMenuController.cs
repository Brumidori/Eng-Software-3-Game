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
        public MenuTab tab;
        public Button button;
        public Image icon;
        public Sprite defaultSprite;
        public Sprite hoverSprite;
        public Sprite currentSprite;
        public string targetScene;
    }

    [SerializeField] private MenuItemEntry[] items;

    private MenuTab _activeTab = MenuTab.None;
    private MenuTab _hoveredTab = MenuTab.None;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        foreach (var item in items)
        {
            var captured = item;
            item.button.onClick.AddListener(() => HandleItemClick(captured));
        }

        RefreshAll();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _hoveredTab = MenuTab.None;
        RefreshAll();
    }

    public void SetActiveTab(MenuTab tab)
    {
        _activeTab = tab;
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
        if (_hoveredTab == tab)
            _hoveredTab = MenuTab.None;
        RefreshAll();
    }

    private void HandleItemClick(MenuItemEntry item)
    {
        if (string.IsNullOrWhiteSpace(item.targetScene)) return;
        if (SceneManager.GetActiveScene().name == item.targetScene) return;

        SetActiveTab(item.tab);
        SceneManager.LoadScene(item.targetScene);
    }

    private void RefreshAll()
    {
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
}
