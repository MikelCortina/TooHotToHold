using UnityEngine;
using Fusion;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Configuración")]
    public NetworkObject targetNetworkObject;

    [Tooltip("Arrastra aquí el modelo 3D HIJO del jugador, el mismo que pusiste en Interpolation Target")]
    public Transform interpolationTarget; // <--- ¡LA CLAVE ESTÁ AQUÍ!

    public float distance = 5f;
    public float mouseSensitivity = 2f;
    public Vector2 pitchMinMax = new Vector2(-40, 85);

    private float yaw;
    private float pitch;

    void Start()
    {
        if (targetNetworkObject != null && targetNetworkObject.HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Usar LateUpdate es correcto, se ejecuta después de que Fusion actualiza los visuales.
    void LateUpdate()
    {
        if (targetNetworkObject == null || !targetNetworkObject.HasInputAuthority) return;

        // 1. Recogemos el input visual
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        Vector3 targetRotation = new Vector3(pitch, yaw);
        transform.eulerAngles = targetRotation;

        // 2. Seguimos al Interpolation Target (suave), si por error no hay, caemos al root (con tirones)
        Transform targetToFollow = interpolationTarget != null ? interpolationTarget : targetNetworkObject.transform;

        transform.position = targetToFollow.position - transform.forward * distance;
    }
}