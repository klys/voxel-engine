using UnityEngine;
using SocketIOClient;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Collections.Concurrent;
public class NetClient : MonoBehaviour
{
    private SocketIOClient.SocketIO client = null;

    [SerializeField] private GameObject Player;
    [SerializeField] private GameObject PlayerOnline;
    public bool Connected = false;

    private Vector3 PlayerPos_Cache;
    private float PlayerAngle_Cache;

    public int playerId = -1;

    private readonly ConcurrentQueue<Action> mainThreadActions = new();


    public class PlayerData
    {
        public GameObject gameObject; // Reference to Unity object
        public int playerId;
        public Vector3 Position;
        public float Angle;
    }

    ConcurrentDictionary<int, PlayerData> playerPositions = new();
    void Start()
    {
        Debug.Log("Starting NetClient.");
        client = new SocketIOClient.SocketIO("http://127.0.0.1:3000/", new SocketIOOptions
        {
            // Optional: Configure options like auto-reconnection, query parameters, etc.
            Reconnection = true,
            ReconnectionAttempts = 5,
            ReconnectionDelay = 1000, // milliseconds
            Query = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("token", "myAuthToken123")
                }
        });

        client.OnConnected += (sender, e) =>
        {
            Debug.Log("Connected to Socket.IO server!");
            Connected = true;

        };

        client.OnDisconnected += (sender, e) =>
        {
            Debug.Log($"Disconnected from Socket.IO server. Reason: {e}");
        };

        client.OnError += (sender, e) =>
        {
            Debug.LogError($"Socket.IO Error: {e}");
        };

        // game packets

        client.On("player-identification", async (response) =>
        {
            playerId = response.GetValue<int>();
            Debug.Log($"Player identified as {playerId}");

            await client.EmitAsync("player-start", PlayerController.SerializeTransform(playerId, Player.transform.position, Player.transform.eulerAngles.y));
        });

        client.On("player-start", (response) =>
        {
            byte[] data = response.GetValue<byte[]>();
            Vector3 position;
            float angle;
            int _playerId;

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                _playerId = reader.ReadInt32();
                position.x = reader.ReadSingle();
                position.y = reader.ReadSingle();
                position.z = reader.ReadSingle();
                angle = reader.ReadSingle();
            }

            Debug.Log($"Starting Player: PlayerID: {_playerId}, Position: {position}, Angle: {angle}");
            if (_playerId != playerId)
            {
                Debug.Log($"Player {_playerId} connected.");
                //Quaternion rot = Quaternion.Euler(0, angle, 0);

                //AddPlayer(_playerId, CreatePlayer(_playerId, position), position, angle);
                 RunOnMainThread(() =>
                {
                    Debug.Log($"[MainThread] Creating player {_playerId}");
                    AddPlayer(_playerId, CreatePlayer(_playerId, position), position, angle);
                });
            }
        });

        client.On("player-move", (response) =>
        {
            byte[] data = response.GetValue<byte[]>();
            Vector3 position;
            float angle;
            int _playerId;

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                _playerId = reader.ReadInt32();
                position.x = reader.ReadSingle();
                position.y = reader.ReadSingle();
                position.z = reader.ReadSingle();
                angle = reader.ReadSingle();
            }

            Debug.Log($"PlayerID: {_playerId}, Position: {position}, Angle: {angle}");
            if (_playerId != playerId)
            {
                Debug.Log($"Player {_playerId} is moving.");

                RunOnMainThread(() =>
                {
                    UpdatePlayerState(_playerId, position, angle);
                });

                
            }
        });

        _ = ConnectToServer();
    }

    private async Task ConnectToServer()
    {
        Debug.Log("Attempting to connect to Socket.IO server...");
        await client.ConnectAsync();

    }

    // You might want to disconnect when the MonoBehaviour is destroyed
    void OnDestroy()
    {
        if (client != null && client.Connected)
        {
            _ = client.DisconnectAsync(); // Disconnect asynchronously
        }
    }

    void Update()
    {

        if (Connected == true)
        {
            if ((PlayerPos_Cache != Player.transform.position) || !Mathf.Approximately(PlayerAngle_Cache, Player.transform.eulerAngles.y))
            {
                Debug.Log($" Player {playerId} moving and sending his data");
                client.EmitAsync("player-move", PlayerController.SerializeTransform(playerId, Player.transform.position, Player.transform.eulerAngles.y));
                PlayerPos_Cache = Player.transform.position;
                PlayerAngle_Cache = Player.transform.eulerAngles.y;
                Debug.Log($" x: {PlayerPos_Cache.x}");
            }
        }

         while (mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    void AddPlayer(int playerId, GameObject go, Vector3 pos, float angle)
    {
        var data = new PlayerData
        {
            gameObject = go,
            Position = go.transform.position,
            Angle = go.transform.eulerAngles.y
        };

        playerPositions.TryAdd(playerId, data);
    }

    void UpdatePlayerState(int playerId, Vector3 pos, float angle)
    {
        if (playerPositions.TryGetValue(playerId, out var data))
        {
            data.Position = pos;
            data.Angle = angle;
            data.gameObject.transform.position = pos;
            data.gameObject.transform.rotation = Quaternion.Euler(0, data.Angle, 0);
        }
    }

    public GameObject CreatePlayer(int _playerId, Vector3 startPosition)
    {
        Debug.Log("CreatePlayer: Head");
        if (PlayerOnline == null)
        {
            Debug.LogError("PlayerOnline prefab not assigned!");
            return null;
        }
        if (!playerPositions.ContainsKey(_playerId))
        {
            Debug.Log($"CreatePlayer: after if check {startPosition}");
            GameObject go = Instantiate(PlayerOnline, startPosition, Quaternion.identity);
            go.name = $"Player_{_playerId}";
            Debug.Log($"[CreatePlayer] Instantiated {go.name} at {go.transform.position}, active: {go.activeSelf}");


            var data = new PlayerData
            {
                playerId = _playerId,
                gameObject = go,
                Position = startPosition,
                Angle = 0f
            };

            playerPositions.TryAdd(_playerId, data);
            return go;
        }
        return null;
    }
    
    private void RunOnMainThread(Action action)
    {
        mainThreadActions.Enqueue(action);
    }

}
