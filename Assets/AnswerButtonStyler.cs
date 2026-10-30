using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Deixa os botões filhos ovais e coloridos. Anexe no MultipleChoiceGroup
/// (e, se quiser o mesmo visual, também no ComparisonAnswerGroup).
/// </summary>
public class AnswerButtonStyler : MonoBehaviour
{
    [Header("Uma cor por botão (repete se houver mais botões)")]
    public Color[] buttonColors =
    {
        new Color(1.00f, 0.71f, 0.76f), // rosa
        new Color(0.65f, 0.85f, 1.00f), // azul
        new Color(0.72f, 0.94f, 0.75f), // verde
        new Color(1.00f, 0.88f, 0.54f)  // amarelo
    };

    public Color textColor = new Color(0.29f, 0.17f, 0.16f);
    public float minFontSize = 28f;
    public float maxFontSize = 64f;

    private static Sprite _pillSprite;
    private bool _styled;

    private void OnEnable()
    {
        if (_styled) return;
        _styled = true;
        Style();
    }

    private void Style()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = GetPillSprite();
                img.type = Image.Type.Sliced;
                img.color = buttonColors[i % buttonColors.Length];
            }

            // O botão multiplica a cor da imagem por estas cores (efeito de clique).
            ColorBlock c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
            c.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            btn.colors = c;

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = textColor;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = minFontSize;
                label.fontSizeMax = maxFontSize;
            }
        }
    }

    // Gera um sprite em forma de pílula (com borda mais escura) e fatia em 9 partes,
    // para esticar em qualquer tamanho sem deformar as pontas.
    private static Sprite GetPillSprite()
    {
        if (_pillSprite != null) return _pillSprite;

        const int W = 128, H = 64;
        const float R = 32f;     // raio = metade da altura -> pílula perfeita
        const float ring = 3f;   // espessura da borda

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[W * H];
        Vector2 half = new Vector2(W / 2f, H / 2f);
        Vector2 inner = half - new Vector2(R, R);

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - inner;
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                          + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - R;

                float outer = Mathf.Clamp01(0.5f - d);          // forma externa (suavizada)
                float fill = Mathf.Clamp01(0.5f - (d + ring));  // miolo
                float shade = Mathf.Lerp(0.72f, 1f, fill);      // borda = versão mais escura da cor

                pixels[y * W + x] = new Color(shade, shade, shade, outer);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _pillSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(32, 31, 32, 31));
        return _pillSprite;
    }
}