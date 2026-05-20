using UnityEngine;
using UnityEngine.EventSystems;

public class SingleEventSystem : MonoBehaviour
{
    private void Awake()
    {
        var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        if (all.Length > 1)
            Destroy(gameObject);
    }
}
