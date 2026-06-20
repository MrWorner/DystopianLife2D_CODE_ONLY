// НАЗНАЧЕНИЕ: Управление вводом и перемещением игрока (Гибридная модель)
// ЗАВИСИМОСТИ: Pawn, Rigidbody2D, ClientNetworkTransform
// ПРИМЕЧАНИЕ: Движение на клиенте (Owner Auth) + Валидация на сервере

using Sirenix.OdinInspector;
using UniRx;
using UniRx.Triggers;
using Unity.Netcode;
using UnityEngine;

public class LocalPlayerController : NetworkBehaviour
{
    private static LocalPlayerController _instance;

    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Rigidbody2D _rigidbody;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Pawn _pawn;
    #endregion

    #region Поля: Settings (Validation)
    [BoxGroup("Validation"), SerializeField, Tooltip("Допустимая погрешность скорости (для компенсации лагов)")]
    private float _speedTolerance = 1.2f; // +20% к скорости допустимо

    [BoxGroup("Validation"), SerializeField, Tooltip("Дистанция, после которой сервер вернет игрока назад")]
    private float _snapDistance = 2.0f;
    #endregion

    #region Поля: Debug
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug = true;
    #endregion

    #region Личные поля
    private readonly CompositeDisposable _disposables = new();
    private Vector2 _currentInput;
    private Vector3 _lastServerValidPosition;

    public Pawn Pawn { get => _pawn; }
    //public static Pawn CurrentPlayerPawn { get => _instance._pawn; }
    #endregion

    #region Unity Методы
    private void Awake()
    {
        _instance = this;
        if (_rigidbody == null) DebugUtils.LogMissingReference(this, nameof(_rigidbody));
        if (_pawn == null) DebugUtils.LogMissingReference(this, nameof(_pawn));
    }

    public override void OnNetworkSpawn()
    {
        _disposables.Clear();

        // Инициализация позиции для валидации
        _lastServerValidPosition = transform.position;

        // 1. ВВОД (Только Владелец)
        this.UpdateAsObservable()
            .Where(_ => IsOwner)
            .Subscribe(_ => HandleInput())
            .AddTo(_disposables);

        // 2. ФИЗИКА ПЕРЕМЕЩЕНИЯ (Только Владелец)
        // Мы двигаем себя сами, ClientNetworkTransform синхронизирует это остальным
        this.FixedUpdateAsObservable()
            .Where(_ => IsOwner)
            .Subscribe(_ => MoveLocally())
            .AddTo(_disposables);
    }

    private new void OnDestroy()
    {
        base.OnDestroy();
        _disposables.Dispose();
    }
    #endregion

    #region Client Logic (Prediction)
    private void HandleInput()
    {
        if (Pawn.LocalPlayer == null || Pawn.LocalPlayer.PlayerController != this) return;

        // 1. Полная блокировка (смерть, арест, оглушение)
        if (_pawn.IsBusy)
        {
            _currentInput = Vector2.zero;
            return;
        }

        // 2. Бросок предмета (Проверяем IsActionPending ЗДЕСЬ)
        // Если мы УЖЕ ждем ответа от сервера (IsActionPending == true), то кнопку G игнорируем
        if (Input.GetKeyDown(KeyCode.G) && _pawn.IsCarryingCargo && !_pawn.IsActionPending)
        {
            _pawn.PredictiveDrop();
        }

        // 3. Движение (Работает всегда, даже если IsActionPending == true)
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        _currentInput = new Vector2(x, y).normalized;
    }

    private void MoveLocally()
    {
        if (_pawn.IsBusy)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        // Мгновенное применение скорости (Prediction)
        float currentSpeed = _pawn.MoveSpeed;
        _rigidbody.linearVelocity = _currentInput * currentSpeed;
    }
    #endregion
}