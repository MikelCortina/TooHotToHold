using UnityEngine;
using Fusion;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Configuración")]
    public NetworkObject targetNetworkObject;

    [Tooltip("Arrastra aquí el modelo 3D HIJO del jugador, el mismo que pusiste en Interpolation Target")]
    public Transform interpolationTarget;

    public float distance = 5f;
    public float mouseSensitivity = 2f;
    public Vector2 pitchMinMax = new Vector2(-40, 85);

    // NUEVO: Límite horizontal de la cámara
    [Header("Límites de Cámara")]
    [Tooltip("Ángulo máximo horizontal (izquierda/derecha) respecto al cuerpo. 90 = 180 grados en total.")]
    public float maxYawAngle = 90f;

    private float yaw;
    private float pitch;

    void Start()
    {
        if (targetNetworkObject != null && targetNetworkObject.HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Inicializamos el yaw/pitch con la rotación inicial para evitar un "salto" brusco al empezar
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (targetNetworkObject == null || !targetNetworkObject.HasInputAuthority) return;

        // 1. Recogemos el input visual
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        // NUEVO: Limitar el giro horizontal (Yaw) respecto a la rotación del cuerpo padre
        float rootYaw = targetNetworkObject.transform.eulerAngles.y;

        // Calculamos cuántos grados de diferencia hay entre hacia dónde mira el cuerpo y hacia dónde quiere mirar la cámara
        float deltaYaw = Mathf.DeltaAngle(rootYaw, yaw);

        // Bloqueamos esa diferencia para que no pase de nuestro límite (-90 a 90)
        float clampedDeltaYaw = Mathf.Clamp(deltaYaw, -maxYawAngle, maxYawAngle);

        // Le devolvemos el valor bloqueado al yaw real
        yaw = rootYaw + clampedDeltaYaw;

        Vector3 targetRotation = new Vector3(pitch, yaw);
        transform.eulerAngles = targetRotation;

        // 2. Seguimos al Interpolation Target (suave), si por error no hay, caemos al root (con tirones)
        Transform targetToFollow = interpolationTarget != null ? interpolationTarget : targetNetworkObject.transform;

        transform.position = targetToFollow.position - transform.forward * distance;
    }
}