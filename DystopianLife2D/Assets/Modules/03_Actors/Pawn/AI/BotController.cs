using Sirenix.OdinInspector;
using UniRx;
using Unity.Netcode;
using UnityEngine;
using Pathfinding;

public class BotController : NetworkBehaviour
{
    [SerializeField] private Pawn _pawn;
    private readonly CompositeDisposable _aiDisposables = new();

    #region Поля: Settings
    [BoxGroup("SETTINGS"), SerializeField] private float _wanderRadius = 10f;
    [BoxGroup("SETTINGS"), SerializeField] private float _idleDuration = 4f;
    // Если true, бот будет возвращаться к месту спавна. 
    // Если false, будет гулять там, где его оставил игрок.
    [BoxGroup("SETTINGS"), SerializeField] private bool _returnToSpawn = false;
    #endregion

    private Vector2 _centerPosition;

    private void Awake()
    {
        if (_pawn == null) _pawn = GetComponent<Pawn>();
    }

    private void OnEnable()
    {
        // Если менеджера нет или мы не сервер — выключаем скрипт немедленно
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            enabled = false;
            return;
        }

        // Если мы хотим, чтобы бот гулял там, где его бросили — обновляем центр
        if (!_returnToSpawn)
        {
            _centerPosition = transform.position;
        }

        //Debug.Log($"<color=lime>[AI-ACTIVATE]</color> Бот {name} запущен на сервере.");

        _pawn.SyncSpeeds();
        StartWandering();
    }

    // OnDisable срабатывает, когда в павна вселяется игрок
    private void OnDisable()
    {
        _aiDisposables.Clear(); // Остановка таймеров UniRx

        if (_pawn != null && _pawn.AIPath != null)
        {
            // Сбрасываем путь, чтобы бот не пытался куда-то идти, пока им рулит игрок
            _pawn.AIPath.destination = transform.position;
            _pawn.AIPath.canMove = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // Запоминаем точку самого первого спавна
        _centerPosition = transform.position;
    }

    private void StartWandering()
    {
        _aiDisposables.Clear();

        // Запускаем цикл выбора цели
        Observable.Interval(System.TimeSpan.FromSeconds(_idleDuration))
            .Subscribe(_ =>
            {
                if (_pawn.AIPath != null && _pawn.AIPath.enabled)
                {
                    PickNewTarget();
                }
            })
            .AddTo(_aiDisposables);

        // Выбираем первую цель сразу при включении, не дожидаясь интервала
        PickNewTarget();
    }

    private void PickNewTarget()
    {
        Vector2 randomPoint = _centerPosition + (Random.insideUnitCircle * _wanderRadius);

        if (_pawn.AIPath != null)
        {
            _pawn.AIPath.canMove = true;
            _pawn.AIPath.destination = randomPoint;

            //Debug.Log($"<color=yellow>[AI]</color> Бот {name} направляется к {randomPoint}");
        }
    }
}