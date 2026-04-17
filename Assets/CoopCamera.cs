using UnityEngine;

public class CoopCamera : MonoBehaviour
{
    public Transform player1;
    public Transform player2;

    public float minZoom = 5f;   // Nivel de zoom cuando están juntos
    public float maxZoom = 12f;  // Nivel de zoom al máximo estiramiento
    public float zoomFactor = 1.5f; // Ajusta qué tan rápido hace zoom

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        // 1. Calcular el punto central y mover la cámara
        Vector3 centerPoint = (player1.position + player2.position) / 2f;
        // Asume una cámara con vista superior/isométrica. Mantén la altura 'Y' o 'Z' original de tu cámara.
        transform.position = new Vector3(centerPoint.x, transform.position.y, centerPoint.z);

        // 2. Calcular la distancia para hacer el Zoom
        float distance = Vector3.Distance(player1.position, player2.position);

        // Ajustar el tamaño (asumiendo cámara ortográfica para juegos estilo 2D/isométrico)
        // Si usas perspectiva, tendrías que modificar el cam.fieldOfView o la posición Y/Z.
        float targetZoom = Mathf.Lerp(minZoom, maxZoom, distance / zoomFactor);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 5f);
    }
}