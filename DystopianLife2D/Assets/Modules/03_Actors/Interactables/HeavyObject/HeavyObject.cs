// ================== HeavyObject.cs ================== //
// НАЗНАЧЕНИЕ: Тяжелый предмет (ящик), который можно переносить и размещать в транспорте
// ЗАВИСИМОСТИ: ICargo, IInteractable, NetworkBehaviour, Rigidbody2D
// ПРИМЕЧАНИЕ: Мгновенная синхронизация без корутин
// ========================================================== //

using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Unity.Multiplayer.PlayMode;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(NetworkObject))]
public class HeavyObject : InteractableBase, ICargo
{

    private static readonly Color NormalColor = Color.white;
    private static readonly Color PendingDropColor = new Color(1f, 0f, 0f, 0.5f);

    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Rigidbody2D _rigidbody;
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Collider2D _collider;
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private SpriteRenderer _spriteRenderer;
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Transform _visual;
    #endregion

    #region Поля: Settings
    [BoxGroup("SETTINGS"), SerializeField]
    private string _cargoName = "Ящик";
    [BoxGroup("SETTINGS/Interaction"), SerializeField]
    private Sprite _pickupIcon;
    [BoxGroup("SETTINGS/Interaction"), SerializeField]
    private int _interactionPriority = 7;
    [BoxGroup("SETTINGS/Physics"), SerializeField]
    private float _dropForceMultiplier = 5f;
    #endregion

    #region Поля: Network Sync
    private readonly NetworkVariable<CargoState> _netState = new(
         CargoState.OnGround,
         NetworkVariableReadPermission.Everyone,
         NetworkVariableWritePermission.Server
     );

    private readonly NetworkVariable<NetworkObjectReference> _carrierRef = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<NetworkObjectReference> _seatRef = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    #endregion

    #region Поля: Debug
    [BoxGroup("DEBUG"), ReadOnly, ShowInInspector]
    private CargoState _currentState => _netState.Value;
    [BoxGroup("DEBUG"), ReadOnly, ShowInInspector]
    private string _carrierName => CurrentCarrier != null ? CurrentCarrier.name : "None";
    [BoxGroup("DEBUG"), SerializeField]
    protected bool _ColoredDebug = true;

    [BoxGroup("DEBUG"), SerializeField] private bool _isDropPendingClient;
    [BoxGroup("DEBUG"), SerializeField] private bool _isPickupPendingClient;
    #endregion

    #region Свойства: ICargo
    public string CargoName => _cargoName;
    public override Transform Transform => transform;
    public CargoState CurrentState => _netState.Value;

    public Pawn CurrentCarrier
    {
        get
        {
            return _carrierRef.Value.TryGet(out NetworkObject netObj)
                ? netObj.GetComponent<Pawn>()
                : null;
        }
    }

    public VehicleSeat CurrentSeat
    {
        get
        {
            return _seatRef.Value.TryGet(out NetworkObject netObj)
                ? netObj.GetComponent<VehicleSeat>()
                : null;
        }
    }
    #endregion

    #region Свойства: IInteractable
    public override Vector3 Position => transform.position;
    //public override Transform Transform => transform;
    public Transform Visual => _visual;

    public override bool CanInteract
    {
        get
        {
            return CurrentState == CargoState.OnGround ||
                   CurrentState == CargoState.InVehicle;
        }
    }

    public override int InteractionPriority => _interactionPriority;
    public override Vector3 InteractionPosition => transform.position;

    public override Sprite GetInteractionIcon() => _pickupIcon;

    public override string GetInteractionHint()
    {
        switch (CurrentState)
        {
            case CargoState.OnGround:
                return $"Поднять {_cargoName}";
            case CargoState.InVehicle:
                return $"Забрать {_cargoName} из машины";
            case CargoState.Carried:
                return $"{_cargoName} (в руках)";
            default:
                return _cargoName;
        }
    }

    public override void Interact(Pawn interactor)
    {
        if (!CanInteract) return;

        if (interactor.CurrentCarriedCargo != null)
        {
            ColoredDebug.CLog(gameObject, "<color=orange>[CARGO]</color> У игрока уже есть груз!", _ColoredDebug);
            return;
        }

        if (CurrentState == CargoState.OnGround)
        {
            // Используем prediction при подборе
            if (interactor == Pawn.LocalPlayer)
            {
                interactor.PredictivePickup(this);
            }
            else
            {
                RequestPickUpServerRpc(interactor.NetworkObject, this.NetworkObject);
            }
        }
        else if (CurrentState == CargoState.InVehicle)
        {
            RequestTakeFromVehicleServerRpc(interactor.NetworkObject, this.NetworkObject);
        }
    }
    #endregion

    #region Unity Методы
    private void Awake()
    {
        if (_rigidbody == null) DebugUtils.LogMissingReference(this, nameof(_rigidbody));
        if (_collider == null) DebugUtils.LogMissingReference(this, nameof(_collider));
        if (_spriteRenderer == null) DebugUtils.LogMissingReference(this, nameof(_spriteRenderer));
    }

    public override void OnNetworkSpawn()
    {
        _netState.OnValueChanged += OnStateChanged;
        ApplyState(_netState.Value);
    }

    public override void OnNetworkDespawn()
    {
        _netState.OnValueChanged -= OnStateChanged;
    }
    #endregion


    public void InitiatePredictiveDropClient(Pawn pawn)
    {
        // 1. Помечаем, что клиент ЖДЕТ подтверждения
        SetPendingVisual(true);

        // 2. Отцепляем визуал от рук
        if (_visual != null)
        {
            _visual.SetParent(null, true);
        }

        ColoredDebug.CLog(
            gameObject,
            "<color=cyan>[PREDICTION]</color> Drop pending (red transparent)",
            _ColoredDebug
        );
    }

    #region Network: Server RPC
    [ServerRpc(RequireOwnership = false)]
    public void RequestPickUpServerRpc(NetworkObjectReference pawnRef, NetworkObjectReference cargoRef, ServerRpcParams rpcParams = default)
    {
        if (!pawnRef.TryGet(out NetworkObject pawnNetObj)) return;
        if (!cargoRef.TryGet(out NetworkObject cargoNetObj)) return;

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        HeavyObject cargo = cargoNetObj.GetComponent<HeavyObject>();

        if (pawn == null || cargo == null)
        {
            Debug.Log("RequestPickUpServerRpc <color=red>pawn == null || cargo == null</color>", this);
            PickUpRejectedClientRpc(cargoRef, cargo.transform.position, pawnRef);
            return;
        }

        if (cargo.CurrentState != CargoState.OnGround)
        {
            Debug.Log("RequestPickUpServerRpc <color=red>cargo.CurrentState != CargoState.OnGround</color>", this);
            PickUpRejectedClientRpc(cargoRef, cargo.transform.position, pawnRef);
            return;
        }

        if (pawn.CurrentCarriedCargo != null && !IsServer)
        {
            Debug.Log("RequestPickUpServerRpc <color=red>pawn.CurrentCarriedCargo != null && !IsServer</color>", this);
            PickUpRejectedClientRpc(cargoRef, cargo.transform.position, pawnRef);
            return;
        }

        // Сохраняем позицию для возможного отката
        Vector3 originalPosition = cargo.transform.position;

        try
        {
            cargo.PickUp_Server(pawn);
        }
        catch
        {
            Debug.Log("RequestPickUpServerRpc <color=red> catch</color>", this);
            PickUpRejectedClientRpc(cargoRef, originalPosition, pawnRef);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceInVehicleServerRpc(NetworkObjectReference cargoRef, NetworkObjectReference seatRef, ServerRpcParams rpcParams = default)
    {
        if (!cargoRef.TryGet(out NetworkObject cargoNetObj) || !seatRef.TryGet(out NetworkObject seatNetObj))
            return;

        HeavyObject cargo = cargoNetObj.GetComponent<HeavyObject>();
        VehicleSeat seat = seatNetObj.GetComponent<VehicleSeat>();

        if (cargo == null || seat == null || cargo.CurrentState != CargoState.Carried) return;
        if (!seat.AllowCargo || seat.IsOccupied) return;

        // Сохраняем ссылку на несущего для ClientRpc
        NetworkObjectReference previousCarrierRef = cargo._carrierRef.Value;

        // Владение серверу
        cargo.TransferOwnership_Server(NetworkManager.ServerClientId);

        // Обновляем сетевые переменные
        cargo._netState.Value = CargoState.InVehicle;
        cargo._carrierRef.Value = default;
        cargo._seatRef.Value = seatNetObj;

        // Занимаем сиденье
        seat.OccupyCargo(cargo);

        // Parenting
        cargoNetObj.TrySetParent(seat.SeatPoint, true);

        ColoredDebug.CLog(gameObject, $"<color=lime>[SERVER]</color> Груз размещен в {seat.Type}", _ColoredDebug);

        // Оповещаем клиентов
        PlaceInVehicleClientRpc(cargoRef, seatRef, previousCarrierRef);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeFromVehicleServerRpc(NetworkObjectReference pawnRef, NetworkObjectReference cargoRef, ServerRpcParams rpcParams = default)
    {
        if (!pawnRef.TryGet(out NetworkObject pawnNetObj) || !cargoRef.TryGet(out NetworkObject cargoNetObj))
            return;

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        HeavyObject cargo = cargoNetObj.GetComponent<HeavyObject>();

        if (pawn == null || cargo == null) return;
        if (cargo.CurrentState != CargoState.InVehicle) return;

        // ИСПРАВЛЕНИЕ: Разрешаем брать, если руки пусты ИЛИ если в руках уже этот же объект (случай Хоста)
        if (pawn.CurrentCarriedCargo != null && pawn.CurrentCarriedCargo != (ICargo)cargo)
        {
            ColoredDebug.CLog(gameObject, "<color=red>[SERVER]</color> Отказано: руки заняты другим объектом", _ColoredDebug);
            return;
        }

        // ... далее ваш код (TransferOwnership, VacateCargo и т.д.) ...

        // Передаем владение игроку
        cargo.TransferOwnership_Server(rpcParams.Receive.SenderClientId);

        if (cargo.CurrentSeat != null)
            cargo.CurrentSeat.VacateCargo();

        cargo._netState.Value = CargoState.Carried;
        cargo._seatRef.Value = default;
        cargo._carrierRef.Value = pawnNetObj;
        cargoNetObj.TrySetParent(pawnNetObj.transform);

        TakeFromVehicleClientRpc(pawnRef, cargoRef);
    }
    #endregion

    #region Network: Client RPC
    [ClientRpc]
    private void PickUpClientRpc(NetworkObjectReference pawnRef, NetworkObjectReference cargoRef)
    {
        if (!pawnRef.TryGet(out var pawnNet) || !cargoRef.TryGet(out var cargoNet))
            return;

        Pawn pawn = pawnNet.GetComponent<Pawn>();
        HeavyObject cargo = cargoNet.GetComponent<HeavyObject>();

        if (pawn == null || cargo == null) return;

        // Убрать условие проверки на локального игрока - выполнять для ВСЕХ клиентов
        Transform visual = cargo.Visual;

        // Всегда обновляем визуал для всех клиентов
        if (visual != null && pawn.HoldPoint != null)
        {
            // Сохраняем мировой scale
            Vector3 worldScale = visual.lossyScale;

            visual.SetParent(pawn.HoldPoint, true);

            // Возвращаем scale
            visual.localScale = new Vector3(
                worldScale.x / pawn.HoldPoint.lossyScale.x,
                worldScale.y / pawn.HoldPoint.lossyScale.y,
                worldScale.z / pawn.HoldPoint.lossyScale.z
            );

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        // Локальная логика только для локального игрока
        if (pawn == Pawn.LocalPlayer)
        {
            if (cargo._isPickupPendingClient)
            {
                // Подтверждение prediction
                cargo.SetPendingPickupVisual(false);
                pawn.IsActionPending = false;

                ColoredDebug.CLog(gameObject,
                    "<color=yellow>[CLIENT]</color> Сервер подтвердил подбор",
                    _ColoredDebug);
            }

            // Назначаем груз локально
            pawn.AssignCargo(cargo);
        }

        // Для наблюдателей: просто обновить визуал (уже сделано выше)
    }

    [ClientRpc]
    private void DropClientRpc(
    NetworkObjectReference pawnRef,
    NetworkObjectReference cargoRef,
    Vector3 targetPosition)
    {
        if (!pawnRef.TryGet(out var pawnNet) || !cargoRef.TryGet(out var cargoNet))
            return;

        Pawn pawn = pawnNet.GetComponent<Pawn>();
        HeavyObject cargo = cargoNet.GetComponent<HeavyObject>();

        if (pawn == null || cargo == null) return;

        cargo.SetPendingPickupVisual(false);
        cargo.SetPendingDropVisual(false);

        // СЕРВЕР ПРИСЛАЛ ПОДТВЕРЖДЕНИЕ
        Transform visual = cargo.Visual;

        // 1. Возвращаем визуал обратно "внутрь" сетевого объекта HeavyObject
        // Потому что теперь сам HeavyObject переместился на позицию броска
        visual.SetParent(cargo.transform, true);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;

        visual.gameObject.SetActive(true); // На всякий случай

        if (pawn == Pawn.LocalPlayer)
        {
            pawn.ClearCargo();
            pawn.IsActionPending = false; // Разрешаем снова нажимать G
        }

        cargo.SetPendingVisual(false);

        ColoredDebug.CLog(gameObject, "<color=yellow>[CLIENT]</color> Сервер подтвердил бросок", _ColoredDebug);
    }

    /// <summary>
    /// Пока не используется!
    /// </summary>
    /// <param name="cargoRef"></param>
    [ClientRpc]
    private void DropRejectedClientRpc(NetworkObjectReference cargoRef)
    {
        if (!cargoRef.TryGet(out var netObj)) return;
        var cargo = netObj.GetComponent<HeavyObject>();

        cargo.SetPendingVisual(false);

        if (Pawn.LocalPlayer != null)
            Pawn.LocalPlayer.IsActionPending = false;

        // вернуть визуал обратно в руки
    }

    [ClientRpc]
    private void PlaceInVehicleClientRpc(NetworkObjectReference cargoRef, NetworkObjectReference seatRef, NetworkObjectReference carrierRef)
    {
        if (!cargoRef.TryGet(out NetworkObject cargoNetObj) || !seatRef.TryGet(out NetworkObject seatNetObj))
            return;

        HeavyObject cargo = cargoNetObj.GetComponent<HeavyObject>();
        VehicleSeat seat = seatNetObj.GetComponent<VehicleSeat>();

        if (cargo == null || seat == null) return;

        Pawn carrier = null;
        if (carrierRef.TryGet(out NetworkObject carrierNetObj))
        {
            carrier = carrierNetObj.GetComponent<Pawn>();
        }

        bool isLocalPlayer = (carrier != null && carrier == Pawn.LocalPlayer);

        // 🔥 МГНОВЕННО без корутины (как с Pawn)
        PlaceInVehicle_Client(cargo, seat, carrier, isLocalPlayer);
    }

    [ClientRpc]
    private void TakeFromVehicleClientRpc(NetworkObjectReference pawnRef, NetworkObjectReference cargoRef)
    {
        if (!pawnRef.TryGet(out NetworkObject pawnNetObj) || !cargoRef.TryGet(out NetworkObject cargoNetObj))
            return;

        Pawn pawn = pawnNetObj.GetComponent<Pawn>();
        HeavyObject cargo = cargoNetObj.GetComponent<HeavyObject>();

        if (pawn == null || cargo == null) return;

        bool isLocalPlayer = (pawn == Pawn.LocalPlayer);

        // 🔥 МГНОВЕННО без корутины (как с Pawn)
        TakeFromVehicle_Client(pawn, cargo, isLocalPlayer);
    }
    #endregion

    #region Client Visual Logic (БЕЗ КОРУТИН!)
    private void PlaceInVehicle_Client(HeavyObject cargo, VehicleSeat seat, Pawn carrier, bool isLocalPlayer)
    {
        // 1. МГНОВЕННОЕ размещение визуала (для всех клиентов и Хоста)
        Transform visual = cargo.Visual;
        if (visual != null)
        {
            Vector3 worldScale = visual.lossyScale;

            visual.SetParent(seat.SeatPoint, true);

            visual.localScale = new Vector3(
                worldScale.x / seat.SeatPoint.lossyScale.x,
                worldScale.y / seat.SeatPoint.lossyScale.y,
                worldScale.z / seat.SeatPoint.lossyScale.z
            );

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        // 2. Занимаем сиденье (логически)
        seat.OccupyCargo(cargo);

        // 3. Локальная логика подтверждения
        if (isLocalPlayer)
        {
            // Если мы ждали подтверждения (был prediction)
            if (cargo._isDropPendingClient)
            {
                cargo.SetPendingDropVisual(false); // Возвращаем нормальный цвет
                cargo._isDropPendingClient = false;

                if (carrier != null)
                {
                    carrier.IsActionPending = false; // Разблокируем ввод
                    carrier.ClearCargo();            // Очищаем руки окончательно
                }

                ColoredDebug.CLog(gameObject,
                    "<color=yellow>[CLIENT]</color> Размещение в машине подтверждено (Prediction End)",
                    _ColoredDebug);
            }
            else if (carrier != null)
            {
                // Если предсказания не было (например, форсировано сервером), просто чистим руки
                carrier.ClearCargo();
            }
        }
        else if (carrier != null)
        {
            // Для остальных клиентов просто визуально освобождаем руки того, кто положил
            // (Хотя ClearCargo в Pawn делает LookAt, что может быть лишним для прокси, но допустимо)
        }

        ColoredDebug.CLog(gameObject,
            $"<color=green>[CLIENT]</color> Груз размещен в машине {(isLocalPlayer ? "(локальный игрок)" : "")}",
            _ColoredDebug);
    }

    private void TakeFromVehicle_Client(Pawn pawn, HeavyObject cargo, bool isLocalPlayer)
    {
        // 1. Освобождаем сиденье
        if (cargo.CurrentSeat != null)
        {
            cargo.CurrentSeat.VacateCargo();
        }

        // 2. Визуальное размещение (для всех клиентов)
        Transform visual = cargo.Visual;
        if (visual != null && pawn.HoldPoint != null)
        {
            Vector3 worldScale = visual.lossyScale;
            visual.SetParent(pawn.HoldPoint, true);
            visual.localScale = new Vector3(
                worldScale.x / pawn.HoldPoint.lossyScale.x,
                worldScale.y / pawn.HoldPoint.lossyScale.y,
                worldScale.z / pawn.HoldPoint.lossyScale.z
            );
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        // 3. Сброс предсказания
        if (isLocalPlayer)
        {
            // Мы используем cargo._isPickupPendingClient, чтобы понять, что нужно выключить цвет
            if (cargo._isPickupPendingClient)
            {
                cargo.SetPendingPickupVisual(false);
                cargo._isPickupPendingClient = false;
            }

            pawn.IsActionPending = false; // Всегда разблокируем, если это наш павн
            pawn.AssignCargo(cargo);      // Подтверждаем назначение

            ColoredDebug.CLog(gameObject, "<color=yellow>[CLIENT]</color> Забор из авто подтвержден", _ColoredDebug);
        }
    }
    #endregion

    #region ICargo Implementation
    public void PickUp_Server(Pawn picker)
    {
        if (!IsServer) return;

        NetworkObject cargoNetObj = NetworkObject;
        NetworkObject pawnNetObj = picker.NetworkObject;

        TransferOwnership_Server(picker.NetworkObject.OwnerClientId);

        cargoNetObj.TrySetParent(picker.HoldPoint, true);
        cargoNetObj.transform.position = picker.HoldPoint.position;

        _netState.Value = CargoState.Carried;
        _carrierRef.Value = pawnNetObj;
        _seatRef.Value = default;

        _rigidbody.simulated = false;
        _collider.enabled = false;

        PickUpClientRpc(pawnNetObj, cargoNetObj);

        ColoredDebug.CLog(
            gameObject,
            $"<color=lime>[SERVER]</color> {picker.name} поднял {_cargoName}",
            _ColoredDebug);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDropServerRpc(NetworkObjectReference cargoRef, Vector3 targetPosition, ServerRpcParams rpcParams = default)
    {
        if (!cargoRef.TryGet(out NetworkObject cargoNetObj))
        {
            Debug.LogError("<color=red>Failed to get cargo NetworkObject!</color>", this);
            return;
        }
        HeavyObject cargo = cargoNetObj.GetComponent<HeavyObject>();

        if (cargo == null || cargo.CurrentState != CargoState.Carried)
        {
            Debug.LogError("<color=red>Cargo is null or not carried!</color>", this);
            return;
        }

        if (cargo.CurrentCarrier == null ||
    cargo.CurrentCarrier.NetworkObject.OwnerClientId != rpcParams.Receive.SenderClientId)
        {
            Debug.LogError("Sender is not the carrier!");
            return;
        }

        cargo.Drop_Server(targetPosition);
    }

    public void Drop_Server(Vector3 targetPosition)
    {
        if (!IsServer) return;

        NetworkObject cargoNetObj = NetworkObject;
        NetworkObjectReference carrierRef = _carrierRef.Value;
        ///Vector3 dropPos = transform.position;

        if (carrierRef.TryGet(out NetworkObject carrierNetObj))
        {
            ///dropPos = carrierNetObj.transform.position + Vector3.down;
            if (carrierNetObj.TryGetComponent<Pawn>(out var pawn))
            {
                pawn.ClearCargo();
            }
        }

        TransferOwnership_Server(NetworkManager.ServerClientId);

        cargoNetObj.TrySetParent((Transform)null, true);

        ///transform.position = dropPos;
        transform.position = targetPosition;

        _netState.Value = CargoState.OnGround;
        _carrierRef.Value = default;
        _seatRef.Value = default;

        _rigidbody.simulated = true;
        _collider.enabled = true;
        _rigidbody.linearVelocity = Vector2.zero;

        DropClientRpc(carrierRef, cargoNetObj, targetPosition);

        ColoredDebug.CLog(
            gameObject,
            "<color=yellow>[SERVER]</color> Груз сброшен",
            _ColoredDebug);
    }

    public void PlaceInVehicle_Server(VehicleSeat seat)
    {
        if (!IsServer) return;
        RequestPlaceInVehicleServerRpc(NetworkObject, seat.NetworkObject);
    }

    public void TakeFromVehicle_Server(Pawn taker, Transform holdPoint)
    {
        if (!IsServer) return;
        RequestTakeFromVehicleServerRpc(taker.NetworkObject, NetworkObject);
    }
    #endregion

    #region State Management
    private void OnStateChanged(CargoState oldState, CargoState newState)
    {
        ApplyState(newState);
    }

    private void ApplyState(CargoState state)
    {
        var netTransform = GetComponent<NetworkTransform>();

        switch (state)
        {
            case CargoState.OnGround:
                if (IsServer)
                {
                    netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
                }
                netTransform.enabled = true;
                _rigidbody.simulated = true;
                _collider.enabled = true;
                break;

            case CargoState.Carried:
            case CargoState.InVehicle:
                netTransform.enabled = false;
                _rigidbody.simulated = false;
                _collider.enabled = false;
                break;
        }
    }
    #endregion

    #region Editor / Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = CurrentState switch
        {
            CargoState.OnGround => Color.green,
            CargoState.Carried => Color.yellow,
            CargoState.InVehicle => Color.cyan,
            _ => Color.white
        };

        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
    #endregion

    private void TransferOwnership_Server(ulong newOwnerClientId)
    {
        if (!IsServer) return;

        if (NetworkObject.OwnerClientId == newOwnerClientId) return;

        if (newOwnerClientId == NetworkManager.ServerClientId)
        {
            NetworkObject.RemoveOwnership();
            ColoredDebug.CLog(gameObject, "<color=cyan>[NETWORK]</color> Владение возвращено серверу", _ColoredDebug);
        }
        else
        {
            NetworkObject.ChangeOwnership(newOwnerClientId);
            ColoredDebug.CLog(gameObject, $"<color=cyan>[NETWORK]</color> Владение передано клиенту: {newOwnerClientId}", _ColoredDebug);
        }
    }

    private void SetPendingVisual(bool pending, bool isPickup = false)
    {
        if (isPickup)
            _isPickupPendingClient = pending;
        else
            _isDropPendingClient = pending;

        if (_spriteRenderer == null)
            return;

        if (pending)
        {
            _spriteRenderer.color = isPickup
                ? new Color(0f, 1f, 0f, 0.5f) // Зеленый для подбора
                : PendingDropColor; // Красный для выброса
        }
        else
        {
            _spriteRenderer.color = NormalColor;
        }
    }

    // Обновите существующие вызовы:
    private void SetPendingPickupVisual(bool pending) => SetPendingVisual(pending, true);
    private void SetPendingDropVisual(bool pending) => SetPendingVisual(pending, false);

    public void InitiatePredictivePickupClient(Pawn pawn)
    {
        // 1. Помечаем, что клиент ЖДЕТ подтверждения подбора
        SetPendingPickupVisual(true);

        // 2. Выключаем физику и коллайдер локально
        _rigidbody.simulated = false;
        _collider.enabled = false;

        // 3. Уже привязываем визуал к рукам (предсказание)
        if (_visual != null)
        {
            // Сохраняем мировой scale
            Vector3 worldScale = _visual.lossyScale;

            _visual.SetParent(pawn.HoldPoint, true);

            // Возвращаем scale
            _visual.localScale = new Vector3(
                worldScale.x / pawn.HoldPoint.lossyScale.x,
                worldScale.y / pawn.HoldPoint.lossyScale.y,
                worldScale.z / pawn.HoldPoint.lossyScale.z
            );

            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
        }

        ColoredDebug.CLog(
            gameObject,
            "<color=cyan>[PREDICTION]</color> Pickup pending (green transparent)",
            _ColoredDebug
        );
    }

    [ClientRpc]
    private void PickUpRejectedClientRpc(NetworkObjectReference cargoRef, Vector3 originalPosition, NetworkObjectReference pawnRef)
    {
        if (!pawnRef.TryGet(out NetworkObject pawnNetObj)) return;

        if (Pawn.LocalPlayer != pawnNetObj)
        {
            return;
        }

        if (!cargoRef.TryGet(out var netObj)) return;
        var cargo = netObj.GetComponent<HeavyObject>();

        if (cargo == null) return;

        // Откатываем prediction
        cargo.SetPendingPickupVisual(false);

        // Возвращаем визуал на место
        if (cargo.Visual != null)
        {
            cargo.Visual.SetParent(cargo.transform, true);
            cargo.Visual.localPosition = Vector3.zero;
            cargo.Visual.localRotation = Quaternion.identity;
        }

        // Включаем физику обратно
        cargo._rigidbody.simulated = true;
        cargo._collider.enabled = true;

        // Возвращаем на позицию до подбора
        cargo.transform.position = originalPosition;

        // Очищаем локальный груз
        if (Pawn.LocalPlayer != null)
        {
            Pawn.LocalPlayer.ClearCargo();
            Pawn.LocalPlayer.IsActionPending = false;
        }

        SetPendingVisual(false);

        ColoredDebug.CLog(gameObject,
            "<color=red>[CLIENT]</color> Сервер отверг подбор (откат)",
            _ColoredDebug);
    }

    public void InitiatePredictiveTakeFromVehicle(Pawn pawn)
    {
        if (_isPickupPendingClient) return;

        // 1. Включаем визуальный "ожидающий" режим (зеленый полупрозрачный)
        SetPendingPickupVisual(true);

        // 2. Ставим замок на действия игрока
        pawn.IsActionPending = true;

        // 3. Сразу перемещаем визуал в руки
        if (_visual != null)
        {
            Vector3 worldScale = _visual.lossyScale;
            _visual.SetParent(pawn.HoldPoint, true);

            _visual.localScale = new Vector3(
                worldScale.x / pawn.HoldPoint.lossyScale.x,
                worldScale.y / pawn.HoldPoint.lossyScale.y,
                worldScale.z / pawn.HoldPoint.lossyScale.z
            );

            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
        }

        // 4. Назначаем локально, чтобы InteractionSystem понимала, что руки заняты
        pawn.AssignCargo(this);

        ColoredDebug.CLog(gameObject, "<color=cyan>[PREDICTION]</color> TakeFromVehicle started", _ColoredDebug);
    }

    public void InitiatePredictivePlaceInVehicle(VehicleSeat seat, Pawn pawn)
    {
        if (_isDropPendingClient) return;

        // 1. Включаем визуальный "ожидающий" режим (Красный полупрозрачный, как при Drop)
        SetPendingDropVisual(true);

        // 2. Блокируем действия
        pawn.IsActionPending = true;

        // 3. Мгновенно перемещаем визуал в слот сиденья
        if (_visual != null)
        {
            Vector3 worldScale = _visual.lossyScale;

            _visual.SetParent(seat.SeatPoint, true);

            // Сохраняем масштаб
            _visual.localScale = new Vector3(
                worldScale.x / seat.SeatPoint.lossyScale.x,
                worldScale.y / seat.SeatPoint.lossyScale.y,
                worldScale.z / seat.SeatPoint.lossyScale.z
            );

            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
        }

        // 4. Локально очищаем руки (но не удаляем ссылку совсем, пока сервер не подтвердит)
        // Визуально предмет уже в машине

        ColoredDebug.CLog(gameObject, "<color=cyan>[PREDICTION]</color> PlaceInVehicle pending (red transparent)", _ColoredDebug);
    }
}
