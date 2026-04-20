using Fusion;
using UnityEngine;

public class BalancingObjectController : NetworkBehaviour
{
    [Header("Evolución de Aceleración")]
    [Tooltip("Aceleración inicial cuando el objeto empieza a balancearse.")]
    public float minAcceleration = 4f;

    [Tooltip("Aceleración máxima que alcanzará, haciéndolo muy difícil de controlar.")]
    public float maxAcceleration = 15f;

    [Tooltip("Tiempo en segundos que tarda en pasar de la aceleración mínima a la máxima.")]
    public float timeToMaxAcceleration = 45f;

    [Header("Configuración del Deslizamiento")]
    [Tooltip("Fricción (0 a 1). 1 es hielo puro, valores menores frenan el objeto gradualmente.")]
    public float friction = 0.98f;

    [Tooltip("Velocidad máxima absoluta que puede alcanzar el objeto patinando.")]
    public float maxSpeed = 6f;

    [Header("Condición de Caída")]
    [Tooltip("Distancia desde el centro de la olla a la que el objeto se cae por el borde.")]
    public float fallDistance = 0.8f;

    // Variables de red
    [Networked] private Vector2 CurrentLocalVelocity { get; set; }
    [Networked] private NetworkBool HasFallen { get; set; }
    [Networked] private float CurrentAccelerationTime { get; set; } // Nuestro temporizador sincronizado

    private Rigidbody rb;
    private Transform parentPot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public override void Spawned()
    {
        parentPot = transform.parent;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasFallen || parentPot == null) return;

        if (Object.HasStateAuthority)
        {
            ApplySlidingLogic();
        }
    }

    private void ApplySlidingLogic()
    {
        // 1. ACTUALIZAR LA DIFICULTAD (El temporizador)
        // Aumentamos el tiempo activo, pero sin pasarnos del tiempo máximo definido
        CurrentAccelerationTime = Mathf.Clamp(CurrentAccelerationTime + Runner.DeltaTime, 0f, timeToMaxAcceleration);

        // Calculamos la aceleración actual basada en el tiempo transcurrido
        // Mathf.Lerp mezcla entre el mínimo y el máximo según el porcentaje de tiempo completado
        float currentAccelerationLimit = Mathf.Lerp(minAcceleration, maxAcceleration, CurrentAccelerationTime / timeToMaxAcceleration);

        // 2. CALCULAR LA INCLINACIÓN
        Vector3 localGravity = parentPot.InverseTransformDirection(Vector3.down);

        // Usamos la aceleración calculada dinámicamente
        Vector2 acceleration = new Vector2(localGravity.x, localGravity.z) * currentAccelerationLimit;

        // 3. ACTUALIZAR VELOCIDAD
        Vector2 velocity = CurrentLocalVelocity;
        velocity += acceleration * Runner.DeltaTime;
        velocity *= friction;
        velocity = Vector2.ClampMagnitude(velocity, maxSpeed);

        CurrentLocalVelocity = velocity;

        // 4. MOVER EL OBJETO
        Vector3 currentLocalPos = transform.localPosition;
        currentLocalPos.x += CurrentLocalVelocity.x * Runner.DeltaTime;
        currentLocalPos.z += CurrentLocalVelocity.y * Runner.DeltaTime;

        transform.localPosition = currentLocalPos;

        // 5. COMPROBAR SI SE CAE
        float distanceFromCenter = new Vector2(currentLocalPos.x, currentLocalPos.z).magnitude;

        if (distanceFromCenter >= fallDistance)
        {
            RPC_DropObject();
        }
    }

    // --- NUEVO MÉTODO PARA REINICIAR LA DIFICULTAD ---
    // Lo hacemos como un RPC para que cualquier jugador o script (ej. si atrapan el objeto) pueda llamar a reiniciarlo
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ResetAcceleration()
    {
        CurrentAccelerationTime = 0f;
        // Opcional: También podrías querer frenar el objeto al reiniciar
        // CurrentLocalVelocity = Vector2.zero; 
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DropObject()
    {
        HasFallen = true;
        transform.SetParent(null);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 worldVelocity = parentPot.TransformDirection(new Vector3(CurrentLocalVelocity.x, 0, CurrentLocalVelocity.y));
            rb.linearVelocity = worldVelocity;
        }
    }
}