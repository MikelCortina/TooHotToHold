using UnityEngine;
using Fusion;

public class PlayerHands : NetworkBehaviour
{
    [Header("Asigna esto en el Prefab de tu jugador")]
    public Transform manoIzquierda;
    public Transform manoDerecha;
}