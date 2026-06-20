// НАЗНАЧЕНИЕ: Паспорт персонажа. Хранит ссылки на все части.
// ЗАВИСИМОСТИ: LocalPlayerController, BotController, Rigidbody2D, Pathfinding components
// ПРИМЕЧАНИЕ: Использование обязательных групп Required/SETTINGS/DEBUG

using Pathfinding;
using Pathfinding.RVO;
using Sirenix.OdinInspector;
using System;
using System.Globalization;
using UniRx;
using UniRx.Triggers;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(10)]
public class Pawn : NetworkBehaviour
{
    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private GameObject _visual;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private LocalPlayerController _playerController;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private BotController _botController;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Rigidbody2D _rigidbody;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Transform _directionTransform;

    [PropertyOrder(-1), BoxGroup("Required/AI"), Required(InfoMessageType.Error), SerializeField]
    private AIPath _aiPath;

    [PropertyOrder(-1), BoxGroup("Required/AI"), Required(InfoMessageType.Error), SerializeField]
    private Seeker _seeker;

    [PropertyOrder(-1), BoxGroup("Required/AI"), Required(InfoMessageType.Error), SerializeField]
    private RVOController _RVOController;

    [PropertyOrder(-1), BoxGroup("Required/AI"), Required(InfoMessageType.Error), SerializeField]
    private AIDestinationSetter _AIDestinationSetter;

    [BoxGroup("SETTINGS/Visuals"), SerializeField]
    private float _rotationSpeed = 15f; // Скорость поворота "глаз"

    [BoxGroup("SETTINGS/Visuals"), SerializeField, ReadOnly]
    private float _currentAngle; // Текущий локальный угол для интерполяции

    [BoxGroup("SETTINGS/Visuals"), SerializeField]
    private float _rotationVelocityThreshold = 0.1f; // Минимальная скорость для поворота
    #endregion

    #region Поля: Settings
    [BoxGroup("SETTINGS"), SerializeField] private float _moveSpeed = 10f;
    #endregion

    #region Поля: Debug
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug = false;
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private Vector3? _forcedLookTarget;
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private float _forcedLookTimer;
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private Vector2Int сurrentGridCell;
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private bool _isBusy;
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private VehicleSeat _currentSeat;
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private Vector3 _preInteractionPosition;
    #endregion

    private readonly NetworkVariable<float> _netEyeRotation = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );


    #region Поля: Cargo System
    [BoxGroup("DEBUG/Cargo"), ShowInInspector, ReadOnly]
    private ICargo _currentCarriedCargo;

    [PropertyOrder(-1), BoxGroup("Required/Cargo"), Required(InfoMessageType.Error), SerializeField]
    private Transform _holdPoint; // Точка крепления груза на руках
    #endregion

    
    private readonly CompositeDisposable _disposables = new();

    #region Свойства
    public float MoveSpeed => _moveSpeed;
    public VehicleSeat CurrentSeat => _currentSeat;

    public bool IsInVehicle => _currentSeat != null;
    public VehicleSeat.SeatType? CurrentVehicleRole => _currentSeat != null ? _currentSeat.Type : null;

    public LocalPlayerController PlayerController => _playerController;
    public BotController BotController => _botController;
    public Rigidbody2D Rigidbody => _rigidbody;
    public AIPath AIPath => _aiPath;
    public Seeker Seeker => _seeker;
    public RVOController RVOController => _RVOController;
    public AIDestinationSetter AIDestinationSetter => _AIDestinationSetter;
    //public NetworkObject NetworkObject => GetComponent<NetworkObject>();
    public static Pawn LocalPlayer { get; private set; }

    public float EyeRotationAngle => _netEyeRotation.Value;

    public bool IsActionPending { get; set; } // Блокировка действий на время сетевого пинга

    #endregion

    #region Свойства: Cargo
    public ICargo CurrentCarriedCargo => _currentCarriedCargo;
    public bool IsCarryingCargo => _currentCarriedCargo != null;
    public Transform HoldPoint => _holdPoint;
    public bool IsInsideVehicle => CurrentSeat != null;
    public bool IsBusy
    {
        get => _isBusy;
        set => _isBusy = value;
    }
    public Vector2Int СurrentGridCell { get => сurrentGridCell;  }
    #endregion

    #region Unity Методы
    private void Awake()
    {
        if (_playerController == null) DebugUtils.LogMissingReference(this, nameof(_playerController));
        if (_botController == null) DebugUtils.LogMissingReference(this, nameof(_botController));
        if (_rigidbody == null) DebugUtils.LogMissingReference(this, nameof(_rigidbody));
        if (_directionTransform == null) DebugUtils.LogMissingReference(this, nameof(_directionTransform));

        // Деактивация контроллеров при старте
        if (_playerController) _playerController.enabled = false;
        if (_botController) _botController.enabled = false;
        if (_aiPath) _aiPath.enabled = false;
        if (_seeker) _seeker.enabled = false;
        if (_RVOController) _RVOController.enabled = false;
        if (_AIDestinationSetter) _AIDestinationSetter.enabled = false;
    }

    private void Start()
    {
        if (PossessionManager.Instance != null)
        {
            PossessionManager.Instance.RegisterPawn(this);
        }
        else
        {
            ColoredDebug.CLog(gameObject, "<color=red>[ERROR]</color> PossessionManager не найден на сцене!", _ColoredDebug);
        }

        SpatialGrid.Instance.AddPawn(this);

        // Инициализируем текущий угол
        if (_directionTransform != null)
            _currentAngle = _directionTransform.eulerAngles.z;

        this.UpdateAsObservable()
            .Subscribe(_ => HandleDirectionRotation())
            .AddTo(_disposables);
    }

    void FixedUpdate()
    {
        SpatialGrid.Instance.UpdatePawnCell(this);
    }

    private void OnDestroy()
    {
        if (LocalPlayer == this)
        {
            LocalPlayer = null;
        }
    }
    #endregion

    #region Публичные методы
    public void SetAsLocalPlayer()
    {
        LocalPlayer = this;
        ColoredDebug.CLog(gameObject, "<color=lime>[ACTION]</color> Теперь это активный игрок: {0}", _ColoredDebug, name);
    }

    public void RemoveLocalPlayerStatus()
    {
        if (LocalPlayer == this) LocalPlayer = null;
    }

    public void AssignSeat(VehicleSeat seat)
    {
        _currentSeat = seat;
        ColoredDebug.CLog(gameObject, "<color=orange>[SYSTEM]</color> Назначено место: {0}", _ColoredDebug, seat.Type);
    }

    public void ClearSeat()
    {
        _currentSeat = null;
        ColoredDebug.CLog(gameObject, "<color=orange>[SYSTEM]</color> Место освобождено (вышел из авто).", _ColoredDebug);
    }

    public void SyncSpeeds()
    {
        if (_aiPath != null)
        {
            _aiPath.maxSpeed = _moveSpeed;
            // Ускорение в 3 раза выше скорости согласно логике класса
            _aiPath.maxAcceleration = _moveSpeed * 3f;
        }
    }

    /// <summary>
    /// Заставляет павна смотреть в определенную точку в течение заданного времени.
    /// Это перекрывает поворот по направлению движения.
    /// </summary>
    public void LookAt(Vector3 targetPosition, float duration = 0.25f)
    {
        _forcedLookTarget = targetPosition;
        _forcedLookTimer = duration;
    }

    public void PredictiveDrop()
    {
        if (_currentCarriedCargo == null || IsActionPending) return;

        if (_currentCarriedCargo is HeavyObject heavy)
        {
            IsActionPending = true; // Захлопываем "замок"

            // 1. Предсказание: Визуально скрываем ящик и подготавливаемся
            heavy.InitiatePredictiveDropClient(this);

            var visualPos = ((HeavyObject)_currentCarriedCargo).Visual.position;

            // 2. Отправляем запрос на сервер
            heavy.RequestDropServerRpc(heavy.NetworkObject, visualPos);
        }
    }
    #endregion

    #region Публичные методы: Cargo
    /// <summary>
    /// Назначить груз, который несет Pawn
    /// </summary>
    public void AssignCargo(ICargo cargo)
    {
        if (_currentCarriedCargo == cargo) return;

        _currentCarriedCargo = cargo;
        ColoredDebug.CLog(gameObject,
            $"<color=orange>[CARGO]</color> Назначен груз: {cargo.CargoName}",
            _ColoredDebug);
    }

    /// <summary>
    /// Освободить руки от груза
    /// </summary>
    public void ClearCargo()
    {
        if (_currentCarriedCargo == null) return;

        LookAt(_currentCarriedCargo.Transform.position, 0.25f);

        _currentCarriedCargo = null;

        ColoredDebug.CLog(gameObject,
            "<color=orange>[CARGO]</color> Руки освобождены",
            _ColoredDebug);
    }

    public void SetCurrentGrid(Vector2Int cell)
    {
        сurrentGridCell = cell;
    }

    /*
    public void ShowVisual()
    {
        _visual.SetActive(false);
    }
    
    public void HideVisual()
    {
        _visual.SetActive(true);
    }
    */
    #endregion

    /// <summary>
    /// Поворачивает объект Direction в сторону движения
    /// </summary>
    private void HandleDirectionRotation()
    {
        if (_directionTransform == null || IsInVehicle) return;

        if (!IsSpawned) return;

        // --- ЛОГИКА ВЛАДЕЛЬЦА (Хост или Тот, кто управляет этим павном) ---
        // Мы вычисляем угол и отправляем его в сеть
        if (base.IsOwner)
        {
            float targetAngle = _netEyeRotation.Value; // По умолчанию сохраняем старый
            bool updateNetwork = false;

            // 1. Приоритет: Принудительный взгляд (Interaction System)
            if (_forcedLookTimer > 0 && _forcedLookTarget.HasValue)
            {
                _forcedLookTimer -= Time.deltaTime;
                Vector3 directionToTarget = _forcedLookTarget.Value - transform.position;
                if (directionToTarget.sqrMagnitude > 0.01f)
                {
                    targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
                    updateNetwork = true;
                }
            }
            // 2. Приоритет: Движение
            else
            {
                Vector2 velocity = Vector2.zero;
                if (_aiPath != null && _aiPath.enabled) velocity = _aiPath.velocity;
                else if (_rigidbody != null) velocity = _rigidbody.linearVelocity; // Unity 6

                if (velocity.sqrMagnitude > _rotationVelocityThreshold * _rotationVelocityThreshold)
                {
                    targetAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                    updateNetwork = true;
                }
            }

            // Если угол изменился, обновляем сетевую переменную
            if (updateNetwork)
            {
                // Оптимизация: не спамим в сеть, если угол почти такой же
                if (Mathf.Abs(Mathf.DeltaAngle(_netEyeRotation.Value, targetAngle)) > 0.1f)
                {
                    _netEyeRotation.Value = targetAngle;
                }
            }
        }

        // --- ЛОГИКА ВИЗУАЛИЗАЦИИ (Работает у ВСЕХ: и у Хоста, и у Клиентов) ---
        // Мы берем значение из сетевой переменной и плавно крутим спрайт к нему

        float networkedAngle = _netEyeRotation.Value;

        // -90f offset, т.к. спрайт "Глаз" смотрит вверх (Y+), а 0 градусов это вправо (X+)
        Quaternion targetRotation = Quaternion.Euler(0, 0, networkedAngle - 90f);

        _directionTransform.rotation = Quaternion.Lerp(
            _directionTransform.rotation,
            targetRotation,
            Time.deltaTime * _rotationSpeed
        );
    }

    private void RotateTowardsVector(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // -90f, т.к. спрайт "Глаз" смотрит вверх по умолчанию
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle - 90f);

        _directionTransform.rotation = Quaternion.Lerp(
            _directionTransform.rotation,
            targetRotation,
            Time.deltaTime * _rotationSpeed
        );
    }

    public void PredictivePickup(HeavyObject target)
    {
        if (IsCarryingCargo || IsActionPending) return;

        IsActionPending = true;

        // 1. Предсказываем подбор на клиенте
        InitiatePredictivePickupClient(target);

        // 2. Отправляем запрос на сервер
        target.RequestPickUpServerRpc(NetworkObject, target.NetworkObject);
    }

    private void InitiatePredictivePickupClient(HeavyObject cargo)
    {
        if (cargo == null) return;

        // 1. Помечаем объект как pending
        cargo.InitiatePredictivePickupClient(this);

        // 2. Привязываем визуал к рукам (предсказание)
        Transform visual = cargo.Visual;
        if (visual != null)
        {
            // Сохраняем мировой scale
            Vector3 worldScale = visual.lossyScale;

            visual.SetParent(_holdPoint, true);

            // Возвращаем scale
            visual.localScale = new Vector3(
                worldScale.x / _holdPoint.lossyScale.x,
                worldScale.y / _holdPoint.lossyScale.y,
                worldScale.z / _holdPoint.lossyScale.z
            );

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        // 3. Назначаем груз локально
        AssignCargo(cargo);

        ColoredDebug.CLog(
            gameObject,
            "<color=cyan>[PREDICTION]</color> Pickup pending (object in hands but pending)",
            _ColoredDebug
        );
    }
}