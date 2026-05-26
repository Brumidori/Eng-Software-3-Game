using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class QuickAccessHUD : MonoBehaviour
{
    [SerializeField] private Button btnMedalhas;
    [SerializeField] private Button btnRanking;

    [SerializeField] private string medalhasScene = "Medalhas";
    [SerializeField] private string rankingScene  = "Ranking";

    private void OnEnable()
    {
        if (btnMedalhas != null) btnMedalhas.onClick.AddListener(AbrirMedalhas);
        if (btnRanking  != null) btnRanking.onClick.AddListener(AbrirRanking);
    }

    private void OnDisable()
    {
        if (btnMedalhas != null) btnMedalhas.onClick.RemoveListener(AbrirMedalhas);
        if (btnRanking  != null) btnRanking.onClick.RemoveListener(AbrirRanking);
    }

    private void AbrirMedalhas() {}// => SceneManager.LoadScene(medalhasScene);
    private void AbrirRanking()  => SceneManager.LoadScene(rankingScene);
}
