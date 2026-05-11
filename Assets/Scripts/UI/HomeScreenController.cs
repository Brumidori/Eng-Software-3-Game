using UnityEngine;

public class HomeScreenController : MonoBehaviour
{
    private void Start()
    {
        MainMenuController.Instance?.SetActiveTab(MenuTab.Duelo);
    }
}
