using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Espalha os bichinhos pelo ambiente à medida que o AR descobre superfícies
/// horizontais (chão, mesas, camas). Mede cada modelo, ajusta o tamanho em metros
/// e apoia no chão pelo ponto mais baixo. Bichinhos capturados ou recarregando
/// não nascem de novo.
/// </summary>
public class CreatureSpawner : MonoBehaviour
{
    [System.Serializable]
    public class CreatureSpecies
    {
        [Tooltip("Prefab do bichinho (com Collider, Creature Instance e Animator)")]
        public GameObject prefab;

        [Tooltip("Tamanho no mundo, em METROS: a maior medida do bichinho. Ex: 0.4 = gato pequeno. O script mede o modelo e ajusta a escala sozinho.")]
        public float targetSize = 0.4f;

        [Tooltip("Ajuste fino de altura. Negativo desce, positivo sobe.")]
        public float groundOffset = 0f;
    }

    [Header("Referências")]
    [Tooltip("O AR Plane Manager (fica no XR Origin)")]
    public ARPlaneManager planeManager;

    [Tooltip("A câmera do AR (Main Camera dentro do XR Origin). Se vazio, usa a Main Camera.")]
    public Camera arCamera;

    [Header("Bichinhos do jogo")]
    public List<CreatureSpecies> species = new List<CreatureSpecies>();

    [Header("Quantos e com que ritmo")]
    public int maxCreaturesAtOnce = 3;
    public float secondsBetweenSpawns = 5f;
    [Tooltip("Área mínima (m²) de uma superfície para um bichinho nascer nela")]
    public float minPlaneArea = 0.3f;

    [Header("Onde nascem (em metros)")]
    public float minDistanceFromCamera = 1.2f;
    public float maxDistanceFromCamera = 5f;
    public float minDistanceBetweenCreatures = 1.2f;

    [Header("Descobrir os bichinhos")]
    [Tooltip("Distância (horizontal) em que o bichinho aparece, se estiver na tela")]
    public float revealDistance = 4f;

    [Tooltip("Distância em que aparece o aviso 'tem um bichinho por perto!'")]
    public float hintDistance = 6f;

    public GameObject hintPanel;
    public GameObject scanHintPanel;

    [Header("Visual do chão detectado")]
    public bool showPlaneVisuals = true;

    [Header("Diagnóstico")]
    public bool debugLogs = true;
    [Tooltip("Opcional: texto na tela com planos, bichinhos e o motivo de não nascer")]
    public TMP_Text debugText;

    private readonly List<CreatureInstance> _spawned = new List<CreatureInstance>();
    private bool _running;
    private float _nextSpawnTime;
    private float _lastLogTime = -100f;
    private string _lastReason = "-";

    /// <summary>Chamado pelo StartMenuManager quando a criança escolhe a idade.</summary>
    public void StartSpawning()
    {
        if (_running) return;
        _running = true;

        if (arCamera == null) arCamera = Camera.main;

        // Garante que o detector de planos está ligado
        if (planeManager != null) planeManager.enabled = true;

        _nextSpawnTime = Time.time + 1f;
        if (scanHintPanel != null) scanHintPanel.SetActive(true);
        if (hintPanel != null) hintPanel.SetActive(false);

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(0.5f);

        while (true)
        {
            yield return wait;

            HidePlaneVisualsIfNeeded();
            CleanUpList();
            UpdateHint();

            if (Time.time >= _nextSpawnTime)
                TrySpawnOne();

            UpdateDebugText();
        }
    }

    // ------------------------------------------------------------------
    // Decidir se nasce mais um bichinho, qual, e onde
    // ------------------------------------------------------------------

    private void TrySpawnOne()
    {
        if (planeManager == null || arCamera == null)
        {
            LogThrottled("FALTA REFERENCIA: Plane Manager ou Ar Camera vazio no Spawner.");
            return;
        }

        int availableNow = 0;
        foreach (CreatureInstance c in _spawned)
            if (c != null && c.CurrentState == CreatureInstance.State.Available)
                availableNow++;

        if (availableNow >= maxCreaturesAtOnce)
        {
            LogThrottled("Ja existem " + availableNow + " bichinhos no mundo (limite).");
            return;
        }

        CreatureSpecies chosen = PickEligibleSpecies();
        if (chosen == null)
        {
            LogThrottled("Nenhum bichinho disponivel pra nascer (todos capturados, recarregando ou ja no mundo, ou lista Species vazia).");
            return;
        }

        if (!TryFindSpawnPoint(out Vector3 point)) return;

        SpawnCreature(chosen, point);
        _nextSpawnTime = Time.time + secondsBetweenSpawns;
    }

    private CreatureSpecies PickEligibleSpecies()
    {
        var eligible = new List<CreatureSpecies>();

        foreach (CreatureSpecies s in species)
        {
            if (s == null || s.prefab == null) continue;

            CreatureInstance prefabInstance = s.prefab.GetComponent<CreatureInstance>();
            if (prefabInstance == null) continue;

            string creatureName = prefabInstance.creatureName;

            if (CreatureCatalog.Instance != null && CreatureCatalog.Instance.IsCaptured(creatureName)) continue;
            if (CreatureInstance.IsOnCooldown(creatureName)) continue;
            if (IsAlreadySpawned(creatureName)) continue;

            eligible.Add(s);
        }

        if (eligible.Count == 0) return null;
        return eligible[Random.Range(0, eligible.Count)];
    }

    private bool IsAlreadySpawned(string creatureName)
    {
        foreach (CreatureInstance c in _spawned)
            if (c != null && c.creatureName == creatureName)
                return true;

        return false;
    }

    private bool TryFindSpawnPoint(out Vector3 result)
    {
        result = Vector3.zero;
        Vector3 cameraPosition = arCamera.transform.position;

        var usablePlanes = new List<ARPlane>();
        int totalPlanes = 0;

        foreach (ARPlane plane in planeManager.trackables)
        {
            totalPlanes++;

            if (plane.subsumedBy != null) continue;
            if (plane.trackingState != TrackingState.Tracking) continue;
            if (!TryGetPlaneInfo(plane, out _, out _, out float area)) continue;
            if (area < minPlaneArea) continue;

            usablePlanes.Add(plane);
        }

        if (usablePlanes.Count == 0)
        {
            LogThrottled("Sem superficie grande o bastante (" + totalPlanes +
                         " planos). Mova o celular devagar sobre o chao ou uma mesa.");
            return false;
        }

        for (int attempt = 0; attempt < 30; attempt++)
        {
            ARPlane plane = usablePlanes[Random.Range(0, usablePlanes.Count)];
            TryGetPlaneInfo(plane, out Vector2 min, out Vector2 max, out _);

            Vector2 local = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
            if (!IsInsidePlane(plane, local)) continue;

            Vector3 world = plane.transform.TransformPoint(new Vector3(local.x, 0f, local.y));

            float distance = Vector3.Distance(world, cameraPosition);
            if (distance < minDistanceFromCamera || distance > maxDistanceFromCamera) continue;
            if (IsTooCloseToOtherCreature(world)) continue;

            result = world;
            return true;
        }

        LogThrottled("Achei " + usablePlanes.Count + " superficie(s), mas nenhum ponto bom " +
                     "(muito perto/longe ou colado em outro bichinho). Ande um pouco.");
        return false;
    }

    private bool IsTooCloseToOtherCreature(Vector3 point)
    {
        foreach (CreatureInstance c in _spawned)
        {
            if (c == null) continue;
            if (Vector3.Distance(point, c.transform.position) < minDistanceBetweenCreatures)
                return true;
        }

        return false;
    }

    private void SpawnCreature(CreatureSpecies s, Vector3 point)
    {
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject created = Instantiate(s.prefab, point, rotation);

        // 1) escala: a maior medida do modelo vira "targetSize" metros
        Bounds b = GetBounds(created);
        float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (biggest > 0.0001f)
            created.transform.localScale *= s.targetSize / biggest;

        // 2) altura: o ponto mais baixo do modelo encosta no chão
        b = GetBounds(created);
        float groundY = point.y + s.groundOffset;
        created.transform.position += Vector3.up * (groundY - b.min.y);

        CreatureInstance creature = created.GetComponent<CreatureInstance>();
        creature.SetupForSpawn(revealDistance);

        _spawned.Add(creature);
        if (scanHintPanel != null) scanHintPanel.SetActive(false);

        if (debugLogs)
            Debug.Log("[Spawner] Nasceu: " + creature.creatureName);
    }

    private static Bounds GetBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;

        foreach (Renderer r in renderers)
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }

        return bounds;
    }

    // ------------------------------------------------------------------
    // Manutenção
    // ------------------------------------------------------------------

    private void CleanUpList()
    {
        _spawned.RemoveAll(c => c == null || c.CurrentState == CreatureInstance.State.Captured);
    }

    private void UpdateHint()
    {
        if (hintPanel == null || arCamera == null) return;

        bool somethingNearby = false;

        foreach (CreatureInstance c in _spawned)
        {
            if (c == null) continue;
            if (c.CurrentState != CreatureInstance.State.Available || c.IsRevealed) continue;

            if (HorizontalDistance(arCamera.transform.position, c.transform.position) <= hintDistance)
            {
                somethingNearby = true;
                break;
            }
        }

        hintPanel.SetActive(somethingNearby);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void UpdateDebugText()
    {
        if (debugText == null) return;

        int planes = planeManager != null ? planeManager.trackables.count : -1;
        int hidden = 0;
        float nearest = -1f;

        foreach (CreatureInstance c in _spawned)
        {
            if (c == null) continue;
            if (!c.IsRevealed) hidden++;
            if (arCamera == null) continue;

            float d = HorizontalDistance(arCamera.transform.position, c.transform.position);
            if (nearest < 0f || d < nearest) nearest = d;
        }

        debugText.text = "planos: " + planes +
                         "\nbichinhos: " + _spawned.Count + " (escondidos " + hidden + ")" +
                         "\nmais perto: " + (nearest < 0f ? "-" : nearest.ToString("0.0") + " m") +
                         "\n" + _lastReason;
    }

    private void HidePlaneVisualsIfNeeded()
    {
        if (showPlaneVisuals || planeManager == null) return;

        foreach (ARPlane plane in planeManager.trackables)
            foreach (Renderer r in plane.GetComponentsInChildren<Renderer>())
                r.enabled = false;
    }

    // ------------------------------------------------------------------
    // Geometria do plano
    // ------------------------------------------------------------------

    private static bool TryGetPlaneInfo(ARPlane plane, out Vector2 min, out Vector2 max, out float area)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        area = 0f;

        var boundary = plane.boundary;
        if (!boundary.IsCreated || boundary.Length < 3) return false;

        min = boundary[0];
        max = boundary[0];
        float twiceArea = 0f;

        for (int i = 0; i < boundary.Length; i++)
        {
            Vector2 a = boundary[i];
            Vector2 b = boundary[(i + 1) % boundary.Length];

            twiceArea += a.x * b.y - b.x * a.y;
            min = Vector2.Min(min, a);
            max = Vector2.Max(max, a);
        }

        area = Mathf.Abs(twiceArea) * 0.5f;
        return area > 0f;
    }

    private static bool IsInsidePlane(ARPlane plane, Vector2 point)
    {
        var boundary = plane.boundary;
        int count = boundary.Length;
        bool inside = false;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = boundary[i];
            Vector2 b = boundary[j];

            bool crosses = (a.y > point.y) != (b.y > point.y);
            if (crosses && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }

        return inside;
    }

    private void LogThrottled(string message)
    {
        _lastReason = message;
        if (!debugLogs || Time.time - _lastLogTime < 5f) return;

        _lastLogTime = Time.time;
        Debug.Log("[Spawner] " + message);
    }
}