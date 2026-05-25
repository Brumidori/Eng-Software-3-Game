using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class MedalModalController : MonoBehaviour
{
    [SerializeField] private Transform panel;
    [SerializeField] private GameObject medalPrefab;
    [SerializeField] private Sprite[] medalSprites;

    void Start()
    {
        initializeMedals();
    }
    
    private void initializeMedals()
    {
        for (int i = 0; i < medalSprites.Length; i++)
        {
            GameObject medal = Instantiate(medalPrefab, panel);

            medal.GetComponent<Image>().sprite = medalSprites[i];
        }
    }

    private void setMedalColor (int index, Color color)
    {
        panel.GetChild(index).GetComponent<Image>().color = color;
    }
    public void setMedalColor (int index, bool hasMedal)
    {
        setMedalColor(index, hasMedal ? Color.white : new Color(1, 1, 1, 0.5f));
    }
}
