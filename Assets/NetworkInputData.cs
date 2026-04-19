using Fusion;
using UnityEngine;

// NUEVO: Enum para identificar nuestras acciones
public enum MyButtons
{
    Jump = 0,
}

public struct NetworkInputData : INetworkInput
{
    public float horizontal;
    public float vertical;
    public Vector3 cameraForward;
    public Vector3 cameraRight;

    // REEMPLAZO: Usamos NetworkButtons en lugar de un bool suelto
    public NetworkButtons buttons;
}