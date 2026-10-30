using UnityEngine;

/// <summary>
/// Detecta toque (celular) ou clique (computador) num bichinho no mundo AR,
/// usando raycast físico contra os Colliders dos bichinhos — diferente do raycast
/// de UI que os botões já usam, então os dois não conflitam.
/// </summary>
public class CreatureTapDetector : MonoBehaviour
{
    public Camera arCamera;
    public CreatureCaptureController captureController;

    private void Update()
    {
        Vector2 screenPosition;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
        }
        else
        {
            return;
        }

        if (arCamera == null) return;

        Ray ray = arCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            CreatureInstance creature = hit.collider.GetComponent<CreatureInstance>();
            if (creature != null && creature.IsInteractable)
            {
                captureController.StartEncounter(creature);
            }
        }
    }
}