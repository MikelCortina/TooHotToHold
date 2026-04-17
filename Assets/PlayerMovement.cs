using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Inputs")]
    public string horizontalAxis = "Horizontal1";
    public string verticalAxis = "Vertical1";

    [Header("Físicas")]
    public float moveForce = 20f;
    public float maxSpeed = 5f;

    [Header("Referencias")]
    public Transform cameraTransform; // Arrastra tu Main Camera aquí en el Inspector

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Evitar que el jugador se caiga como un dominó por las físicas
        rb.freezeRotation = true;
    }

    void FixedUpdate()
    {
        // 1. Rotar el jugador con la cámara
        if (cameraTransform != null)
        {
            // Obtener hacia dónde mira la cámara, ignorando la inclinación (eje Y)
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            // Rotar el Rigidbody hacia esa dirección
            if (cameraForward != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
                rb.MoveRotation(targetRotation);
            }
        }

        // 2. Obtener input
        float h = Input.GetAxis(horizontalAxis);
        float v = Input.GetAxis(verticalAxis);

        // 3. Calcular dirección de movimiento basada en la cámara
        Vector3 moveDir = (transform.forward * v + transform.right * h).normalized;

        // Añadir fuerza al jugador
        rb.AddForce(moveDir * moveForce, ForceMode.Acceleration);

        // 4. Limitar la velocidad máxima (solo en plano horizontal para no afectar la gravedad)
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVelocity.magnitude > maxSpeed)
        {
            Vector3 limitedVelocity = flatVelocity.normalized * maxSpeed;
            // Aplicar la velocidad limitada, manteniendo la velocidad de caída (Y) intacta
            rb.linearVelocity = new Vector3(limitedVelocity.x, rb.linearVelocity.y, limitedVelocity.z);
        }
    }
}