using UnityEngine;
using UnityEngine.UI;

public class StepIndicator : MonoBehaviour
{
    public ScrollRect scrollRect;

    public Image[] points;

    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1, 1, 1, 0.3f);

    int currentIndex;

    void Update()
    {
        if (scrollRect == null)
        {
            Debug.LogError("ScrollRect não atribuído!");
            return;
        }

        if (points == null || points.Length == 0)
        {
            Debug.LogError("Points vazio!");
            return;
        }

        float pos = scrollRect.verticalNormalizedPosition;

        int index = Mathf.RoundToInt(
            (1 - pos) * (points.Length - 1)
        );

        if (index != currentIndex)
        {
            currentIndex = index;
            UpdatePoints();
        }
    }

    void UpdatePoints()
    {
        for (int i = 0; i < points.Length; i++)
        {
            points[i].color =
                i == currentIndex
                ? activeColor
                : inactiveColor;
        }
    }
}
