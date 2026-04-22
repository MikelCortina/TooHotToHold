using Fusion;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Físicas")]
    public float moveForce = 20f;
    public float maxSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Detección Suelo")]
    public LayerMask groundLayer;

    // Ya no es pública desde el inspector, se asigna sola
    [HideInInspector] public Transform sharedPot;

    [Header("Animación / Rigging")]
    public Transform torsoBone;
    public Animator animator;

    [Tooltip("Ángulo máximo hacia cada lado. 90 significa 180 grados de rango total.")]
    public float maxTorsoAngle = 90f;

    private Rigidbody rb;
    [Networked] private NetworkBool isGrounded { get; set; }
    [Networked] private NetworkButtons previousButtons { get; set; }

    // --- VARIABLES DE RED (LA SOLUCIÓN AL PROBLEMA) ---
    [Networked] public Quaternion TargetTorsoRotation { get; set; }
    [Networked] public NetworkBool IsHoldingPot { get; set; }
    [Networked] public int PotPlayerIndex { get; set; } // 1 para el P1, 2 para el P2
    // --------------------------------------------------

    private Quaternion smoothedTorsoRotation;

    public Transform torsoTarget;

    [Header("Animación / Rigging - Manos")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    public TwoBoneIKConstraint leftArmIK;
    public TwoBoneIKConstraint rightArmIK;

    [HideInInspector] public Transform potGripLeft;
    [HideInInspector] public Transform potGripRight;

    private float currentIKWeight = 0f;
    public float ikTransitionSpeed = 5f;

    [Header("Límites de Movimiento")]
    public float maxArmReach = 1.3f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (torsoBone != null)
        {
            smoothedTorsoRotation = torsoBone.rotation;
        }
    }

    // --- NUEVA FUNCIÓN PARA AUTO-ASIGNAR LA OLLA ---
    private void CheckAndAssignPot()
    {
        if (IsHoldingPot && sharedPot == null)
        {
            // Si la red dice que tenemos la olla pero aún no la hemos asignado visualmente, la buscamos
            ObjectPhysicsController pot = FindObjectOfType<ObjectPhysicsController>();
            if (pot != null)
            {
                sharedPot = pot.transform;
                if (PotPlayerIndex == 1)
                {
                    potGripLeft = pot.p1_GripLeft;
                    potGripRight = pot.p1_GripRight;
                }
                else if (PotPlayerIndex == 2)
                {
                    potGripLeft = pot.p2_GripLeft;
                    potGripRight = pot.p2_GripRight;
                }
            }
        }
        else if (!IsHoldingPot && sharedPot != null)
        {
            // Si la soltamos, limpiamos todo
            sharedPot = null;
            potGripLeft = null;
            potGripRight = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            // Verificamos si tenemos la olla antes de hacer cálculos de físicas
            CheckAndAssignPot();

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            float totalCheckDistance = 1.7f;
            isGrounded = Physics.Raycast(rayOrigin, Vector3.down, totalCheckDistance, groundLayer);

            NetworkButtons pressedButtons = data.buttons.GetPressed(previousButtons);
            previousButtons = data.buttons;

            if (pressedButtons.IsSet(MyButtons.Jump) && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }

            // ROTACIÓN DEL CUERPO
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

            // ROTACIÓN DEL TORSO
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
                    TargetTorsoRotation = Quaternion.LookRotation(finalForwardFlat);
                }
            }

            Vector3 flatCameraRight = data.cameraRight;
            flatCameraRight.y = 0f;
            flatCameraRight.Normalize();

            Vector3 flatCameraForwardDir = data.cameraForward;
            flatCameraForwardDir.y = 0f;
            flatCameraForwardDir.Normalize();

            Vector3 moveDir = (flatCameraForwardDir * data.vertical + flatCameraRight * data.horizontal).normalized;

            // CORREA FÍSICA
            if (sharedPot != null)
            {
                Vector3 playerPosFlat = new Vector3(transform.position.x, 0, transform.position.z);
                Vector3 potPosFlat = new Vector3(sharedPot.position.x, 0, sharedPot.position.z);

                float distanceToPot = Vector3.Distance(playerPosFlat, potPosFlat);

                if (distanceToPot >= maxArmReach)
                {
                    Vector3 dirToPot = (potPosFlat - playerPosFlat).normalized;

                    if (Vector3.Dot(moveDir, dirToPot) < 0)
                    {
                        moveDir = Vector3.ProjectOnPlane(moveDir, dirToPot);

                        Vector3 currentVel = rb.linearVelocity;
                        Vector3 flatVel = new Vector3(currentVel.x, 0, currentVel.z);
                        if (Vector3.Dot(flatVel, dirToPot) < 0)
                        {
                            Vector3 correctedVel = Vector3.ProjectOnPlane(flatVel, dirToPot);
                            rb.linearVelocity = new Vector3(correctedVel.x, currentVel.y, correctedVel.z);
                        }
                    }
                }
            }

            rb.AddForce(moveDir * moveForce, ForceMode.Acceleration);

            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVelocity.magnitude > maxSpeed)
            {
                Vector3 limitedVelocity = flatVelocity.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(limitedVelocity.x, rb.linearVelocity.y, limitedVelocity.z);
            }

            if (animator != null)
            {
                animator.SetFloat("VelX", data.horizontal);
                animator.SetFloat("VelZ", data.vertical);
            }
        }
    }

    private void LateUpdate()
    {
        // El Jugador 2 pasará por aquí y encontrará la olla visualmente si la red dice que IsHoldingPot es true
        CheckAndAssignPot();

        // 1. LÓGICA DEL TORSO
        if (torsoBone != null && TargetTorsoRotation != default(Quaternion))
        {
            smoothedTorsoRotation = Quaternion.Slerp(smoothedTorsoRotation, TargetTorsoRotation, Time.deltaTime * 15f);
            torsoTarget.rotation = smoothedTorsoRotation;
        }

        // 2. LÓGICA DE LAS MANOS IK
        float targetWeight = IsHoldingPot ? 1f : 0f;
        currentIKWeight = Mathf.Lerp(currentIKWeight, targetWeight, Time.deltaTime * ikTransitionSpeed);

        if (leftArmIK != null) leftArmIK.weight = currentIKWeight;
        if (rightArmIK != null) rightArmIK.weight = currentIKWeight;

        if (currentIKWeight > 0.01f && potGripLeft != null && potGripRight != null)
        {
            leftHandTarget.position = potGripLeft.position;
            leftHandTarget.rotation = potGripLeft.rotation;

            rightHandTarget.position = potGripRight.position;
            rightHandTarget.rotation = potGripRight.rotation;
        }
    }
}