using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Detecta planos (chão, mesa) através da câmera usando AR Foundation e permite
/// que o jogador toque na tela para "plantar" a raposinha ali no mundo real.
///
/// Requisitos no GameObject (o Editor cria a maioria automaticamente com o menu
/// GameObject > XR > AR Session Origin, ou já vem pronto no template AR):
///   - ARSession na cena
///   - Este script anexado a um objeto que também tenha ARRaycastManager e ARPlaneManager
///
/// IMPORTANTE: se o toque não funcionar, vá em Edit > Project Settings > Player >
/// Other Settings > Active Input Handling e mude para "Both" (compatibiliza o
/// Input antigo, usado aqui, com o novo Input System do Unity 6).
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class ARFoxPlacer : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Prefab da raposinha (o modelo Toon Fox importado da Asset Store)")]
    public GameObject foxPrefab;

    [Tooltip("Gerenciador de planos da AR Foundation, para esconder as marcações de chão depois de plantar a raposinha")]
    public ARPlaneManager planeManager;

    [Header("Configuração")]
    [Tooltip("Se true, tocar de novo na tela move a raposinha para o novo local. Se false, ela fica fixa após o primeiro toque.")]
    public bool allowReposition = false;

    [Tooltip("Ajuste manual de altura para a raposinha encostar direito no chão (o pivô do modelo pode não ficar exatamente nos pés). Valores negativos descem a raposinha, positivos sobem.")]
    public float groundOffset = 0f;

    [Tooltip("Escala da raposinha ao aparecer. O modelo provavelmente vem grande demais por padrão — tente valores como 0.05 a 0.3 até o tamanho ficar natural (como se fosse um cachorro pequeno/gato, a ~1-2m de distância).")]
    public float foxScale = 0.15f;

    [Tooltip("Só permite plantar a raposinha depois que isto for true. Controlado pelo StartMenuManager, para travar o jogo até a tela inicial terminar.")]
    public bool inputEnabled = false;

    /// <summary>Disparado assim que a raposinha é plantada pela primeira vez — use para iniciar o primeiro desafio de matemática.</summary>
    public System.Action OnFoxPlaced;

    private ARRaycastManager _raycastManager;
    private GameObject _spawnedFox;
    private static readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

    private void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
    }

    private void Update()
    {
        if (!inputEnabled)
            return; // menu inicial ainda não liberou o jogo

        if (_spawnedFox != null && !allowReposition)
            return; // raposinha já plantada e reposicionamento desligado -> não faz nada

        Vector2 screenPosition;

        // No celular: reage ao toque na tela.
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
        }
        // No computador (Editor/mouse): reage ao clique, para facilitar testes com a simulação de AR.
        else if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
        }
        else
        {
            return; // nenhum input relevante neste frame
        }

        // Faz o raycast contra os planos detectados (chão/mesa) dentro da área já mapeada
        if (_raycastManager.Raycast(screenPosition, _hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = _hits[0].pose;
            PlaceOrMoveFox(hitPose);
        }
    }

    private void PlaceOrMoveFox(Pose pose)
    {
        if (_spawnedFox == null)
        {
            if (foxPrefab == null)
            {
                Debug.LogWarning("ARFoxPlacer: 'foxPrefab' não foi atribuído no Inspector.");
                return;
            }

            Vector3 spawnPosition = pose.position + Vector3.up * groundOffset;

            // Sempre fica de pé e de frente pra câmera, em vez de copiar a rotação
            // da superfície detectada (que deixaria ela "de lado" numa parede, por exemplo).
            Quaternion facingRotation = Quaternion.identity;
            if (Camera.main != null)
            {
                Vector3 toCamera = Camera.main.transform.position - spawnPosition;
                toCamera.y = 0f; // ignora a altura, só gira no eixo vertical
                if (toCamera.sqrMagnitude > 0.001f)
                    facingRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }

            _spawnedFox = Instantiate(foxPrefab, spawnPosition, facingRotation);
            _spawnedFox.transform.localScale = Vector3.one * foxScale;
            HidePlaneVisuals();
            OnFoxPlaced?.Invoke();
        }
        else if (allowReposition)
        {
            _spawnedFox.transform.position = pose.position;
            _spawnedFox.transform.rotation = pose.rotation;
        }
    }

    /// <summary>
    /// Depois que a raposinha é plantada, esconde as marcações visuais dos planos
    /// detectados (aquela "grade" que aparece no chão), para deixar a cena mais limpa.
    /// </summary>
    private void HidePlaneVisuals()
    {
        if (planeManager == null)
            return;

        foreach (var plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(false);
        }
        planeManager.enabled = false;
    }

    /// <summary>Referência pública à raposinha já plantada, para outros scripts (ex: animações de acerto/erro).</summary>
    public GameObject SpawnedFox => _spawnedFox;
}