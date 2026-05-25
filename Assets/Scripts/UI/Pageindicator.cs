using UnityEngine;
using UnityEngine.UI;

public class PageIndicator : MonoBehaviour
{
    [Header("Scroll")]
    public ScrollRect scrollRect;

    [Header("Dots")]
    public Image[] dots;

    [Header("Sprites")]
    public Sprite activeSprite;
    public Sprite inactiveSprite;

    [Header("Config")]
    public float stepSize = 600f;

    private int currentIndex = -1;

    void Update()
    {
        float contentPos = scrollRect.content.anchoredPosition.y;

        int newIndex = Mathf.RoundToInt(contentPos / stepSize);

        newIndex = Mathf.Clamp(newIndex, 0, dots.Length - 1);

        if (newIndex != currentIndex)
        {
            currentIndex = newIndex;
            UpdateDots();
        }
    }

    void UpdateDots()
    {
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i].sprite =
                (i == currentIndex)
                ? activeSprite
                : inactiveSprite;
        }
    }
}
