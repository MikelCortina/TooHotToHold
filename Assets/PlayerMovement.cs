using UnityEngine;
using Fusion;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour // Cambiado a NetworkBehaviour
{
    [Header("Físicas")]
    public float moveForce = 20f;
    public float maxSpeed = 5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    // FixedUpdateNetwork reemplaza a FixedUpdate en Fusion
    // FixedUpdateNetwork reemplaza a FixedUpdate en Fusion
    public override void FixedUpdateNetwork()
    {
        // Obtenemos el input que definimos en NetworkInputData
        if (GetInput(out NetworkInputData data))
        {
            // --- INICIO DE LA CORRECCIÓN ---
            // 1. Tomamos la dirección de la cámara pero anulamos el eje Y (inclinación)
            Vector3 flatCameraForward = data.cameraForward;
            flatCameraForward.y = 0f;

            if (flatCameraForward != Vector3.zero)
            {
                flatCameraForward.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(flatCameraForward);

                // Usamos Slerp para suavizar la rotación y evitar que un micro-tirón de la cámara
                // provoque un giro de 180 grados instantáneo.
                Quaternion smoothedRotation = Quaternion.Slerp(rb.rotation, targetRotation, Runner.DeltaTime * 15f);
                rb.MoveRotation(smoothedRotation);
            }
            // --- FIN DE LA CORRECCIÓN ---

            // 2. Calcular dirección de movimiento (también usando el vector aplanado es mejor)
            // Aplanamos también la derecha para evitar que el jugador intente "volar" o hundirse
            Vector3 flatCameraRight = data.cameraRight;
            flatCameraRight.y = 0f;
            flatCameraRight.Normalize();

            Vector3 moveDir = (flatCameraForward * data.vertical + flatCameraRight * data.horizontal).normalized;

            // 3. Añadir fuerza al jugador
            rb.AddForce(moveDir * moveForce, ForceMode.Acceleration);

            // 4. Limitar la velocidad máxima
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVelocity.magnitude > maxSpeed)
            {
                Vector3 limitedVelocity = flatVelocity.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(limitedVelocity.x, rb.linearVelocity.y, limitedVelocity.z);
            }
        }
    }
}