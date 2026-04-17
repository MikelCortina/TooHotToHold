using UnityEngine;

public class DualArmManager : MonoBehaviour
{
    [Header("Jugadores")]
    public Transform player1;
    public Transform player2;

    [Header("Objeto Compartido")]
    public Rigidbody sharedObjectRb;
    public Transform agarreIzquierdo; // Arrastra el objeto hijo izquierdo aquí
    public Transform agarreDerecho;   // Arrastra el objeto hijo derecho aquí

    [Header("Visuales (Cuerdas/Brazos)")]
    public LineRenderer brazo1Renderer;
    public LineRenderer brazo2Renderer;

    [Header("Físicas (Resortes)")]
    public SpringJoint jointP1; // El resorte en el Jugador 1
    public SpringJoint jointP2; // El resorte en el Jugador 2

    [Header("Configuración")]
    public float maxArmStretch = 6f; // Cuánto se puede estirar UN solo brazo
    private bool isConnected = true;

    void Start()
    {
        brazo1Renderer.positionCount = 2;
        brazo2Renderer.positionCount = 2;
    }

    void Update()
    {
        if (!isConnected) return;

        // 1. Dibujar el brazo del Jugador 1 hacia el lado izquierdo del objeto
        brazo1Renderer.SetPosition(0, player1.position);
        brazo1Renderer.SetPosition(1, agarreIzquierdo.position);

        // 2. Dibujar el brazo del Jugador 2 hacia el lado derecho del objeto
        brazo2Renderer.SetPosition(0, player2.position);
        brazo2Renderer.SetPosition(1, agarreDerecho.position);

        // 3. Comprobar si ALGUNO de los dos brazos se ha estirado demasiado
        float distBrazo1 = Vector3.Distance(player1.position, agarreIzquierdo.position);
        float distBrazo2 = Vector3.Distance(player2.position, agarreDerecho.position);

        if (distBrazo1 > maxArmStretch || distBrazo2 > maxArmStretch)
        {
            SoltarObjeto();
        }
    }

    void SoltarObjeto()
    {
        isConnected = false;

        // Ocultar los brazos visuales
        brazo1Renderer.enabled = false;
        brazo2Renderer.enabled = false;

        // Romper los resortes para que el objeto caiga libremente
        if (jointP1 != null) Destroy(jointP1);
        if (jointP2 != null) Destroy(jointP2);

        Debug.Log("¡Se estiraron demasiado y soltaron el objeto!");
    }
}