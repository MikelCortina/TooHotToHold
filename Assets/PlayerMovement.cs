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

    [Header("Animación / Rigging")]
    public Transform torsoBone;
    // NUEVO: Referencia al Animator
    public Animator animator;

    [Tooltip("Ángulo máximo hacia cada lado. 90 significa 180 grados de rango total.")]
    public float maxTorsoAngle = 90f;

    private Rigidbody rb;
    [Networked] private NetworkBool isGrounded { get; set; }
    [Networked] private NetworkButtons previousButtons { get; set; }

    private Quaternion targetTorsoRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    public override void Spawned()
    {
        GameObject potObject = GameObject.FindGameObjectWithTag("Bandeja");

        if (potObject != null)
        {
            sharedPot = potObject.transform;
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

            // 3. ROTACIÓN DEL CUERPO
            if (sharedPot != null)
            {
                Vector3 directionToPot = sharedPot.position - transform.position;
                directionToPot.y = 0f;

                if (directionToPot != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPot.normalized);
                    rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Runner.DeltaTime * 15f);
                }
            }
            else
            {
                Vector3 flatCameraForwardBody = data.cameraForward;
                flatCameraForwardBody.y = 0f;
                if (flatCameraForwardBody != Vector3.zero)
                {
                    flatCameraForwardBody.Normalize();
                    Quaternion targetRotation = Quaternion.LookRotation(flatCameraForwardBody);
                    rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Runner.DeltaTime * 15f);
                }
            }

            // 3.5. CÁLCULO DE LA ROTACIÓN DEL TORSO
            if (torsoBone != null)
            {
                Vector3 bodyForwardFlat = transform.forward;
                bodyForwardFlat.y = 0f;
                bodyForwardFlat.Normalize();

                Vector3 cameraForwardFlat = data.cameraForward;
                cameraForwardFlat.y = 0f;
                cameraForwardFlat.Normalize();

                if (cameraForwardFlat != Vector3.zero && bodyForwardFlat != Vector3.zero)
                {
                    float angle = Vector3.SignedAngle(bodyForwardFlat, cameraForwardFlat, Vector3.up);
                    float clampedAngle = Mathf.Clamp(angle, -maxTorsoAngle, maxTorsoAngle);
                    Vector3 finalForwardFlat = Quaternion.Euler(0, clampedAngle, 0) * bodyForwardFlat;
                    Vector3 finalForward = new Vector3(finalForwardFlat.x, data.cameraForward.y, finalForwardFlat.z);
                    targetTorsoRotation = Quaternion.LookRotation(finalForward);
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

            // NUEVO -> 6. MANDAR VALORES AL ANIMATOR PARA LAS PIERNAS
            if (animator != null)
            {
                // Pasamos directamente el input (WASD / Joystick) al Blend Tree.
                // Como el cuerpo del Rigidbody siempre mira hacia la cámara/olla, 
                // data.vertical (W/S) siempre será caminar de frente o hacia atrás,
                // y data.horizontal (A/D) siempre será hacer "strafe" lateral.
                animator.SetFloat("VelX", data.horizontal);
                animator.SetFloat("VelZ", data.vertical);
            }
        }
    }

    private void LateUpdate()
    {
        if (torsoBone != null && targetTorsoRotation != default(Quaternion))
        {
            torsoBone.rotation = Quaternion.Slerp(torsoBone.rotation, targetTorsoRotation, Time.deltaTime * 15f);
        }
    }
}