using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FeedbackPopup : MonoBehaviour
{
    [SerializeField] private Text messageText;
    [SerializeField] private Image panelImage;

    [Header("Cores")]
    [SerializeField] private Color errorColor   = new Color(0.72f, 0.12f, 0.12f, 0.95f);
    [SerializeField] private Color successColor = new Color(0.10f, 0.52f, 0.34f, 0.95f);

    [Header("Tempos (segundos)")]
    [SerializeField] private float errorDuration   = 5f;
    [SerializeField] private float successDuration = 2.5f;
    [SerializeField] private float fadeDuration    = 0.2f;

    private CanvasGroup _group;
    private Coroutine   _routine;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();

        if (panelImage == null)
            panelImage = GetComponent<Image>();

        if (messageText == null)
            messageText = GetComponentInChildren<Text>();

        // Desativa raycastTarget somente nos elementos do proprio popup
        if (panelImage != null) panelImage.raycastTarget = false;
        if (messageText != null) messageText.raycastTarget = false;
        foreach (Transform child in transform)
        {
            foreach (var g in child.GetComponents<Graphic>())
                g.raycastTarget = false;
        }

        _group.alpha          = 0f;
        _group.interactable   = false;
        _group.blocksRaycasts = false;
    }

    public void Show(string message, bool isError)
    {
        if (messageText != null)
            messageText.text = message;
        else
            Debug.LogWarning("[FeedbackPopup] messageText nao atribuido.");

        if (panelImage != null)
            panelImage.color = isError ? errorColor : successColor;
        else
            Debug.LogWarning("[FeedbackPopup] panelImage nao atribuido.");

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(ShowRoutine(isError ? errorDuration : successDuration));

        Debug.Log($"[FeedbackPopup] Show: \"{message}\" | erro={isError}");
    }

    public void Hide()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _group.alpha          = 0f;
        _group.interactable   = false;
        _group.blocksRaycasts = false;
    }

    // Coroutine única — sem aninhamento para StopCoroutine funcionar corretamente
    private IEnumerator ShowRoutine(float duration)
    {
        // FadeIn — começa do alpha atual para evitar piscar
        float startAlpha = _group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            _group.alpha = Mathf.Lerp(startAlpha, 1f, t / fadeDuration);
            t += Time.deltaTime;
            yield return null;
        }
        _group.alpha          = 1f;
        _group.interactable   = false;
        _group.blocksRaycasts = false;

        yield return new WaitForSeconds(duration);

        // FadeOut
        t = 0f;
        while (t < fadeDuration)
        {
            _group.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            t += Time.deltaTime;
            yield return null;
        }
        _group.alpha          = 0f;
        _group.interactable   = false;
        _group.blocksRaycasts = false;
    }
}
