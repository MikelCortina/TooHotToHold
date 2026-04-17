using UnityEngine;

public class ObjectPhysicsController : MonoBehaviour
{
    [Header("Manos de los Jugadores")]
    public Transform p1_HandLeft;
    public Transform p1_HandRight;
    public Transform p2_HandLeft;
    public Transform p2_HandRight;

    [Header("Configuración de Distancia y Movimiento")]
    public float minDistance = 0.5f;
    public float maxDistance = 3.5f;
    public float followSpeed = 8f;

    [Header("Configuración de Caída")]
    public float dropDistance = 4.5f;
    private bool hasDropped = false;
    private Rigidbody rb;

    [Header("Configuración de Inclinación")]
    public float maxRotationAngle = 180f;

    [Header("Ajuste de Ejes")]
    public bool invertX = false;
    public bool invertZ = false;
    public bool swapAxes = true;

    private Vector3 prevP1Center;
    private Vector3 prevP2Center;
    private float currentPullDirection = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (hasDropped) return;

        if (p1_HandLeft == null || p2_HandLeft == null) return;

        Vector3 p1Center = (p1_HandLeft.position + p1_HandRight.position) / 2f;
        Vector3 p2Center = (p2_HandLeft.position + p2_HandRight.position) / 2f;

        if (prevP1Center == Vector3.zero)
        {
            prevP1Center = p1Center;
            prevP2Center = p2Center;
        }

        float currentDistance = Vector3.Distance(p1Center, p2Center);

        if (currentDistance >= dropDistance)
        {
            SoltarObjeto();
            return;
        }

        Vector3 idealCenter = (p1Center + p2Center) / 2f;
        transform.position = Vector3.Lerp(transform.position, idealCenter, Time.deltaTime * followSpeed);

        // --- INICIO DE LA ZONA CORREGIDA ---
        Vector3 axis = (p2Center - p1Center).normalized;
        float pullP1 = Vector3.Dot(p1Center - prevP1Center, -axis);
        float pullP2 = Vector3.Dot(p2Center - prevP2Center, axis);

        if (pullP1 > 0.001f || pullP2 > 0.001f)
        {
            // 1. Usamos Mathf.Abs para sumar la cantidad real de movimiento sin importar la dirección
            float totalMovement = Mathf.Abs(pullP1) + Mathf.Abs(pullP2);

            if (totalMovement > 0.0001f) // 2. Prevenimos divisiones por cero
            {
                float targetDirection = (pullP2 - pullP1) / totalMovement;

                // 3. VITAL: Forzamos a que el resultado nunca pase de -1 (100% P1) o 1 (100% P2)
                targetDirection = Mathf.Clamp(targetDirection, -1f, 1f);

                currentPullDirection = Mathf.Lerp(currentPullDirection, targetDirection, Time.deltaTime * 10f);
            }
        }
        // --- FIN DE LA ZONA CORREGIDA ---

        float stretchRatio = Mathf.Clamp01((currentDistance - minDistance) / (maxDistance - minDistance));
        float pitchAngle = stretchRatio * maxRotationAngle * currentPullDirection;

        float distLeft = Vector3.Distance(p1_HandLeft.position, p2_HandLeft.position);
        float distRight = Vector3.Distance(p1_HandRight.position, p2_HandRight.position);
        float rollDiff = (distLeft - distRight) / (maxDistance - minDistance);

        float rollAngle = Mathf.Clamp(rollDiff * maxRotationAngle, -maxRotationAngle, maxRotationAngle);

        if (invertX) pitchAngle *= -1f;
        if (invertZ) rollAngle *= -1f;

        Vector3 forwardDir = p2Center - p1Center;
        forwardDir.y = 0;

        if (forwardDir != Vector3.zero)
        {
            Quaternion baseRotation = Quaternion.LookRotation(forwardDir);

            float ejeX = swapAxes ? rollAngle : pitchAngle;
            float ejeZ = swapAxes ? pitchAngle : rollAngle;

            Quaternion tiltRotation = Quaternion.Euler(ejeX, 0, ejeZ);
            Quaternion targetRotation = baseRotation * tiltRotation;

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        prevP1Center = p1Center;
        prevP2Center = p2Center;
    }

    private void SoltarObjeto()
    {
        hasDropped = true;

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = false;
        rb.useGravity = true;

        transform.parent = null;
    }
}