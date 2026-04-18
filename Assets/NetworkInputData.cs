using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public float horizontal;
    public float vertical;
    public Vector3 cameraForward;
    public Vector3 cameraRight;
}