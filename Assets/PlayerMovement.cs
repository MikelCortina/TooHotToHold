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

    [Header("Cooperativo")]
    public Transform sharedPot;

    // NUEVO: Referencia al objeto que rotará con la cámara
    [Header("Visuales")]
    public Transform objectToRotateWithCamera;

    private Rigidbody rb;
    [Networked] private NetworkBool isGrounded { get; set; }

    [Networked] private NetworkButtons previousButtons { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    public override void Spawned()
    {
        // Buscamos la bandeja automáticamente al hacer spawn
        GameObject potObject = GameObject.FindGameObjectWithTag("Bandeja");

        if (potObject != null)
        {
            sharedPot = potObject.transform;
            Debug.Log("¡Olla encontrada y asignada automáticamente!");
        }
        else
        {
            Debug.LogWarning("OJO: No se encontró la olla. ¿Te olvidaste de ponerle el Tag 'Olla'?");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            // 1. RAYCAST
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            float totalCheckDistance = 1.7f;
            isGrounded = Physics.Raycast(rayOrigin, Vector3.down, totalCheckDistance, groundLayer);

            // 2. LECTURA DE BOTONES
            NetworkButtons pressedButtons = data.buttons.GetPressed(previousButtons);
            previousButtons = data.buttons;

            if (pressedButtons.IsSet(MyButtons.Jump) && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }

            // 3. ROTACIÓN DEL CUERPO (Hacia la olla)
            if (sharedPot != null)
            {
                Vector3 directionToPot = sharedPot.position - transform.position;
                directionToPot.y = 0f; // Rotación plana

                if (directionToPot != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPot.normalized);
                    rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Runner.DeltaTime * 15f);
                }
            }
            else
            {
                // Fallback: Si no hay olla, el cuerpo rota con la cámara
                Vector3 flatCameraForwardBody = data.cameraForward;
                flatCameraForwardBody.y = 0f;
                if (flatCameraForwardBody != Vector3.zero)
                {
                    flatCameraForwardBody.Normalize();
                    Quaternion targetRotation = Quaternion.LookRotation(flatCameraForwardBody);
                    rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Runner.DeltaTime * 15f);
                }
            }

            // NUEVO -> 3.5. ROTACIÓN DEL OBJETO EXTRA (Hacia la cámara)
            if (objectToRotateWithCamera != null)
            {
                Vector3 flatCameraForwardObj = data.cameraForward;

                // IMPORTANTE: Si quieres que el objeto también mire hacia arriba/abajo (ej: una linterna), 
                // comenta o borra la siguiente línea. Si solo quieres que gire de izquierda a derecha, déjala.
                flatCameraForwardObj.y = 0f;

                if (flatCameraForwardObj != Vector3.zero)
                {
                    flatCameraForwardObj.Normalize();
                    Quaternion targetObjRotation = Quaternion.LookRotation(flatCameraForwardObj);

                    // Usamos .rotation en lugar de .localRotation para que ignore la rotación del cuerpo padre
                    objectToRotateWithCamera.rotation = Quaternion.Slerp(objectToRotateWithCamera.rotation, targetObjRotation, Runner.DeltaTime * 15f);
                }
            }

            // 4. MOVIMIENTO HORIZONTAL
            Vector3 flatCameraRight = data.cameraRight;
            flatCameraRight.y = 0f;
            flatCameraRight.Normalize();

            Vector3 flatCameraForwardDir = data.cameraForward;
            flatCameraForwardDir.y = 0f;
            flatCameraForwardDir.Normalize();

            Vector3 moveDir = (flatCameraForwardDir * data.vertical + flatCameraRight * data.horizontal).normalized;
            rb.AddForce(moveDir * moveForce, ForceMode.Acceleration);

            // 5. LIMITAR VELOCIDAD
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVelocity.magnitude > maxSpeed)
            {
                Vector3 limitedVelocity = flatVelocity.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(limitedVelocity.x, rb.linearVelocity.y, limitedVelocity.z);
            }
        }
    }
}