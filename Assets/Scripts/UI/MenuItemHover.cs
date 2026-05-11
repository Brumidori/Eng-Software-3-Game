using UnityEngine;
using UnityEngine.EventSystems;

public class MenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private MenuTab tab;

    public void OnPointerEnter(PointerEventData eventData)
    {
        MainMenuController.Instance?.OnItemHoverEnter(tab);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        MainMenuController.Instance?.OnItemHoverExit(tab);
    }
}
