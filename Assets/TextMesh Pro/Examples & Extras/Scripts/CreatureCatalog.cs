using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Livro de bichinhos (catálogo): guarda quais já foram capturados, mostra a mensagem
/// de captura, e controla o livro que abre e "vira de página". Cada bichinho aparece
/// como uma figurinha 2D: colorida se já foi capturado, silhueta preta se ainda não.
/// </summary>
public class CreatureCatalog : MonoBehaviour
{
    public static CreatureCatalog Instance { get; private set; }

    [Serializable]
    public class CatalogEntry
    {
        [Tooltip("Precisa ser IGUAL ao 'Creature Name' do prefab do bichinho")]
        public string creatureName;

        [Tooltip("Figurinha 2D colorida do bichinho (PNG com fundo transparente, importado como Sprite)")]
        public Sprite icon;
    }

    [Header("Bichinhos do jogo (na ordem em que aparecem no livro)")]
    public List<CatalogEntry> entries = new List<CatalogEntry>();

    [Header("Botão do livro (fica no topo da tela)")]
    public Button bookButton;
    public RectTransform bookButtonRect; // pra onde o bichinho voa ao ser capturado

    [Header("Livro aberto")]
    public GameObject bookPanel;       // painel que cobre a tela e liga/desliga
    public RectTransform bookContent;  // o livro em si (arte + figurinhas), que "abre" e "vira"
    public Image[] slotImages;         // as figurinhas (uma por espaço da página)
    public TMP_Text[] slotNames;       // os nomes embaixo de cada figurinha
    public Button previousPageButton;
    public Button nextPageButton;
    public Button closeButton;
    public TMP_Text infoText;          // "Página 1/2   Capturados: 3/8"

    [Header("Mensagem de captura")]
    public GameObject celebrationPanel;
    public TMP_Text celebrationTitle;  // "CAPTUROU!"
    public TMP_Text celebrationName;   // "Coelho agora está no seu livro!"
    public float celebrationSeconds = 2.4f;

    [Header("Testes e apresentação")]
    [Tooltip("Se marcado, o livro começa vazio toda vez que o jogo abre (útil nos testes e na apresentação). Desmarque para o progresso ficar salvo entre uma partida e outra.")]
    public bool resetProgressOnStart = false;

    private const string PrefsPrefix = "captured_";
    private int _currentPage = 0;
    private bool _flipping = false;

    private void Awake()
    {
        Instance = this;

        if (resetProgressOnStart)
            ResetCatalog();

        if (bookPanel != null) bookPanel.SetActive(false);
        if (celebrationPanel != null) celebrationPanel.SetActive(false);

        if (bookButton != null) bookButton.onClick.AddListener(OpenBook);
        if (closeButton != null) closeButton.onClick.AddListener(CloseBook);
        if (previousPageButton != null) previousPageButton.onClick.AddListener(() => ChangePage(-1));
        if (nextPageButton != null) nextPageButton.onClick.AddListener(() => ChangePage(1));
    }

    // ------------------------------------------------------------------
    // Quais bichinhos já foram capturados (fica salvo, mesmo fechando o app)
    // ------------------------------------------------------------------

    public bool IsCaptured(string creatureName)
    {
        return PlayerPrefs.GetInt(PrefsPrefix + creatureName, 0) == 1;
    }

    public void MarkCaptured(string creatureName)
    {
        PlayerPrefs.SetInt(PrefsPrefix + creatureName, 1);
        PlayerPrefs.Save();
        RefreshPage();
    }

    /// <summary>Para testes: apaga tudo que foi capturado (botão direito no componente no Inspector).</summary>
    [ContextMenu("Resetar catálogo (teste)")]
    public void ResetCatalog()
    {
        foreach (var entry in entries)
        {
            PlayerPrefs.DeleteKey(PrefsPrefix + entry.creatureName);
            CreatureInstance.ClearCooldown(entry.creatureName);
        }

        PlayerPrefs.Save();
        RefreshPage();
    }

    // ------------------------------------------------------------------
    // Mensagem espalhafatosa de captura + o livro "pulsando" quando o bichinho chega
    // ------------------------------------------------------------------

    public void ShowCaptureCelebration(string creatureName)
    {
        if (celebrationPanel == null) return;
        StartCoroutine(CelebrationRoutine(creatureName));
    }

    private IEnumerator CelebrationRoutine(string creatureName)
    {
        if (celebrationTitle != null) celebrationTitle.text = "CAPTUROU!";
        if (celebrationName != null) celebrationName.text = creatureName + " agora está no seu livro!";

        celebrationPanel.SetActive(true);

        Transform panelTransform = celebrationPanel.transform;
        float duration = 0.45f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float scale = EaseOutBack(Mathf.Clamp01(t / duration));
            panelTransform.localScale = Vector3.one * scale;
            yield return null;
        }

        panelTransform.localScale = Vector3.one;

        yield return new WaitForSeconds(celebrationSeconds);
        celebrationPanel.SetActive(false);
    }

    public void PulseBookIcon()
    {
        if (bookButtonRect != null)
            StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        float duration = 0.4f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float scale = 1f + 0.4f * Mathf.Sin(k * Mathf.PI);
            bookButtonRect.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        bookButtonRect.localScale = Vector3.one;
    }

    // ------------------------------------------------------------------
    // O livro: abrir, fechar e virar páginas
    // ------------------------------------------------------------------

    public void OpenBook()
    {
        if (bookPanel == null) return;

        bookPanel.SetActive(true);
        RefreshPage();
        StartCoroutine(OpenAnimation());
    }

    public void CloseBook()
    {
        if (bookPanel != null) bookPanel.SetActive(false);
    }

    private IEnumerator OpenAnimation()
    {
        if (bookContent == null) yield break;

        float duration = 0.35f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float scale = Mathf.LerpUnclamped(0.3f, 1f, EaseOutBack(Mathf.Clamp01(t / duration)));
            bookContent.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        bookContent.localScale = Vector3.one;
    }

    private void ChangePage(int delta)
    {
        if (_flipping) return;

        int newPage = _currentPage + delta;
        if (newPage < 0 || newPage >= TotalPages()) return;

        StartCoroutine(FlipAnimation(newPage));
    }

    // "Virar a página" simulado: o livro dá uma leve apertada na largura,
    // troca o conteúdo no meio, e volta ao normal.
    private IEnumerator FlipAnimation(int newPage)
    {
        _flipping = true;
        float half = 0.12f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            SetBookScaleX(Mathf.Lerp(1f, 0.82f, t / half));
            yield return null;
        }

        _currentPage = newPage;
        RefreshPage();

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            SetBookScaleX(Mathf.Lerp(0.82f, 1f, t / half));
            yield return null;
        }

        SetBookScaleX(1f);
        _flipping = false;
    }

    private void SetBookScaleX(float x)
    {
        if (bookContent != null)
            bookContent.localScale = new Vector3(x, 1f, 1f);
    }

    private int TotalPages()
    {
        int perPage = (slotImages != null && slotImages.Length > 0) ? slotImages.Length : 1;
        return Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)perPage));
    }

    /// <summary>Preenche as figurinhas da página atual: coloridas se capturadas, silhueta preta se não.</summary>
    private void RefreshPage()
    {
        if (slotImages == null || slotImages.Length == 0) return;

        int perPage = slotImages.Length;
        int totalPages = TotalPages();
        _currentPage = Mathf.Clamp(_currentPage, 0, totalPages - 1);

        int capturedCount = 0;
        foreach (var entry in entries)
            if (IsCaptured(entry.creatureName)) capturedCount++;

        for (int i = 0; i < perPage; i++)
        {
            if (slotImages[i] == null) continue;

            int index = _currentPage * perPage + i;
            bool hasEntry = index < entries.Count;
            bool hasName = slotNames != null && i < slotNames.Length && slotNames[i] != null;

            slotImages[i].gameObject.SetActive(hasEntry);
            if (hasName) slotNames[i].gameObject.SetActive(hasEntry);
            if (!hasEntry) continue;

            CatalogEntry entry = entries[index];
            bool captured = IsCaptured(entry.creatureName);

            slotImages[i].sprite = entry.icon;
            slotImages[i].color = captured ? Color.white : Color.black; // preto = silhueta
            if (hasName) slotNames[i].text = captured ? entry.creatureName : "???";
        }

        if (previousPageButton != null) previousPageButton.interactable = _currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = _currentPage < totalPages - 1;

        if (infoText != null)
            infoText.text = $"Página {_currentPage + 1}/{totalPages}   Capturados: {capturedCount}/{entries.Count}";
    }

    // Curva de animação com um "quique" no final (passa um pouco do tamanho e volta)
    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }
}