using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PossessionManager : MonoBehaviour
{
    public static PossessionManager Instance { get; private set; }

    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required, SerializeField]
    private CameraFollow _cameraFollow;
    [BoxGroup("Required"), Required, SerializeField]
    private InteractionSystem _interactionSystem;
    #endregion

    #region Поля: Settings

    #endregion

    #region Поля: Debug
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private List<Pawn> _registeredPawns = new();
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private int _currentIndex = 0;
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;
    #endregion

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private IEnumerator WaitForNetworkManagerAndSubscribe()
    {
        while (NetworkManager.Singleton == null)
        {
            yield return null;
        }
        StartCoroutine(WaitAndInitialize());
    }

    private void Start()
    {
        // Вызывается из OnlineTestUI после старта хоста/клиента
    }

    public void StartWithDelay()
    {
        StartCoroutine(WaitForNetworkManagerAndSubscribe());
    }


    public void OnExitSeat(Pawn pawn, bool isPlayer)
    {
        ApplyPawnState(pawn, isPlayer);
    }

    /*
    private IEnumerator WaitAndInitialize()
    {
        yield return new WaitForSeconds(3.5f);
        InitializeState();
    }
    */

    private IEnumerator WaitAndInitialize()
    {
        // 1. Ждем появления NetworkManager
        while (NetworkManager.Singleton == null) yield return null;

        // 2. Ждем подключения
        float connectionTimeout = 30f;
        float connTimer = 0;
        while (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsConnectedClient)
        {
            connTimer += Time.deltaTime;
            if (connTimer > connectionTimeout)
            {
                Debug.LogError("[PossessionManager] Не удалось дождаться подключения к сети.");
                OnlineTestUI.Instance.ReconnectClient();
                
                yield break;
            }
            yield return null;
        }

        // Если мы клиент, даем сети "продышаться"
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            yield return new WaitForSeconds(0.5f);

        // 3. Пытаемся синхронизировать павнов
        float syncTimeout = 3.0f; // Тайм-аут на поиск свободного павна
        float syncTimer = 0;
        bool success = false;

        while (syncTimer < syncTimeout)
        {
            _registeredPawns.RemoveAll(p => p == null);
            if (_registeredPawns.Count == 0)
            {
                var found = Object.FindObjectsByType<Pawn>(FindObjectsSortMode.None);
                foreach (var p in found) RegisterPawn(p);
            }

            // Проверяем: заспавнились ли они и есть ли среди них свободные
            bool anySpawned = _registeredPawns.Any(p => p.GetComponent<NetworkObject>().IsSpawned);
            int freeIndex = FindFirstFreePawn();

            if (anySpawned && freeIndex != -1)
            {
                success = true;
                break;
            }

            syncTimer += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        if (!success)
        {
            // КРИТИЧЕСКИЙ МОМЕНТ: Если за 3 секунды мы не нашли павнов или они "заняты" (глюк синхронизации)
            if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[PossessionManager] Павны не найдены или не синхронизированы. ПЕРЕПОДКЛЮЧЕНИЕ...");
                OnlineTestUI.Instance.ReconnectClient();
                yield break; // Выходим из этой корутины, новая запустится при переподключении
            }
        }

        InitializeState();
    }


    private void InitializeState()
    {
        _registeredPawns.RemoveAll(p => p == null);
        if (_registeredPawns.Count == 0) return;
        _registeredPawns = _registeredPawns.OrderBy(p => p.name).ToList();

        if (_registeredPawns.Count == 0)
        {
            Debug.LogError("[PossessionManager] Нет свободных Pawn для управления! КРИТИЧЕСКАЯ ОШИБКА. ЛИСТ ПУСТ");
            return;
        }

        int freePawnIndex = FindFirstFreePawn();

        if (freePawnIndex == -1)
        {
            Debug.LogError("[PossessionManager] Нет свободных Pawn для управления! Все заняты игроками!");
            _currentIndex = 0;
            ApplyPawnState(_registeredPawns[_currentIndex], false);
        }
        else
        {
            _currentIndex = freePawnIndex;

            var ownership = _registeredPawns[_currentIndex].GetComponent<NetworkedPawnOwnership>();
            if (ownership != null)
            {
                ownership.RequestOwnership();
            }

            ApplyPawnState(_registeredPawns[_currentIndex], true);
        }

        _cameraFollow.SetTarget(_registeredPawns[_currentIndex].transform);
        _cameraFollow.SetMode(CameraFollow.CameraMode.Walking);

        ColoredDebug.CLog(gameObject,
            $"<color=lime>[PossessionManager]</color> Инициализация: Pawn {_registeredPawns[_currentIndex].name}",
            _ColoredDebug);


    }

    private int FindFirstFreePawn()
    {
        for (int i = 0; i < _registeredPawns.Count; i++)
        {
            var pawn = _registeredPawns[i];
            if (pawn == null) continue;

            var netObj = pawn.GetComponent<NetworkObject>();

            // Если объект еще не в сети, мы вообще не можем его проверять
            if (netObj == null || !netObj.IsSpawned) continue;

            var ownership = pawn.GetComponent<NetworkedPawnOwnership>();

            // Если мы здесь, значит IsSpawned == true и данные о владельце синхронизированы
            if (!ownership.IsOccupiedByPlayer())
            {
                return i;
            }
        }
        return -1;
    }

    public void RegisterPawn(Pawn pawn)
    {
        if (!_registeredPawns.Contains(pawn)) _registeredPawns.Add(pawn);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z)) SwitchPawn(1);
        if (Input.GetKeyDown(KeyCode.X)) SwitchPawn(-1);

        //ЭТО НУЖНО В БУДУЩЕМ ОБЯЗАТЕЛЬНО ПЕРЕНЕСТИ В СИСТЕМУ СИДЕНИЙ
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (Pawn.LocalPlayer.CurrentSeat != null)
            {
                Pawn.LocalPlayer.CurrentSeat.ExitVehicle();
            }
        }

    }

    private void SwitchPawn(int step)
    {
        if (_registeredPawns.Count <= 1) return;

        var oldOwnership = _registeredPawns[_currentIndex].GetComponent<NetworkedPawnOwnership>();
        if (oldOwnership != null) oldOwnership.ReleaseOwnership();

        ApplyPawnState(_registeredPawns[_currentIndex], false);

        int attempts = 0;
        int nextIndex = _currentIndex;
        bool foundFree = false;

        do
        {
            nextIndex = (nextIndex + step + _registeredPawns.Count) % _registeredPawns.Count;
            attempts++;

            var ownership = _registeredPawns[nextIndex].GetComponent<NetworkedPawnOwnership>();
            if (ownership != null && !ownership.IsOccupiedByPlayer())
            {
                foundFree = true;
                break;
            }

            if (attempts >= _registeredPawns.Count)
            {
                ColoredDebug.CLog(gameObject,
                    "<color=yellow>[PossessionManager]</color> Все Pawn заняты! Остаюсь на текущем.",
                    _ColoredDebug);

                if (oldOwnership != null) oldOwnership.RequestOwnership();
                ApplyPawnState(_registeredPawns[_currentIndex], true);
                return;
            }
        }
        while (true);

        if (foundFree)
        {
            _currentIndex = nextIndex;

            var newOwnership = _registeredPawns[_currentIndex].GetComponent<NetworkedPawnOwnership>();
            if (newOwnership != null)
            {
                newOwnership.RequestOwnership();
            }

            ApplyPawnState(_registeredPawns[_currentIndex], true);
            _cameraFollow.SetTarget(_registeredPawns[_currentIndex].transform);

            ColoredDebug.CLog(gameObject,
                $"<color=cyan>[PossessionManager]</color> Переключение на: {_registeredPawns[_currentIndex].name}",
                _ColoredDebug);
        }
    }

    private void ApplyPawnState(Pawn pawn, bool isPlayer)
    {
        if (pawn == null) return;

        // Контроллер игрока включаем только для локального игрока
        pawn.PlayerController.enabled = isPlayer;

        // ВАЖНО: Бот включается ТОЛЬКО если это не игрок И мы находимся на СЕРВЕРЕ
        if (pawn.BotController)
        {
            bool shouldBeBot = !isPlayer && NetworkManager.Singleton.IsServer;
            pawn.BotController.enabled = shouldBeBot;
        }

        // То же самое для компонентов A* (чтобы они не работали на клиенте)
        if (pawn.AIPath) pawn.AIPath.enabled = !isPlayer && NetworkManager.Singleton.IsServer;

        if (isPlayer)
        {
            pawn.SetAsLocalPlayer();
            if (_interactionSystem != null) _interactionSystem.LinkToPawn(pawn);
        }
        else
        {
            pawn.RemoveLocalPlayerStatus();
        }
    }
}