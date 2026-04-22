using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerIKController : MonoBehaviour
{
    [Header("Referencias de IK")]
    public TwoBoneIKConstraint leftHandIK;
    public TwoBoneIKConstraint rightHandIK;

    [Header("Targets del Rigging (Hijos del Rig)")]
    public Transform leftIKTarget;
    public Transform rightIKTarget;

    [Header("Ajustes")]
    public float transitionSpeed = 5f;

    // Variables internas
    private Transform currentLeftSocket;
    private Transform currentRightSocket;
    private bool isHoldingPot = false;
    private float currentIKWeight = 0f;

    // Esta función la llamarás cuando el jugador decida agarrar la olla
    // Le pasas los Sockets (Transforms vacíos) que has creado en la olla
    public void GrabPot(Transform potLeftSocket, Transform potRightSocket)
    {
        currentLeftSocket = potLeftSocket;
        currentRightSocket = potRightSocket;
        isHoldingPot = true;
    }

    // Esta función la llamas cuando suelte la olla (por distancia o por botón)
    public void DropPot()
    {
        currentLeftSocket = null;
        currentRightSocket = null;
        isHoldingPot = false;
    }

    // Usamos LateUpdate porque los ajustes de huesos (Rigging) se hacen después 
    // de que el Animator procese la animación de caminar/correr.
    private void LateUpdate()
    {
        // 1. Calcular el peso objetivo (1 si agarra, 0 si suelta)
        float targetWeight = isHoldingPot ? 1f : 0f;

        // 2. Transición suave del peso
        currentIKWeight = Mathf.Lerp(currentIKWeight, targetWeight, Time.deltaTime * transitionSpeed);
        leftHandIK.weight = currentIKWeight;
        rightHandIK.weight = currentIKWeight;

        // 3. Si estamos agarrando (o en transición), mover nuestros targets a los sockets de la olla
        if (currentIKWeight > 0.01f && currentLeftSocket != null && currentRightSocket != null)
        {
            // Copiamos la posición y rotación exacta del socket de la bandeja
            leftIKTarget.position = currentLeftSocket.position;
            leftIKTarget.rotation = currentLeftSocket.rotation;

            rightIKTarget.position = currentRightSocket.position;
            rightIKTarget.rotation = currentRightSocket.rotation;
        }
    }
}