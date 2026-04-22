using Fusion;
using UnityEngine;

public class ObjectPhysicsController : NetworkBehaviour
{
    [Header("Manos de los Jugadores")]
    public Transform p1_HandLeft;
    public Transform p1_HandRight;
    public Transform p2_HandLeft;
    public Transform p2_HandRight;

    [Header("Grips (Agarres) de la Olla")]
    [Tooltip("Agarres específicos para el Jugador 1")]
    public Transform p1_GripLeft;
    public Transform p1_GripRight;

    [Tooltip("Agarres específicos para el Jugador 2")]
    public Transform p2_GripLeft;
    public Transform p2_GripRight;

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
        // 1. Buscamos a los jugadores
        PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();

        // DEBUG: Ver cuántos jugadores encuentra en este frame
        Debug.Log($"[OLLA] Buscando jugadores... Encontrados actualmente: {players.Length}");

        if (players.Length >= 2)
        {
            Debug.Log("[OLLA] ¡2 Jugadores encontrados! Intentando ordenarlos por su InputAuthority...");

            try
            {
                // Ordenamos por ID para saber quién es P1 y P2
                System.Array.Sort(players, (a, b) => a.Object.InputAuthority.RawEncoded.CompareTo(b.Object.InputAuthority.RawEncoded));
                Debug.Log($"[OLLA] Ordenamiento exitoso. P1 ID: {players[0].Object.InputAuthority.RawEncoded} | P2 ID: {players[1].Object.InputAuthority.RawEncoded}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[OLLA] Error al ordenar. Es posible que los jugadores aún no tengan InputAuthority asignada por Fusion. Reintentando en el próximo tick... Error: {e.Message}");
                return; // Salimos y volvemos a intentar en el siguiente tick
            }

            PlayerMovement p1Movement = players[0];
            PlayerMovement p2Movement = players[1];

            // 2. Buscamos los PlayerHands. Usamos GetComponent si dices que están en el mismo objeto
            PlayerHands p1Hands = p1Movement.GetComponent<PlayerHands>();
            PlayerHands p2Hands = p2Movement.GetComponent<PlayerHands>();

            // Si por algún motivo están en hijos, usamos GetComponentInChildren como respaldo
            if (p1Hands == null) p1Hands = p1Movement.GetComponentInChildren<PlayerHands>();
            if (p2Hands == null) p2Hands = p2Movement.GetComponentInChildren<PlayerHands>();

            // DEBUG: Comprobar si encontró las manos
            if (p1Hands == null) Debug.LogError("[OLLA] ERROR CRÍTICO: No se encontró el script 'PlayerHands' en el Jugador 1.");
            if (p2Hands == null) Debug.LogError("[OLLA] ERROR CRÍTICO: No se encontró el script 'PlayerHands' en el Jugador 2.");

            if (p1Hands != null && p2Hands != null)
            {
                Debug.Log("[OLLA] Scripts PlayerHands encontrados en ambos. Asignando referencias...");

                // Asignamos a la olla (Físicas)
                p1_HandLeft = p1Hands.manoIzquierda;
                p1_HandRight = p1Hands.manoDerecha;
                p2_HandLeft = p2Hands.manoIzquierda;
                p2_HandRight = p2Hands.manoDerecha;

                // DEBUG: Comprobar que los 4 grips de la olla no estén nulos antes de pasarlos
                if (p1_GripLeft == null || p1_GripRight == null || p2_GripLeft == null || p2_GripRight == null)
                {
                    Debug.LogWarning("[OLLA] ¡OJO! Faltan grips por asignar en el Inspector del ObjectPhysicsController.");
                }

                // Asignamos al P1 (IK) - Ahora usa sus propios grips
                p1Movement.sharedPot = this.transform;
                p1Movement.potGripLeft = this.p1_GripLeft;
                p1Movement.potGripRight = this.p1_GripRight;

                // Asignamos al P2 (IK) - Ahora usa sus propios grips (sin cruzar variables)
                p2Movement.sharedPot = this.transform;
                p2Movement.potGripLeft = this.p2_GripLeft;
                p2Movement.potGripRight = this.p2_GripRight;

                Debug.Log("<color=green>[OLLA] ¡TODAS LAS REFERENCIAS ASIGNADAS CORRECTAMENTE!</color>");
            }
        }
        else
        {
            Debug.Log("[OLLA] Esperando a que el segundo jugador haga spawn en la red para hacer las asignaciones...");
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