using Fusion;
using UnityEngine;
using static Unity.Collections.Unicode;

public class ObjectPhysicsController : NetworkBehaviour // Cambiado a NetworkBehaviour
{
    [Header("Manos de los Jugadores")]
    // IMPORTANTE: En multijugador, no puedes asignar esto en el inspector fácilmente porque 
    // los jugadores "spawnean" en tiempo real. Tendrás que asignar estas variables 
    // mediante código cuando los jugadores se conecten.
    public Transform p1_HandLeft;
    public Transform p1_HandRight;
    public Transform p2_HandLeft;
    public Transform p2_HandRight;

    [Header("Configuración")]
    public float minDistance = 0.5f;
    public float maxDistance = 3.5f;
    public float followSpeed = 8f;
    public float dropDistance = 4.5f;
    public float maxRotationAngle = 180f;

    [Header("Ajuste de Ejes")]
    public bool invertX = false;
    public bool invertZ = false;
    public bool swapAxes = true;

    // [Networked] asegura que todos los clientes sepan si el objeto se ha caído
    [Networked] private NetworkBool HasDropped { get; set; }

    private Rigidbody rb;
    private Vector3 prevP1Center;
    private Vector3 prevP2Center;
    private float currentPullDirection = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // ¡CLAVE! Mientras lo sostienen, las físicas de Unity no deben interferir
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    // Nueva función para que el GameManager le pase los jugadores cuando estén listos
    public void AsignarJugadores(PlayerHands p1, PlayerHands p2)
    {
        p1_HandLeft = p1.manoIzquierda;
        p1_HandRight = p1.manoDerecha;

        p2_HandLeft = p2.manoIzquierda;
        p2_HandRight = p2.manoDerecha;
    }

    public override void FixedUpdateNetwork() // Usamos el update de Fusion
    {
        if (HasDropped) return;

        // --- 1. SISTEMA DE BÚSQUEDA AUTOMÁTICA ---
        // Si no tenemos manos asignadas, las buscamos.
        if (p1_HandLeft == null || p2_HandLeft == null)
        {
            // Buscamos a todos los jugadores en la escena que tengan el script PlayerHands
            PlayerHands[] players = FindObjectsOfType<PlayerHands>();

            // Si ya hay 2 jugadores conectados...
            if (players.Length >= 2)
            {
                // Ordenamos por su ID de red para asegurar que el Player 1 sea el mismo
                // en la pantalla de ambos usuarios (normalmente el Host es el ID más bajo).
                bool isPlayer0First = players[0].Object.InputAuthority.RawEncoded < players[1].Object.InputAuthority.RawEncoded;

                PlayerHands p1 = isPlayer0First ? players[0] : players[1];
                PlayerHands p2 = isPlayer0First ? players[1] : players[0];

                // ¡Asignamos las manos automáticamente!
                p1_HandLeft = p1.manoIzquierda;
                p1_HandRight = p1.manoDerecha;

                p2_HandLeft = p2.manoIzquierda;
                p2_HandRight = p2.manoDerecha;
            }
            else
            {
                // Si aún no han entrado los dos jugadores, nos salimos del update y esperamos.
                return;
            }
        }

        Vector3 p1Center = (p1_HandLeft.position + p1_HandRight.position) / 2f;
        Vector3 p2Center = (p2_HandLeft.position + p2_HandRight.position) / 2f;

        if (prevP1Center == Vector3.zero)
        {
            prevP1Center = p1Center;
            prevP2Center = p2Center;
        }

        float currentDistance = Vector3.Distance(p1Center, p2Center);

        // Solo el servidor/Host tiene autoridad para decidir soltar el objeto
        if (HasStateAuthority && currentDistance >= dropDistance)
        {
            SoltarObjeto();
            return;
        }

        Vector3 idealCenter = (p1Center + p2Center) / 2f;
        transform.position = Vector3.Lerp(transform.position, idealCenter, Runner.DeltaTime * followSpeed);

        // --- ZONA CORREGIDA ---
        Vector3 axis = (p2Center - p1Center).normalized;
        float pullP1 = Vector3.Dot(p1Center - prevP1Center, -axis);
        float pullP2 = Vector3.Dot(p2Center - prevP2Center, axis);

        if (pullP1 > 0.001f || pullP2 > 0.001f)
        {
            float totalMovement = Mathf.Abs(pullP1) + Mathf.Abs(pullP2);
            if (totalMovement > 0.0001f)
            {
                float targetDirection = (pullP2 - pullP1) / totalMovement;
                targetDirection = Mathf.Clamp(targetDirection, -1f, 1f);
                currentPullDirection = Mathf.Lerp(currentPullDirection, targetDirection, Runner.DeltaTime * 10f);
            }
        }

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

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Runner.DeltaTime * 10f);
        }

        prevP1Center = p1Center;
        prevP2Center = p2Center;
    }

    private void SoltarObjeto()
    {
        HasDropped = true;

        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        transform.parent = null;
    }
}