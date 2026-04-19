using UnityEngine;
using Fusion;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Físicas")]
    public float moveForce = 20f;
    public float maxSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Detección Suelo")]
    public LayerMask groundLayer;

    private Rigidbody rb;
    [Networked] private NetworkBool isGrounded { get; set; }

    // NUEVO: Guardamos el estado anterior de los botones para saber cuándo se acaban de pulsar
    [Networked] private NetworkButtons previousButtons { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            // 1. RAYCAST CORREGIDO
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            // 0.5f (bajar al centro) + 1.0f (bajar a los pies) + 0.2f (margen para tocar el piso)
            float totalCheckDistance = 1.7f;

            isGrounded = Physics.Raycast(rayOrigin, Vector3.down, totalCheckDistance, groundLayer);

            Debug.DrawRay(rayOrigin, Vector3.down * totalCheckDistance, isGrounded ? Color.green : Color.red);

            // 2. LECTURA DE BOTONES AL ESTILO FUSION (Equivalente a GetKeyDown)
            NetworkButtons pressedButtons = data.buttons.GetPressed(previousButtons);
            previousButtons = data.buttons;

            if (pressedButtons.IsSet(MyButtons.Jump) && isGrounded)
            {
                Debug.Log($"¡Salto! ¿En el suelo?: {isGrounded}");
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }

            // 3. Rotación
            Vector3 flatCameraForward = data.cameraForward;
            flatCameraForward.y = 0f;
            if (flatCameraForward != Vector3.zero)
            {
                flatCameraForward.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(flatCameraForward);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Runner.DeltaTime * 15f);
            }

            // 4. Movimiento Horizontal
            Vector3 flatCameraRight = data.cameraRight;
            flatCameraRight.y = 0f;
            flatCameraRight.Normalize();
            Vector3 moveDir = (flatCameraForward * data.vertical + flatCameraRight * data.horizontal).normalized;
            rb.AddForce(moveDir * moveForce, ForceMode.Acceleration);

            // 5. Limitar velocidad máxima
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVelocity.magnitude > maxSpeed)
            {
                Vector3 limitedVelocity = flatVelocity.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(limitedVelocity.x, rb.linearVelocity.y, limitedVelocity.z);
            }
        }
    }
}