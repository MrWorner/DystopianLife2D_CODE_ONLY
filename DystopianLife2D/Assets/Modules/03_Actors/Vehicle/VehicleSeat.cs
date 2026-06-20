using Sirenix.OdinInspector;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class VehicleSeat : InteractableBase, IEnterable
{
    public enum SeatType { Driver, Passenger }

    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required, SerializeField]
    private Vehicle _vehicle;

    [PropertyOrder(-1), BoxGroup("Required"), Required, SerializeField]
    private Transform _interactionPoint;

    [PropertyOrder(-1), BoxGroup("Required"), Required, SerializeField]
    private Transform _seatPoint;

    [PropertyOrder(-1), BoxGroup("Required"), Required, SerializeField]
    private GameObject _iconObject;

    [PropertyOrder(-1), BoxGroup("Required/OPTIONAL"), Required, SerializeField]
    private GameObject _visualDoorOpened;

    #endregion

    #region Поля: SETTINGS
    [BoxGroup("SETTINGS"), SerializeField]
    private SeatType _type = SeatType.Passenger;

    [BoxGroup("SETTINGS"), SerializeField] private float _entryTime = 0.33f;

    [BoxGroup("SETTINGS/Interaction"), SerializeField]
    private Sprite _enterIcon;

    [BoxGroup("SETTINGS/Interaction"), SerializeField]
    private string _interactionHint = "Сесть в машину [E]";

    [BoxGroup("SETTINGS/Interaction"), SerializeField]
    private int _interactionPriority = 10;

    [BoxGroup("SETTINGS/Interaction"), SerializeField]
    private float _interactionRadius = 1.5f;
    #endregion

    #region Поля: Cargo System
    [BoxGroup("SETTINGS/Cargo"), SerializeField]
    private bool _allowCargo = false; // Разрешить размещение груза

    private ICargo _currentCargo;
    #endregion

    #region Поля: DEBUG
    // Синхронизируем occupant через NetworkVariable
    private readonly NetworkVariable<ulong> _occupantNetId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    //[BoxGroup("DEBUG"), ReadOnly, SerializeField]
    private InteractionSystem _interactionSystem;
    //[BoxGroup("DEBUG"), ReadOnly, SerializeField]
    private CameraFollow _cameraFollow;

    [BoxGroup("DEBUG"), SerializeField]
    protected bool _ColoredDebug;
    #endregion

    #region Свойства
    public SeatType Type => _type;
    public Vehicle Vehicle => _vehicle;
    public Transform InteractionPoint => _interactionPoint;
    public Transform SeatPoint => _seatPoint;
    public override Vector3 InteractionPosition => _interactionPoint != null ? _interactionPoint.position : transform.position;
    // Получаем Occupant через NetworkObjectId
    public Pawn Occupant
    {
        get
        {
            if (_occupantNetId.Value == ulong.MaxValue) return null;
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_occupantNetId.Value, out var netObj))
            {
                return netObj.GetComponent<Pawn>();
            }
            return null;
        }
    }

    public bool IsOccupied => _occupantNetId.Value != ulong.MaxValue;

    public override Vector3 Position => transform.position;
    public override Transform Transform => transform;

    public override bool CanInteract
    {
        get
        {
            if (_vehicle == null) return false;

            // --- [FIX НАЧАЛО] ---
            // Если игрок несет груз, но место УЖЕ занято ЭТИМ ЖЕ грузом (сервер обновился раньше, чем прошла анимация),
            // мы не должны блокировать интеракцию.
            var lp = Pawn.LocalPlayer;
            if (lp != null && lp.IsCarryingCargo)
            {
                // Проверяем, совпадает ли ID груза в руках с ID того, кто занял место
                if (lp.CurrentCarriedCargo != null && lp.CurrentCarriedCargo.NetworkObject != null)
                {
                    if (_occupantNetId.Value == lp.CurrentCarriedCargo.NetworkObject.NetworkObjectId)
                    {
                        return true; // Разрешаем взаимодействие (чтобы сразу забрать обратно)
                    }
                }

                // Стандартная проверка: если несем что-то другое, место должно быть свободно
                return _allowCargo && !IsOccupied;
            }
            // --- [FIX КОНЕЦ] ---

            // Если место занято грузом - можно забрать
            if (HasCargo)
            {
                return true;
            }

            // Обычная проверка на свободное место для посадки
            return _vehicle.HasAvailableSeats;
        }
    }

    public override int InteractionPriority => _interactionPriority;
    public bool HasAvailableSeats => CanInteract;
    #endregion

    #region Свойства: Cargo
    public bool AllowCargo => _allowCargo;
    public ICargo CurrentCargo => _currentCargo;
    public bool HasCargo => _currentCargo != null;
    #endregion

    #region Unity Методы
    private void Awake()
    {
        if (_vehicle == null) DebugUtils.LogMissingReference(this, nameof(_vehicle));
        if (_interactionPoint == null) DebugUtils.LogMissingReference(this, nameof(_interactionPoint));
        if (_seatPoint == null) DebugUtils.LogMissingReference(this, nameof(_seatPoint));
        if (_iconObject == null) DebugUtils.LogMissingReference(this, nameof(_iconObject));
    }

    private void Start()
    {
        _interactionSystem = InteractionSystem.Instance;
        _cameraFollow = CameraFollow.Instance;
    }

    #endregion

    #region Реализация IInteractable / IEnterable
    public override Sprite GetInteractionIcon() => _enterIcon;

    public override string GetInteractionHint()
    {
        // Если у локального игрока груз в руках
        if (Pawn.LocalPlayer != null && Pawn.LocalPlayer.IsCarryingCargo)
        {
            // --- [FIX НАЧАЛО] ---
            // Если место занято НАШИМ грузом (анимация идет), сразу показываем "Забрать"
            if (_occupantNetId.Value != ulong.MaxValue &&
                Pawn.LocalPlayer.CurrentCarriedCargo != null &&
                Pawn.LocalPlayer.CurrentCarriedCargo.NetworkObject != null &&
                _occupantNetId.Value == Pawn.LocalPlayer.CurrentCarriedCargo.NetworkObject.NetworkObjectId)
            {
                return $"Забрать {Pawn.LocalPlayer.CurrentCarriedCargo.CargoName}";
            }
            // --- [FIX КОНЕЦ] ---

            if (_allowCargo && !IsOccupied)
            {
                return $"Положить груз в {Type}";
            }
            else if (!_allowCargo)
            {
                return "Это место не для груза";
            }
            else if (IsOccupied)
            {
                return "Место занято";
            }
        }

        // Если место занято грузом
        if (HasCargo)
        {
            return $"Забрать {_currentCargo.CargoName}";
        }

        // Обычная подсказка для посадки
        if (_vehicle == null) return _interactionHint;

        int availableSeats = _vehicle.GetAvailableSeatsCount();
        if (availableSeats > 0)
        {
            return $"{_interactionHint} (мест: {availableSeats})";
        }

        return "Нет свободных мест";
    }

    public override void Interact(Pawn interactor)
    {
        if (!CanInteract)
        {
            ColoredDebug.CLog(gameObject, "<color=red>Interact отклонен: CanInteract == false</color>", _ColoredDebug);
            return;
        }

        // СЛУЧАЙ А: У игрока груз в руках (Кладем в машину)
        if (interactor.IsCarryingCargo)
        {
            // Проверка, чтобы не дублировать нажатие во время анимации
            if (_occupantNetId.Value != ulong.MaxValue &&
                interactor.CurrentCarriedCargo != null &&
                interactor.CurrentCarriedCargo.NetworkObject != null &&
                _occupantNetId.Value == interactor.CurrentCarriedCargo.NetworkObject.NetworkObjectId)
            {
                return;
            }

            if (!_allowCargo)
            {
                ColoredDebug.CLog(gameObject, "<color=red>[VehicleSeat]</color> Нельзя положить груз: AllowCargo выключен!", _ColoredDebug);
                return;
            }

            if (IsOccupied)
            {
                ColoredDebug.CLog(gameObject, "<color=red>[VehicleSeat]</color> Нельзя положить груз: Место занято!", _ColoredDebug);
                return;
            }

            // Все проверки пройдены, отправляем запрос
            if (interactor.CurrentCarriedCargo.NetworkObject.TryGetComponent<NetworkObject>(out var cargoNetObj) &&
                GetComponent<NetworkObject>().TryGetComponent<NetworkObject>(out var seatNetObj))
            {
                // ==========================================================
                // [PREDICTION] ДОБАВЛЕНО: Предсказание размещения в машину
                // ==========================================================
                if (interactor == Pawn.LocalPlayer && interactor.CurrentCarriedCargo is HeavyObject heavyCargo)
                {
                    heavyCargo.InitiatePredictivePlaceInVehicle(this, interactor);
                }
                // ==========================================================

                RequestPlaceCargoServerRpc(cargoNetObj.NetworkObjectId, seatNetObj.NetworkObjectId);
            }
            else
            {
                Debug.LogError("Ошибка получения NetworkObject для груза или сиденья!");
            }
            return;
        }

        // СЛУЧАЙ Б: Место занято грузом + у игрока руки свободны (Забираем из машины)
        if (HasCargo && !interactor.IsCarryingCargo)
        {
            if (interactor.TryGetComponent<NetworkObject>(out var pawnNetObj) &&
                _currentCargo.NetworkObject.TryGetComponent<NetworkObject>(out var cargoNetObj))
            {
                // [PREDICTION] Предсказание для забора из машины
                if (interactor == Pawn.LocalPlayer && _currentCargo is HeavyObject heavyCargo)
                {
                    heavyCargo.InitiatePredictiveTakeFromVehicle(interactor);
                }

                RequestTakeCargoServerRpc(pawnNetObj.NetworkObjectId, cargoNetObj.NetworkObjectId);
            }
            return;
        }

        // СЛУЧАЙ В: Обычная посадка игрока
        if (HasCargo)
        {
            ColoredDebug.CLog(gameObject, "<color=red>[INTERACTION]</color> Сиденье занято грузом, сесть нельзя!", _ColoredDebug);
            return;
        }

        if (IsOccupied)
        {
            ColoredDebug.CLog(gameObject, "<color=red>[VehicleSeat]</color> Нельзя сесть! Место занято!", _ColoredDebug);
            return;
        }

        if (interactor.TryGetComponent<NetworkObject>(out var pawnNet))
        {
            InitiatePredictiveEntry(interactor);
            RequestEnterServerRpc(pawnNet.NetworkObjectId);
        }
    }

    private void InitiatePredictiveEntry(Pawn interactor)
    {
        bool isLocalPlayer = (interactor == Pawn.LocalPlayer);
        StartCoroutine(PlayVisualEnterAnimation(interactor, isLocalPlayer));
    }

    public void Enter(Pawn pawn)
    {
        // Этот метод теперь вызывается ТОЛЬКО на сервере после одобрения
        // (см. RequestEnterServerRpc ниже)
    }

    public void OpenVehicleDoor()
    {
        if (_visualDoorOpened != null)
        {
            _visualDoorOpened.SetActive(true);
        }
    }

    public void CloseVehicleDoor()
    {
        if (_visualDoorOpened != null)
        {
            _visualDoorOpened.SetActive(false);
        }
    }
    #endregion

    #region Network RPCs
    [ServerRpc(RequireOwnership = false)]
    private void RequestEnterServerRpc(ulong pawnNetId, ServerRpcParams rpcParams = default)
    {
        // ✅ ШАГ 1: Проверка на сервере
        if (IsOccupied)
        {
            Debug.LogWarning($"[VehicleSeat] Место {Type} уже занято!");
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pawnNetId, out var pawnNetObj))
        {
            Debug.LogError($"[VehicleSeat] NetworkObject {pawnNetId} не найден!");
            return;
        }

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        if (pawn == null)
        {
            Debug.LogError($"[VehicleSeat] Pawn компонент не найден на {pawnNetObj.name}!");
            return;
        }

        // ✅ ШАГ 2: Сервер одобряет и занимает место
        _occupantNetId.Value = pawnNetId;

        // ✅ ШАГ 3: Сервер делает Reparent СРАЗУ (для синхронизации трансформа)
        pawnNetObj.TrySetParent(_seatPoint, true);

        ColoredDebug.CLog(gameObject,
            $"<color=lime>[SERVER]</color> Pawn {pawn.name} одобрен для посадки в {Type}",
            _ColoredDebug);

        if (_type == SeatType.Driver)
        {
            var ownership = _vehicle.GetComponent<NetworkedVehicleOwnership>();
            if (ownership != null)
            {
                // Передаем владение машиной игроку, который садится
                ownership.SetDriver(pawnNetObj.OwnerClientId);
            }
        }

        // ✅ ШАГ 4: Уведомляем ВСЕ клиенты (включая сервер-хост)
        EnterVehicleClientRpc(pawnNetId);
    }

    [ClientRpc]
    private void EnterVehicleClientRpc(ulong pawnNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pawnNetId, out var pawnNetObj))
            return;

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        if (pawn == null) return;

        // ✅ ШАГ 5: На ВСЕХ клиентах запускаем визуальную логику
        ColoredDebug.CLog(gameObject,
            $"<color=cyan>[CLIENT]</color> Начинаю посадку {pawn.name} в {Type}",
            _ColoredDebug);

        bool isLocalPlayer = (pawn == Pawn.LocalPlayer);
        if (isLocalPlayer == false)
        {
            // Для НЕ локального игрока просто запускаем анимацию. У локального уже проигрывалась анимация предсказания!
            StartCoroutine(PlayVisualEnterAnimation(pawn, false));
        }

        ConfirmOccupation(pawn, isLocalPlayer);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceCargoServerRpc(ulong cargoNetId, ulong seatNetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cargoNetId, out var cargoNetObj))
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(seatNetId, out var seatNetObj))
            return;

        ICargo cargo = cargoNetObj.GetComponent<ICargo>();
        VehicleSeat seat = seatNetObj.GetComponent<VehicleSeat>();

        if (cargo == null || seat == null) return;

        // Вызываем логику из HeavyObject
        cargo.PlaceInVehicle_Server(seat);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeCargoServerRpc(ulong pawnNetId, ulong cargoNetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pawnNetId, out var pawnNetObj))
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cargoNetId, out var cargoNetObj))
            return;

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        ICargo cargo = cargoNetObj.GetComponent<ICargo>();

        if (pawn == null || cargo == null) return;

        // Вызываем логику из HeavyObject
        cargo.TakeFromVehicle_Server(pawn, pawn.HoldPoint);
    }
    #endregion

    #region Публичные методы
    public void Occupy(Pawn pawn)
    {
        // Этот метод вызывается из PossessionManager после анимации
        // NetworkVariable уже установлена на сервере, здесь просто логируем
        ColoredDebug.CLog(gameObject,
            $"<color=cyan>[INFO]</color> Место {Type} подтверждено занятым: {pawn.name}",
            _ColoredDebug);
    }

    public void Vacate()
    {
        // ✅ ИЗМЕНЕНИЕ: Освобождение должно идти через сервер
        if (IsServer)
        {
            _occupantNetId.Value = ulong.MaxValue;
            ColoredDebug.CLog(gameObject,
                "<color=yellow>[SERVER]</color> Место освобождено.",
                _ColoredDebug);
        }
    }

    public void SetIconVisible(bool visible)
    {
        if (_iconObject != null)
            _iconObject.SetActive(visible);
    }
    #endregion

    #region Публичные методы: Cargo
    /// <summary>
    /// Разместить груз в этом сиденье
    /// </summary>
    public void OccupyCargo(ICargo cargo)
    {
        if (IsServer)
        {
            // Предполагаю, что код выглядит примерно так:
            if (cargo is NetworkBehaviour cargoNb)
            {
                _occupantNetId.Value = cargoNb.NetworkObjectId;
            }
        }

        _currentCargo = cargo;

        ColoredDebug.CLog(gameObject,
            $"<color=cyan>[CARGO]</color> Груз {cargo.CargoName} размещен в {Type}",
            _ColoredDebug);
    }

    /// <summary>
    /// Освободить место от груза
    /// </summary>
    public void VacateCargo()
    {
        if (_currentCargo != null)
        {
            ColoredDebug.CLog(gameObject,
                "<color=yellow>[CARGO]</color> Груз удален из сиденья",
                _ColoredDebug);
        }

        _currentCargo = null;
        if (IsServer)
        {
            _occupantNetId.Value = ulong.MaxValue;
        }
    }
    #endregion

    private IEnumerator PlayVisualEnterAnimation(Pawn pawn, bool isLocalPlayer)
    {
        Vector3 startPos = pawn.transform.position;
        Vector3 endPos = _seatPoint.position;

        pawn.Rigidbody.simulated = false;
        pawn.PlayerController.enabled = false;

        if (isLocalPlayer)
        {
            _interactionSystem.Deactivate();
        }

        OpenVehicleDoor();

        float elapsed = 0f;
        while (elapsed < _entryTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _entryTime;

            pawn.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        pawn.transform.localPosition = Vector3.zero;

        CloseVehicleDoor();

        // ✅ Управление камерой ТОЛЬКО для локального игрока
        if (isLocalPlayer)
        {
            if (Type == SeatType.Driver)
            {
                Vehicle.CarController.enabled = true;
                _cameraFollow.SetTarget(Vehicle.transform);
                _cameraFollow.SetMode(CameraFollow.CameraMode.Driving);
            }
            else
            {
                _cameraFollow.SetTarget(transform);
                _cameraFollow.SetMode(CameraFollow.CameraMode.Driving);
            }
        }

        // ✅ Скрываем персонажа (синхронизированно)
        var vehicleSync = pawn.GetComponent<NetworkedVehicleSync>();
        if (vehicleSync != null)
        {
            //vehicleSync.HidePawn();
            vehicleSync.SetPawnVisibility(false);
        }
        else
        {
            Debug.LogError("NetworkedVehicleSync компонент не найден на Pawn!");
        }
    }

    private void ConfirmOccupation(Pawn pawn, bool isLocalPlayer)
    {
        // ✅ Обновляем локальное состояние сиденья
        Occupy(pawn);

        if (isLocalPlayer)
        {
            pawn.AssignSeat(this);
            ///pawn.transform.position = _seatPoint.position;
        }
        ColoredDebug.CLog(gameObject,
      "<color=green>[SUCCESS]</color> Посадка завершена (локальный игрок).",
      _ColoredDebug);
    }

    public void ExitVehicle()
    {

        // ✅ ИЗМЕНЕНИЕ: Отправляем запрос на выход через сервер
        var pawn = Pawn.LocalPlayer;
        if (pawn != null && pawn.TryGetComponent<NetworkObject>(out var netObj))
        {
            RequestExitServerRpc(netObj.NetworkObjectId, GetComponent<NetworkObject>().NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestExitServerRpc(ulong pawnNetId, ulong seatNetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pawnNetId, out var pawnNetObj))
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(seatNetId, out var seatNetObj))
            return;

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        VehicleSeat seat = seatNetObj.GetComponent<VehicleSeat>();

        if (pawn == null || seat == null) return;

        // ✅ Освобождаем место на сервере
        seat.Vacate();

        // ✅ Reparent обратно в мир (null parent)
        pawnNetObj.TrySetParent((Transform)null, true);

        ColoredDebug.CLog(gameObject,
            $"<color=lime>[SERVER]</color> {pawn.name} выходит из {seat.Type}",
            _ColoredDebug);

        if (seat.Type == SeatType.Driver)
        {
            ///--Думаю не нужно, т.к. при выходе водитель теряет владение машиной. Поступлю как разработчики Армы 2!--///
            /* var ownership = seat.Vehicle.GetComponent<NetworkedVehicleOwnership>();
            if (ownership != null)
            {
                // Выполняется на сервере
                ownership.RemoveDriver();
            }
            */
        }

        // ✅ Уведомляем всех клиентов
        ExitVehicleClientRpc(pawnNetId, seatNetId);
    }

    [ClientRpc]
    private void ExitVehicleClientRpc(ulong pawnNetId, ulong seatNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pawnNetId, out var pawnNetObj))
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(seatNetId, out var seatNetObj))
            return;

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        VehicleSeat seat = seatNetObj.GetComponent<VehicleSeat>();

        if (pawn == null || seat == null) return;

        bool isLocalPlayer = (pawn == Pawn.LocalPlayer);

        StartCoroutine(ExitVehicleRoutine(pawn, seat, isLocalPlayer));
    }

    private IEnumerator ExitVehicleRoutine(Pawn pawn, VehicleSeat seat, bool isLocalPlayer)
    {
        // ✅ Позиционируем в точке выхода

        OpenVehicleDoor();

        Vector3 startPos = seat.SeatPoint.position;
        Vector3 endPos = seat.InteractionPoint.position;
        pawn.transform.position = startPos;

        // ✅ Показываем персонажа ПЕРЕД активацией
        var vehicleSync = pawn.GetComponent<NetworkedVehicleSync>();
        if (vehicleSync != null)
        {
            vehicleSync.ShowPawn();
        }

        // ✅ Сбрасываем вращение
        pawn.transform.rotation = Quaternion.identity;

        if (seat.Type == SeatType.Driver)
        {
            seat.Vehicle.CarController.enabled = false;
        }

        // ✅ Плавное перемещение к точке выхода
        float elapsed = 0f;
        while (elapsed < _entryTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _entryTime; // Коэффициент от 0 до 1

            // Плавное перемещение
            pawn.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        CloseVehicleDoor();

        pawn.Rigidbody.simulated = true;

        if (isLocalPlayer)
        {
            pawn.ClearSeat();

            PossessionManager.Instance.OnExitSeat(pawn, true);
            _cameraFollow.SetTarget(pawn.transform);
            _cameraFollow.SetMode(CameraFollow.CameraMode.Walking);
            _interactionSystem.LinkToPawn(pawn);
            ColoredDebug.CLog(gameObject,
                "<color=orange>[SYSTEM]</color> Локальный игрок вышел из транспорта.",
                _ColoredDebug);
        }
    }

    #region Editor / Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = HasAvailableSeats ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, _interactionRadius);

#if UNITY_EDITOR
        string label = HasAvailableSeats
            ? $"Доступно мест: {(_vehicle != null ? _vehicle.GetAvailableSeatsCount() : 0)}"
            : "Нет мест";

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            label,
            new GUIStyle { normal = new GUIStyleState { textColor = HasAvailableSeats ? Color.green : Color.red } }
        );
#endif
    }
    #endregion
}