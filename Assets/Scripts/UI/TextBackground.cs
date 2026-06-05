using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Adiciona um fundo semi-transparente atrás de um TMP_Text.
/// Basta anexar este componente ao mesmo GameObject que contém o TMP_Text.
/// Configurável pelo Inspector: cor, padding e raio de arredondamento (sprite).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public class TextBackground : MonoBehaviour
{
    [Tooltip("Cor do fundo (ajuste o alpha para controlar a transparência)")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("Padding em pixels ao redor do texto")]
    [SerializeField] private Vector2 padding = new Vector2(12f, 6f);

    [Tooltip("Sprite opcional com bordas arredondadas (Nine-Slice). Deixe vazio para retângulo sólido.")]
    [SerializeField] private Sprite backgroundSprite;

    private Image    _bgImage;
    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        CreateBackground();
    }

    private void CreateBackground()
    {
        // Cria um GameObject irmão que fica logo atrás no hierarquia
        var bgGO = new GameObject("_TextBackground");
        bgGO.transform.SetParent(transform.parent, false);
        bgGO.transform.SetSiblingIndex(transform.GetSiblingIndex());

        _bgImage        = bgGO.AddComponent<Image>();
        _bgImage.color  = backgroundColor;
        _bgImage.raycastTarget = false;

        if (backgroundSprite != null)
        {
            _bgImage.sprite = backgroundSprite;
            _bgImage.type   = Image.Type.Sliced;
        }

        // Copia o RectTransform do texto e aplica o padding
        var textRect = GetComponent<RectTransform>();
        var bgRect   = bgGO.GetComponent<RectTransform>();

        bgRect.anchorMin        = textRect.anchorMin;
        bgRect.anchorMax        = textRect.anchorMax;
        bgRect.pivot            = textRect.pivot;
        bgRect.anchoredPosition = textRect.anchoredPosition;
        bgRect.sizeDelta        = textRect.sizeDelta + padding * 2f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_bgImage != null)
            _bgImage.color = backgroundColor;
    }
#endif
}
