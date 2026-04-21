using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Configuración")]
    public NetworkPrefabRef playerPrefab; // El prefab de tu jugador

    // --- NUEVAS VARIABLES PARA LOS SPAWN POINTS ---
    [Header("Puntos de Aparición (Spawn Points)")]
    public Transform spawnPointP1;
    public Transform spawnPointP2;

    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    async void Start()
    {
        // Esto le da el control a Fusion sobre el PhysX de Unity
        gameObject.AddComponent<RunnerSimulatePhysics3D>();
        await StartGame();
    }

    async System.Threading.Tasks.Task StartGame()
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // --- ESTA ES LA LÍNEA MÁGICA PARA LAS FÍSICAS ---
        // Obligamos a Fusion a controlar el motor de físicas para la predicción
        gameObject.AddComponent<RunnerSimulatePhysics3D>();

        _runner.AddCallbacks(this);

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "SalaCooperativa",
            Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    // --- AQUÍ ASIGNAMOS EL SPAWN POINT CORRECTO ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Transform spawnPointAsignado;

            // Si no hay nadie en el diccionario, es el primer jugador (P1)
            if (_spawnedCharacters.Count == 0)
            {
                spawnPointAsignado = spawnPointP1;
            }
            // Si ya hay alguien, es el segundo jugador (P2)
            else
            {
                spawnPointAsignado = spawnPointP2;
            }

            // Obtenemos la posición y rotación del Spawn Point (si existe, si no, lo mandamos al centro 0,0,0)
            Vector3 spawnPos = spawnPointAsignado != null ? spawnPointAsignado.position : Vector3.zero;
            Quaternion spawnRot = spawnPointAsignado != null ? spawnPointAsignado.rotation : Quaternion.identity;

            // Instanciamos al jugador en esa posición y rotación exactas
            NetworkObject networkPlayerObject = runner.Spawn(playerPrefab, spawnPos, spawnRot, player);

            _spawnedCharacters.Add(player, networkPlayerObject);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    // Dentro de GameManager.cs
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        data.horizontal = Input.GetAxisRaw("Horizontal");
        data.vertical = Input.GetAxisRaw("Vertical");

        // NUEVO: Guardamos el estado del botón de salto
        data.buttons.Set(MyButtons.Jump, Input.GetKey(KeyCode.Space));

        if (Camera.main != null)
        {
            data.cameraForward = Camera.main.transform.forward;
            data.cameraRight = Camera.main.transform.right;
        }
        else
        {
            data.cameraForward = Vector3.forward;
            data.cameraRight = Vector3.right;
        }

        input.Set(data);
    }

    // El resto de métodos obligatorios vacíos
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason info) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}