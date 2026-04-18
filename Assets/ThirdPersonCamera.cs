using UnityEngine;
using Fusion;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Configuración")]
    public NetworkObject targetNetworkObject; // Referencia al objeto de red del jugador
    public float distance = 5f;
    public float mouseSensitivity = 2f;
    public Vector2 pitchMinMax = new Vector2(-40, 85);

    private float yaw;
    private float pitch;

    void Start()
    {
        // Solo bloqueamos el cursor si este es nuestro jugador
        if (targetNetworkObject != null && targetNetworkObject.HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Si no es nuestro jugador, apagamos la cámara y este script
            gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (targetNetworkObject == null || !targetNetworkObject.HasInputAuthority) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        Vector3 targetRotation = new Vector3(pitch, yaw);
        transform.eulerAngles = targetRotation;

        transform.position = targetNetworkObject.transform.position - transform.forward * distance;
    }
}