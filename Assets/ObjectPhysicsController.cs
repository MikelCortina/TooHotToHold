using Fusion;
using UnityEngine;

public class ObjectPhysicsController : NetworkBehaviour
{
    [Header("Manos de los Jugadores")]
    public Transform p1_HandLeft;
    public Transform p1_HandRight;
    public Transform p2_HandLeft;
    public Transform p2_HandRight;

    [Header("Configuración")]
    public float followSpeed = 8f;
    public float maxRotationAngle = 180f;
    public float dropDistance = 4.5f;
    public float minDistance = 0.5f;
    public float maxDistance = 3.5f;

    [Header("Ajuste de Ejes")]
    public bool invertX = false;
    public bool invertZ = false;
    public bool swapAxes = true;

    [Networked] private NetworkBool HasDropped { get; set; }

    private Rigidbody rb;
    private Vector3 prevP1Center;
    private Vector3 prevP2Center;
    private float currentPullDirection = 0f;

    private void Awake()
    {
        // Aseguramos que el Rigidbody exista desde el principio para que Zibra pueda leer la inercia
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Lo mantenemos cinemático mientras los jugadores lo sostienen
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasDropped) return;

        // 1. BUSCAR JUGADORES
        if (p1_HandLeft == null || p2_HandLeft == null)
        {
            FindHands();
            return;
        }

        // 2. SOLO EL HOST CALCULA Y MUEVE EL OBJETO
        if (Object.HasStateAuthority)
        {
            ApplyPhysicsLogic();
        }
    }

    private void FindHands()
    {
        PlayerHands[] players = FindObjectsOfType<PlayerHands>();
        if (players.Length >= 2)
        {
            System.Array.Sort(players, (a, b) => a.Object.InputAuthority.RawEncoded.CompareTo(b.Object.InputAuthority.RawEncoded));

            p1_HandLeft = players[0].manoIzquierda;
            p1_HandRight = players[0].manoDerecha;
            p2_HandLeft = players[1].manoIzquierda;
            p2_HandRight = players[1].manoDerecha;
        }
    }

    private void ApplyPhysicsLogic()
    {
        Vector3 p1Center = (p1_HandLeft.position + p1_HandRight.position) / 2f;
        Vector3 p2Center = (p2_HandLeft.position + p2_HandRight.position) / 2f;

        if (prevP1Center == Vector3.zero) { prevP1Center = p1Center; prevP2Center = p2Center; }

        float currentDistance = Vector3.Distance(p1Center, p2Center);

        // Condición de soltar
        if (currentDistance >= dropDistance)
        {
            RPC_SoltarObjeto();
            return;
        }

        // Movimiento de posición usando Rigidbody (Crucial para la inercia de fluidos)
        Vector3 idealCenter = (p1Center + p2Center) / 2f;
        Vector3 newPos = Vector3.Lerp(rb.position, idealCenter, Runner.DeltaTime * followSpeed);
        rb.MovePosition(newPos);

        // Lógica de Rotación
        Vector3 axis = (p2Center - p1Center).normalized;
        float pullP1 = Vector3.Dot(p1Center - prevP1Center, -axis);
        float pullP2 = Vector3.Dot(p2Center - prevP2Center, axis);

        if (pullP1 > 0.001f || pullP2 > 0.001f)
        {
            float totalMovement = Mathf.Abs(pullP1) + Mathf.Abs(pullP2);
            float targetDirection = (pullP2 - pullP1) / totalMovement;
            currentPullDirection = Mathf.Lerp(currentPullDirection, Mathf.Clamp(targetDirection, -1f, 1f), Runner.DeltaTime * 10f);
        }

        float stretchRatio = Mathf.Clamp01((currentDistance - minDistance) / (maxDistance - minDistance));
        float pitchAngle = stretchRatio * maxRotationAngle * currentPullDirection;

        // El lado izquierdo desde la perspectiva del P1 es el derecho del P2
        float distLadoIzquierdo = Vector3.Distance(p1_HandLeft.position, p2_HandRight.position);

        // El lado derecho desde la perspectiva del P1 es el izquierdo del P2
        float distLadoDerecho = Vector3.Distance(p1_HandRight.position, p2_HandLeft.position);

        // Calculamos el ángulo basándonos en los lados paralelos de la bandeja
        float rollAngle = Mathf.Clamp(((distLadoIzquierdo - distLadoDerecho) / (maxDistance - minDistance)) * maxRotationAngle, -maxRotationAngle, maxRotationAngle);

        if (invertX) pitchAngle *= -1f;
        if (invertZ) rollAngle *= -1f;

        Vector3 forwardDir = p2Center - p1Center;
        forwardDir.y = 0;

        if (forwardDir != Vector3.zero)
        {
            Quaternion baseRotation = Quaternion.LookRotation(forwardDir);
            float ejeX = swapAxes ? rollAngle : pitchAngle;
            float ejeZ = swapAxes ? pitchAngle : rollAngle;
            Quaternion targetRotation = baseRotation * Quaternion.Euler(ejeX, 0, ejeZ);

            // Rotación usando Rigidbody
            Quaternion newRot = Quaternion.Slerp(rb.rotation, targetRotation, Runner.DeltaTime * 10f);
            rb.MoveRotation(newRot);
        }

        prevP1Center = p1Center;
        prevP2Center = p2Center;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SoltarObjeto()
    {
        HasDropped = true;

        // Liberamos el Rigidbody para que caiga con físicas reales
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 1f; // Ajusta según el "peso" visual de la olla
        }
    }
}