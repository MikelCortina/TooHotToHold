using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("Referencias (Se asignan solas en partida)")]
    [Tooltip("No necesitas asignarlo, el script lo buscará.")]
    public BalancingObjectController physicalChicken;

    [Tooltip("No necesitas asignarlo, buscará la cámara local.")]
    public Camera localPlayerCamera;

    [Header("Referencias de UI")]
    public RectTransform chickenIcon;

    [Header("Configuración")]
    public float uiRadius = 90f;

    private float physicalFallDistance = 0.8f; // Un valor por defecto por si acaso

    void LateUpdate()
    {
        // 1. BÚSQUEDA DINÁMICA DE LA CÁMARA
        // Si no tenemos cámara, la buscamos. Si aún no ha spawneado, cancelamos este frame y probamos en el siguiente.
        if (localPlayerCamera == null)
        {
            localPlayerCamera = Camera.main;
            if (localPlayerCamera == null) return;
        }

        // 2. BÚSQUEDA DINÁMICA DEL POLLO
        // Igual que con la cámara, si el pollo se instancia tarde, lo esperamos.
        if (physicalChicken == null)
        {
            physicalChicken = FindObjectOfType<BalancingObjectController>();

            if (physicalChicken != null)
            {
                // Cuando lo encontramos, leemos su distancia de caída real
                physicalFallDistance = physicalChicken.fallDistance;
            }
            else
            {
                return; // Si aún no hay pollo, esperamos
            }
        }

        if (chickenIcon == null) return;

        // Si llegamos aquí, es que la cámara y el pollo ya están en la escena.
        UpdateChickenIconPosition();
    }

    private void UpdateChickenIconPosition()
    {
        // Asegurarnos de que el pollo sigue teniendo a la olla como padre
        if (physicalChicken.transform.parent == null) return;

        Vector3 chickenLocalPos = physicalChicken.transform.localPosition;
        Vector2 posOnPlate = new Vector2(chickenLocalPos.x, chickenLocalPos.z);

        float camYaw = localPlayerCamera.transform.eulerAngles.y;
        float potYaw = physicalChicken.transform.parent.eulerAngles.y;

        float relativeAngle = camYaw - potYaw;

        Vector2 rotatedPos = RotateVector2(posOnPlate, -relativeAngle);

        float normalizedDistance = rotatedPos.magnitude / physicalFallDistance;
        Vector2 finalUiPos = rotatedPos.normalized * (normalizedDistance * uiRadius);

        chickenIcon.anchoredPosition = finalUiPos;
    }

    private Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
        float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);

        float tx = v.x;
        float ty = v.y;
        v.x = (cos * tx) - (sin * ty);
        v.y = (sin * tx) + (cos * ty);
        return v;
    }
}