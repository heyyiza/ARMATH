using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Anexado a cada bichinho. Controla o estado: disponível, recarregando depois de
/// uma falha (a hora fica salva, então fechar o app não burla), ou capturado.
/// Fica invisível até a criança chegar perto E apontar a câmera pra ele.
/// Ao ser capturado, gira, encolhe e voa até o botão do livro.
///
/// Para esconder, desliga só o visual e o Collider (objeto desligado não roda código).
/// </summary>
public class CreatureInstance : MonoBehaviour
{
    public enum State { Available, OnCooldown, Captured }

    [Header("Identidade (precisa ser IGUAL ao nome na lista do CreatureCatalog)")]
    public string creatureName = "Raposinha";

    [Header("Aparecer só quando a criança chegar perto")]
    [Tooltip("Distância horizontal (m) em que o bichinho aparece. 0 = sempre visível. O CreatureSpawner ajusta isso.")]
    public float revealDistance = 0f;

    [Tooltip("Ao aparecer, vira de frente pra câmera")]
    public bool faceCameraOnReveal = true;

    [Header("Animação de captura")]
    public float captureFlightSeconds = 1.4f;

    public State CurrentState { get; private set; } = State.Available;
    public bool IsRevealed { get; private set; } = true;

    public bool IsInteractable => CurrentState == State.Available && IsRevealed;

    private const string CooldownPrefix = "cooldown_until_";

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private Vector3 _shownScale;
    private Coroutine _popRoutine;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        _shownScale = transform.localScale;
    }

    private void Start()
    {
        // Bichinho colocado à mão com "Reveal Distance" preenchida: começa escondido
        if (revealDistance > 0f && IsRevealed)
        {
            IsRevealed = false;
            SetVisuals(false);
        }
    }

    /// <summary>Chamado pelo CreatureSpawner depois de criar e ajustar o tamanho do bichinho.</summary>
    public void SetupForSpawn(float reveal)
    {
        revealDistance = reveal;
        _shownScale = transform.localScale; // escala final já ajustada pelo spawner

        if (revealDistance > 0f)
        {
            IsRevealed = false;
            SetVisuals(false);
        }
    }

    private void Update()
    {
        if (CurrentState == State.OnCooldown)
        {
            if (!IsOnCooldown(creatureName))
            {
                CurrentState = State.Available;
                HideUntilNear();
            }
            return;
        }

        if (CurrentState == State.Available && !IsRevealed)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 a = cam.transform.position;
            Vector3 b = transform.position;
            a.y = 0f;
            b.y = 0f;

            if (Vector3.Distance(a, b) <= revealDistance && IsInCameraView(cam))
                Reveal();
        }
    }

    // Está dentro da tela (com uma folga nas bordas)?
    private bool IsInCameraView(Camera cam)
    {
        Vector3 p = cam.WorldToViewportPoint(transform.position + Vector3.up * 0.1f);
        return p.z > 0f && p.x > 0.05f && p.x < 0.95f && p.y > 0.05f && p.y < 0.95f;
    }

    // ------------------------------------------------------------------
    // Recarga (guardada com a hora real, sobrevive a fechar o app)
    // ------------------------------------------------------------------

    public static bool IsOnCooldown(string name)
    {
        return GetCooldownEnd(name) > DateTime.UtcNow;
    }

    public static void ClearCooldown(string name)
    {
        PlayerPrefs.DeleteKey(CooldownPrefix + name);
    }

    private static DateTime GetCooldownEnd(string name)
    {
        string saved = PlayerPrefs.GetString(CooldownPrefix + name, "");

        if (long.TryParse(saved, out long ticks) && ticks > 0)
            return new DateTime(ticks, DateTimeKind.Utc);

        return DateTime.MinValue;
    }

    /// <summary>Chamado quando a criança erra além do permitido e não consegue capturar.</summary>
    public void FailCaptureAndCooldown(float cooldownSeconds)
    {
        CurrentState = State.OnCooldown;

        DateTime end = DateTime.UtcNow.AddSeconds(cooldownSeconds);
        PlayerPrefs.SetString(CooldownPrefix + creatureName, end.Ticks.ToString());
        PlayerPrefs.Save();

        SetVisuals(false); // "foge": some da vista, mas o objeto continua ativo
    }

    private void HideUntilNear()
    {
        if (revealDistance > 0f)
        {
            IsRevealed = false;
            SetVisuals(false);
        }
        else
        {
            Reveal();
        }
    }

    // ------------------------------------------------------------------
    // Aparecer
    // ------------------------------------------------------------------

    private void Reveal()
    {
        IsRevealed = true;
        SetVisuals(true);

        Camera cam = Camera.main;
        if (faceCameraOnReveal && cam != null)
        {
            Vector3 toCamera = cam.transform.position - transform.position;
            toCamera.y = 0f;

            if (toCamera.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(PopIn());
    }

    private IEnumerator PopIn()
    {
        float duration = 0.45f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float scale = Mathf.Max(0.01f, EaseOutBack(Mathf.Clamp01(t / duration)));
            transform.localScale = _shownScale * scale;
            yield return null;
        }

        transform.localScale = _shownScale;
    }

    private void SetVisuals(bool visible)
    {
        if (_renderers != null)
            foreach (Renderer r in _renderers)
                if (r != null) r.enabled = visible;

        if (_colliders != null)
            foreach (Collider c in _colliders)
                if (c != null) c.enabled = visible;
    }

    // ------------------------------------------------------------------
    // Captura: gira, encolhe e voa até o livro
    // ------------------------------------------------------------------

    /// <summary>Chamado quando a criança completa as perguntas corretamente.</summary>
    public void Capture()
    {
        if (CurrentState == State.Captured) return;
        CurrentState = State.Captured;

        if (_popRoutine != null) StopCoroutine(_popRoutine);

        if (CreatureCatalog.Instance != null)
            CreatureCatalog.Instance.ShowCaptureCelebration(creatureName);

        StartCoroutine(FlyToBook());
    }

    private IEnumerator FlyToBook()
    {
        Camera cam = Camera.main;
        RectTransform target = CreatureCatalog.Instance != null ? CreatureCatalog.Instance.bookButtonRect : null;

        if (cam == null || target == null)
        {
            FinishCapture();
            yield break;
        }

        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        float t = 0f;

        while (t < captureFlightSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / captureFlightSeconds);
            float eased = k * k;

            Vector3 screenTarget = new Vector3(target.position.x, target.position.y, 0.6f);
            Vector3 endPosition = cam.ScreenToWorldPoint(screenTarget);

            transform.position = Vector3.Lerp(startPosition, endPosition, eased);
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.05f, eased);
            transform.Rotate(0f, 900f * Time.deltaTime, 0f, Space.World);

            yield return null;
        }

        FinishCapture();
    }

    private void FinishCapture()
    {
        if (CreatureCatalog.Instance != null)
        {
            CreatureCatalog.Instance.MarkCaptured(creatureName);
            CreatureCatalog.Instance.PulseBookIcon();
        }

        Destroy(gameObject);
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }
}