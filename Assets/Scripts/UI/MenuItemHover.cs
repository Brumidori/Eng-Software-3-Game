using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private MenuTab tab;
    [SerializeField] private string  targetScene;
    [SerializeField] private Sprite  defaultSprite;
    [SerializeField] private Sprite  hoverSprite;
    [SerializeField] private Sprite  currentSprite;

    public MenuTab Tab           => tab;
    public string  TargetScene   => targetScene;
    public Sprite  DefaultSprite => defaultSprite;
    public Sprite  HoverSprite   => hoverSprite;
    public Sprite  CurrentSprite => currentSprite;

    public void OnPointerEnter(PointerEventData eventData)
        => MainMenuController.Instance?.OnItemHoverEnter(tab);

    public void OnPointerExit(PointerEventData eventData)
        => MainMenuController.Instance?.OnItemHoverExit(tab);
}
