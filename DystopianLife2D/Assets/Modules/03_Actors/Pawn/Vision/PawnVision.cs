using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using UniRx;
using Unity.Netcode; // Добавлено для NetworkBehaviour

// НАЗНАЧЕНИЕ: Обнаружение вражеских или союзных Pawn. Работает ТОЛЬКО на сервере.
public class PawnVision : NetworkBehaviour
{
    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Pawn _pawn;
    #endregion

    #region Поля
    [BoxGroup("SETTINGS Vision"), SerializeField] private float _viewRadius = 15f;
    [BoxGroup("SETTINGS Vision"), SerializeField] private float _viewAngle = 120f;
    [BoxGroup("SETTINGS Vision"), SerializeField] private LayerMask _obstacleMask;

    [BoxGroup("SETTINGS Optimization"), SerializeField] private float _senseInterval = 0.25f;

    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private float _cosHalfFov;
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;

    [BoxGroup("DEBUG Gizmos"), SerializeField] private bool _drawGizmos = false;
    [BoxGroup("DEBUG Gizmos"), SerializeField] private Color _visibleColor = Color.green;
    [BoxGroup("DEBUG Gizmos"), SerializeField] private Color _blockedColor = Color.red;
    [BoxGroup("DEBUG Gizmos"), SerializeField] private bool _drawCheckedCells = false;
    [BoxGroup("DEBUG Gizmos"), SerializeField] private Color _checkedCellColor = new Color(1f, 1f, 0f, 0.25f);
    #endregion

    private readonly CompositeDisposable _disposables = new();

    #region Unity & Network Методы
    private void Awake()
    {
        if (_pawn == null) DebugUtils.LogMissingReference(this, nameof(_pawn));
        _cosHalfFov = Mathf.Cos(_viewAngle * 0.5f * Mathf.Deg2Rad);
    }

    public override void OnNetworkSpawn()
    {
        // ГЛАВНОЕ ИЗМЕНЕНИЕ 1: Блокируем логику на клиенте
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // Запуск сенсора только на сервере
        Observable.Interval(TimeSpan.FromSeconds(_senseInterval))
            .Subscribe(_ => Sense())
            .AddTo(_disposables);

        ColoredDebug.CLog(gameObject, "<color=lime>[VISION]</color> Система зрения активирована (Server)", _ColoredDebug);
    }

    public override void OnNetworkDespawn()
    {
        _disposables.Clear();
        base.OnNetworkDespawn();
    }

    // OnDestroy нужен для очистки, если объект удаляется не через NetworkDespawn
    private void OnDestroy()
    {
        _disposables.Dispose();
    }
    #endregion

    #region Личные методы
    private void Sense()
    {
        if (!IsSpawned) return;

        List<Pawn> candidates = SpatialGrid.Instance.GetNearbyPawns(transform.position, _viewRadius);
        Vector2 myPos = transform.position;

        // Получаем направление взгляда на основе угла глаз, а не движения
        Vector2 forward = GetEyeDirection();

        foreach (Pawn target in candidates)
        {
            if (target == _pawn) continue;

            if (TryDetectTarget(target, myPos, forward))
            {
                OnTargetSeen(target);
            }
        }
    }

    private bool TryDetectTarget(Pawn target, Vector2 myPos, Vector2 forward)
    {
        Vector2 dir = (Vector2)target.transform.position - myPos;
        float sqrDist = dir.sqrMagnitude;

        if (sqrDist > _viewRadius * _viewRadius) return false;

        float dot = Vector2.Dot(forward, dir.normalized);
        if (dot < _cosHalfFov) return false;

        if (Physics2D.Raycast(myPos, dir.normalized, Mathf.Sqrt(sqrDist), _obstacleMask))
            return false;

        //
        // ColoredDebug.CLog(gameObject, "<color=cyan>[INFO]</color> Цель {0} обнаружена", _ColoredDebug, target.name); 
        return true;
    }

    private void OnTargetSeen(Pawn target)
    {
        // Здесь логика реакции (стрельба, погоня и т.д.)
        ColoredDebug.CLog(gameObject, "<color=lime>[ACTION]</color> Вижу: {0}", _ColoredDebug, target.name);
    }

    /// <summary>
    /// ГЛАВНОЕ ИЗМЕНЕНИЕ 2: Расчет вектора на основе угла глаз (_netEyeRotation)
    /// </summary>
    private Vector2 GetEyeDirection()
    {
        // Берем угол из Pawn (он синхронизирован по сети)
        float angleDeg = _pawn.EyeRotationAngle;
        float angleRad = angleDeg * Mathf.Deg2Rad;

        // Превращаем угол обратно в вектор (0 градусов = Vector2.right)
        return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos || _pawn == null) return;

        // Gizmos рисуем локально, поэтому используем ту же логику направления
        Vector2 forward = (_pawn.NetworkObject != null && _pawn.NetworkObject.IsSpawned)
            ? GetEyeDirection() // Если заспавнен - берем реальный угол
            : (Vector2)transform.up; // Если в префабе - просто вверх

        DrawViewCone(forward);

        // Остальные методы (DrawCheckedCells) можно оставить как есть или убрать для краткости
        if (_drawCheckedCells && SpatialGrid.Instance != null) DrawCheckedCells();
    }

    private void DrawViewCone(Vector2 forward)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _viewRadius);

        Vector2 leftBoundary = RotateVector(forward, -_viewAngle * 0.5f);
        Vector2 rightBoundary = RotateVector(forward, _viewAngle * 0.5f);

        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(leftBoundary * _viewRadius));
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(rightBoundary * _viewRadius));
    }

    private void DrawCheckedCells()
    {
        // (Код из вашего оригинала для отрисовки сетки...)
        // Оставил сокращенным для читаемости, скопируйте из старого файла, если нужно
        if (SpatialGrid.Instance == null) return;
        Vector2Int centerCell = new Vector2Int(Mathf.FloorToInt(transform.position.x / SpatialGrid.Instance.CellSize), Mathf.FloorToInt(transform.position.y / SpatialGrid.Instance.CellSize));
        int cellRadius = Mathf.CeilToInt(_viewRadius / SpatialGrid.Instance.CellSize);
        Gizmos.color = _checkedCellColor;
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                Vector3 cellCenter = new Vector3((centerCell.x + x) * SpatialGrid.Instance.CellSize + SpatialGrid.Instance.CellSize * 0.5f, (centerCell.y + y) * SpatialGrid.Instance.CellSize + SpatialGrid.Instance.CellSize * 0.5f, 0);
                Gizmos.DrawWireCube(cellCenter, new Vector3(SpatialGrid.Instance.CellSize, SpatialGrid.Instance.CellSize, 0.01f));
            }
        }
    }

    private Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
    }
    #endregion
}