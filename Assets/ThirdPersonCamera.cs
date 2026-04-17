using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Configuración")]
    public Transform target; // Arrastra a tu Jugador aquí
    public float distance = 5f;
    public float mouseSensitivity = 2f;
    public Vector2 pitchMinMax = new Vector2(-40, 85); // Límites para mirar arriba/abajo

    private float yaw;
    private float pitch;

    void Start()
    {
        // Bloquear y ocultar el cursor del ratón en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Obtener el input del ratón
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Limitar la rotación vertical para no dar vueltas de campana
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        // 2. Calcular la nueva rotación y posición
        Vector3 targetRotation = new Vector3(pitch, yaw);
        transform.eulerAngles = targetRotation;

        // Colocar la cámara a la distancia correcta detrás del jugador
        transform.position = target.position - transform.forward * distance;
    }
}