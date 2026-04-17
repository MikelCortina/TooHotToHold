using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    public string horizontalAxis = "Horizontal1"; // Cambiar a "Horizontal2" para el J2
    public string verticalAxis = "Vertical1";     // Cambiar a "Vertical2" para el J2

    public float moveForce = 20f;
    public float maxSpeed = 5f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Obtener input
        float h = Input.GetAxis(horizontalAxis);
        float v = Input.GetAxis(verticalAxis);

        // A�adir fuerza al jugador
        Vector3 movement = new Vector3(h, 0, v).normalized;
        rb.AddForce(movement * moveForce, ForceMode.Acceleration);

        // Limitar la velocidad m�xima para que no salgan volando
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }
}