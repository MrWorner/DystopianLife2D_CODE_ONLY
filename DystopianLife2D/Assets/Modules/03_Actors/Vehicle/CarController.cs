// НАЗНАЧЕНИЕ: Управление физикой автомобиля (2D) с синхронизацией через Netcode
// ЗАВИСИМОСТИ: Rigidbody2D, VehicleSeat, Pawn
// ПРИМЕЧАНИЕ: Использует Client-Authoritative подход для владельца

using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UniRx;
using UniRx.Triggers;

[RequireComponent(typeof(Rigidbody2D))]
public class CarController : NetworkBehaviour
{
    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Rigidbody2D _rb;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private VehicleSeat _driverSeat;
    #endregion

    #region Поля: Settings
    [BoxGroup("SETTINGS/Movement"), SerializeField] private float _acceleration = 45f;
    [BoxGroup("SETTINGS/Movement"), SerializeField] private float _steeringSpeed = 250f;
    [BoxGroup("SETTINGS/Movement"), Range(0, 1), SerializeField] private float _driftFactor = 0.9f;
    [BoxGroup("SETTINGS/Movement"), SerializeField] private float _linearDrag = 1.5f;
    [BoxGroup("SETTINGS/Movement"), Range(0.1f, 1f), SerializeField] private float _reverseSpeedFactor = 0.4f;
    [BoxGroup("SETTINGS/Movement"), SerializeField] private float _minSpeedForSteer = 3f;
    #endregion

    #region Поля: State
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private float _currentSpeed;
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;

    private readonly NetworkVariable<Vector2> _netMoveInput = new(writePerm: NetworkVariableWritePermission.Server);
    private Vector2 _localInput;
    private readonly CompositeDisposable _disposables = new();
    #endregion

    #region Unity Методы
    private void Awake()
    {
        if (_rb == null)
            Debug.LogError($"<color=red>[ERROR]</color> {name}: _rb is missing!");

        if (_driverSeat == null)
            Debug.LogError($"<color=red>[ERROR]</color> {name}: _driverSeat is missing!");
    }

    private void OnDestroy()
    {
        _disposables.Dispose(); // Согласно правилам UniRx 
    }
    #endregion

    public override void OnNetworkSpawn()
    {
        _disposables.Clear();

        // 1. Читаем ввод (Только если мы ВЛАДЕЛЕЦ машины)
        this.UpdateAsObservable()
            .Where(_ => IsOwner && _driverSeat.IsOccupied)
            .Subscribe(_ => HandleInput())
            .AddTo(_disposables);

        // 2. Физика на ВЛАДЕЛЬЦЕ (Client-Authoritative)
        this.FixedUpdateAsObservable()
            .Where(_ => IsOwner)
            .Subscribe(_ => ApplyPhysics())
            .AddTo(_disposables);

        ColoredDebug.CLog(gameObject, "<color=cyan>[INFO]</color> CarController spawned. Owner: {0}", _ColoredDebug, IsOwner);
    }

    public override void OnNetworkDespawn()
    {
        _disposables.Clear();
        base.OnNetworkDespawn();
    }

    #region Личные методы
    private void HandleInput()
    {
        if (NetworkManager.Singleton == null) return;

        // Проверка, является ли этот конкретный Pawn - активным игроком
        if (!IsOwner || Pawn.LocalPlayer == null || Pawn.LocalPlayer.CurrentSeat != _driverSeat)
        {
            return;
        }

        _localInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // Синхронизация для визуальных эффектов (колеса, фары)
        SubmitInputServerRpc(_localInput);
    }

    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 input, ServerRpcParams rpcParams = default)
    {
        _netMoveInput.Value = input;
    }

    private void ApplyPhysics()
    {
        if (_rb == null) return;

        _currentSpeed = _rb.linearVelocity.magnitude;
        Vector2 moveInput = IsOwner ? _localInput : _netMoveInput.Value;

        // Определяем фактическое направление движения относительно переда машины [исправление]
        float forwardVelocityDot = Vector2.Dot(_rb.linearVelocity, transform.up);

        // --- 1. ТРЕНИЕ (Drift) ---
        Vector2 forwardVelocity = transform.up * forwardVelocityDot;
        Vector2 rightVelocity = transform.right * Vector2.Dot(_rb.linearVelocity, transform.right);
        _rb.linearVelocity = forwardVelocity + rightVelocity * _driftFactor;

        // --- 2. ГАЗ / ТОРМОЗ ---
        if (Mathf.Abs(moveInput.y) > 0.01f)
        {
            float force = moveInput.y * _acceleration;
            if (moveInput.y < 0) force *= _reverseSpeedFactor;
            _rb.AddForce(transform.up * force);
        }

        // --- 3. ПОВОРОТ ---
        float steerEfficiency = Mathf.Clamp01(_currentSpeed / _minSpeedForSteer);

        // Позволяем немного поворачивать на очень низкой скорости для маневренности
        if (Mathf.Abs(moveInput.y) > 0.1f && steerEfficiency < 0.2f) steerEfficiency = 0.2f;

        if (_currentSpeed > 0.1f || Mathf.Abs(moveInput.y) > 0.1f)
        {
            float rotationAmount = -moveInput.x * _steeringSpeed * steerEfficiency * Time.fixedDeltaTime;

            // ИСПРАВЛЕНИЕ: Инвертируем поворот, если машина ФАКТИЧЕСКИ едет назад (dot < 0)
            // Или если игрок зажал "Назад" при почти полной остановке
            bool isPhysicallyMovingBack = forwardVelocityDot < -0.1f;
            bool isIntendingToMoveBack = moveInput.y < -0.1f && _currentSpeed < 0.5f;

            if (isPhysicallyMovingBack || isIntendingToMoveBack)
            {
                rotationAmount = -rotationAmount;
            }

            _rb.MoveRotation(_rb.rotation + rotationAmount);
        }

        _rb.linearDamping = _linearDrag;
    }
    #endregion
}