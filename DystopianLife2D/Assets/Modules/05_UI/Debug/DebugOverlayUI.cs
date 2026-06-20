using Sirenix.OdinInspector;
using System.Collections;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class DebugOverlayUI : MonoBehaviour
{
    #region UI References
    [BoxGroup("Required"), Required, SerializeField]
    private TextMeshProUGUI _debugText;
    #endregion

    #region Toggles (Settings)
    [BoxGroup("Settings"), LabelText("Частота обновления (сек)")]
    [SerializeField] private float _updateInterval = 0.2f;

    [BoxGroup("Local Player Info")]
    [SerializeField] private bool _showPlayerName = true;
    [BoxGroup("Local Player Info")]
    [SerializeField] private bool _showPosition = true;
    [BoxGroup("Local Player Info")]
    [SerializeField] private bool _showSpeed = true;
    [BoxGroup("Local Player Info")]
    [SerializeField] private bool _showVehicleStatus = true;

    [BoxGroup("Global Info")]
    [SerializeField] private bool _showFPS = true;
    [BoxGroup("Global Info")]
    [SerializeField] private bool _showPlayerCount = true;
    [BoxGroup("Global Info")]
    [SerializeField] private bool _showBotCount = true;
    #endregion

    #region Internal State
    private StringBuilder _sb = new StringBuilder();
    private float _timer;
    private int _cachedBotCount;
    private int _cachedPlayerCount;
    private float _deltaTime = 0.0f;
    #endregion

    private void Start()
    {
        if (_debugText == null)
        {
            DebugUtils.LogMissingReference(this, nameof(_debugText));
            enabled = false;
        }
    }

    private void Update()
    {
        // Расчет FPS каждый кадр
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;

        // Обновление текста по таймеру
        _timer += Time.deltaTime;
        if (_timer >= _updateInterval)
        {
            _timer = 0f;
            UpdateDebugInfo();
        }
    }

    private void UpdateDebugInfo()
    {
        _sb.Clear();

        // --- FPS ---
        if (_showFPS)
        {
            float msec = _deltaTime * 1000.0f;
            float fps = 1.0f / _deltaTime;
            _sb.AppendLine($"FPS: {fps:0.} ({msec:0.0} ms)");
        }

        // --- GLOBAL STATS ---
        if (NetworkManager.Singleton != null && (_showPlayerCount || _showBotCount))
        {
            // Считаем ботов и игроков. Операция FindObjectsByType тяжелая, поэтому делаем это только раз в _updateInterval
            if (_showBotCount || _showPlayerCount)
            {
                CalculateNetworkStats();
            }

            if (_showPlayerCount)
                _sb.AppendLine($"Players Online: {_cachedPlayerCount}");

            if (_showBotCount)
                _sb.AppendLine($"Active Bots: {_cachedBotCount}");
        }

        _sb.AppendLine("----------------");

        // --- LOCAL PLAYER STATS ---
        Pawn player = Pawn.LocalPlayer; // Берем из вашего Pawn.cs

        if (player != null)
        {
            if (_showPlayerName)
            {
                // Используем string.Format для надежности или интерполяцию
                // Проверяем, что имя вообще есть
                string pName = string.IsNullOrEmpty(player.name) ? "Unknown Pawn" : player.name;
                _sb.AppendLine($"Name: <color=#00FFFF>{pName}</color>");
            }

            if (_showPosition)
                _sb.AppendLine($"Pos: {player.transform.position.ToString("F1")}");

            if (_showSpeed)
            {
                // Если в машине - берем скорость машины, если нет - скорость Pawn
                float speed = 0f;
                if (player.IsInVehicle && player.CurrentSeat != null && player.CurrentSeat.Vehicle != null)
                {
                    // Для Unity 6 используем linearVelocity, для старых версий velocity
                    speed = player.CurrentSeat.Vehicle.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
                }
                else if (player.Rigidbody != null)
                {
                    speed = player.Rigidbody.linearVelocity.magnitude;
                }

                // Переводим примерно в км/ч (условно * 3.6, если 1 unit = 1 метр)
                _sb.AppendLine($"Speed: {speed * 3.6f:F0} km/h");
            }

            if (_showVehicleStatus)
            {
                if (player.IsInVehicle && player.CurrentSeat != null && player.CurrentSeat.Vehicle != null)
                {
                    string seatType = player.CurrentVehicleRole.ToString();
                    // Название машины (Оранжевый)
                    string vehicleName = player.CurrentSeat.Vehicle.name;

                    _sb.AppendLine($"Status: <color=#00FF00>In Vehicle</color> ({seatType})");
                    _sb.AppendLine($"Car: <color=#FFA500>{vehicleName}</color>"); // Подсветка машины
                }
                else
                {
                    _sb.AppendLine($"Status: On Foot");
                }
            }
        }
        else
        {
            _sb.AppendLine("<color=red>No Local Player</color>");
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            int ping = GetClientPingMs();
           
            if (ping >= 0)
            {
                string pingColor =
                    ping < 80 ? "#00FF00" :
                    ping < 150 ? "#FFFF00" :
                    "#FF5555";

                _sb.AppendLine($"Ping: <color={pingColor}>{ping} ms</color> ({GetPingQuality(ping)})");
            }
        }
        _debugText.text = _sb.ToString();
    }

    private void CalculateNetworkStats()
    {
        // Считаем игроков через NetworkManager
        if (NetworkManager.Singleton.IsServer)
        {
            _cachedPlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        }
        else
        {
            // На клиенте точное число узнать сложнее без кастомного списка, 
            // но можно посчитать объекты Pawn, которыми владеют игроки
            var allPawns = FindObjectsByType<NetworkedPawnOwnership>(FindObjectsSortMode.None);
            _cachedPlayerCount = allPawns.Count(p => p.IsOccupiedByPlayer());
        }

        // Считаем ботов (Pawn, у которых PawnOwnerId == ulong.MaxValue)
        var ownerships = FindObjectsByType<NetworkedPawnOwnership>(FindObjectsSortMode.None);
        _cachedBotCount = 0;

        foreach (var own in ownerships)
        {
            if (!own.IsOccupiedByPlayer()) // Метод из вашего кода 
            {
                _cachedBotCount++;
            }
        }
    }

    private int GetClientPingMs()
    {
        if (NetworkManager.Singleton == null)
            return -1;

        if (!NetworkManager.Singleton.IsClient)
            return -1;

        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null)
            return -1;

        // RTT считаем до сервера, а не до себя
        ulong serverClientId = NetworkManager.ServerClientId;

        ulong rttUlong = transport.GetCurrentRtt(serverClientId);

        if (rttUlong == 0)
            return 0;

        return (int)(rttUlong / 2);
    }

    string GetPingQuality(int ping)
    {
        if (ping < 80) return "Excellent";
        if (ping < 150) return "OK";
        return "Bad";
    }
}