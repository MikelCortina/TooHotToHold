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
    public Transform p1_GripLeft;
    public Transform p1_GripRight;
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
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasDropped) return;

        // SOLO EL HOST (StateAuthority) EJECUTA ESTA LÓGICA
        if (Object.HasStateAuthority)
        {
            // 1. BUSCAR JUGADORES
            if (p1_HandLeft == null || p2_HandLeft == null)
            {
                FindHands();
                return; // Volvemos a intentar en el próximo tick si no los encontró
            }

            // 2. APLICAR FÍSICAS DE LA OLLA
            ApplyPhysicsLogic();
        }
    }

    private void FindHands()
    {
        PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();

        if (players.Length >= 2)
        {
            try
            {
                System.Array.Sort(players, (a, b) => a.Object.InputAuthority.RawEncoded.CompareTo(b.Object.InputAuthority.RawEncoded));
            }
            catch
            {
                return; // Si el InputAuthority aún no se asienta en el servidor, sale y lo reintenta.
            }

            PlayerMovement p1Movement = players[0];
            PlayerMovement p2Movement = players[1];

            PlayerHands p1Hands = p1Movement.GetComponent<PlayerHands>();
            PlayerHands p2Hands = p2Movement.GetComponent<PlayerHands>();

            if (p1Hands == null) p1Hands = p1Movement.GetComponentInChildren<PlayerHands>();
            if (p2Hands == null) p2Hands = p2Movement.GetComponentInChildren<PlayerHands>();

            if (p1Hands != null && p2Hands != null)
            {
                // Asignamos las manos al host para poder calcular físicas de la olla
                p1_HandLeft = p1Hands.manoIzquierda;
                p1_HandRight = p1Hands.manoDerecha;
                p2_HandLeft = p2Hands.manoIzquierda;
                p2_HandRight = p2Hands.manoDerecha;

                // --- MAGIA DE RED ---
                // Aquí el Host le dice a las variables sincronizadas de los jugadores que agarren la olla.
                p1Movement.IsHoldingPot = true;
                p1Movement.PotPlayerIndex = 1;

                p2Movement.IsHoldingPot = true;
                p2Movement.PotPlayerIndex = 2;
                // --------------------
            }
        }
    }

    private void ApplyPhysicsLogic()
    {
        Vector3 p1Center = (p1_HandLeft.position + p1_HandRight.position) / 2f;
        Vector3 p2Center = (p2_HandLeft.position + p2_HandRight.position) / 2f;

        if (prevP1Center == Vector3.zero) { prevP1Center = p1Center; prevP2Center = p2Center; }

        float currentDistance = Vector3.Distance(p1Center, p2Center);

        if (currentDistance >= dropDistance)
        {
            RPC_SoltarObjeto();
            return;
        }

        Vector3 idealCenter = (p1Center + p2Center) / 2f;
        Vector3 newPos = Vector3.Lerp(rb.position, idealCenter, Runner.DeltaTime * followSpeed);
        rb.MovePosition(newPos);

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

        float distLadoIzquierdo = Vector3.Distance(p1_HandLeft.position, p2_HandRight.position);
        float distLadoDerecho = Vector3.Distance(p1_HandRight.position, p2_HandLeft.position);

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

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 1f;
        }

        // Cuando la olla cae, el Host avisa a las variables de red de los jugadores que se suelten
        if (Object.HasStateAuthority)
        {
            PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();
            foreach (var p in players)
            {
                p.IsHoldingPot = false;
            }
        }
    }
}