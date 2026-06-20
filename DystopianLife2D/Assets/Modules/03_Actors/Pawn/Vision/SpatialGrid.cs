using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

// НАЗНАЧЕНИЕ: Визуализация и управление пространственной сеткой.
// ЗАВИСИМОСТИ: Pawn, ColoredDebug.
// ПРИМЕЧАНИЕ: Отрисовка Gizmos помогает отладить распределение объектов по ячейкам. 

public class SpatialGrid : MonoBehaviour
{
    private static SpatialGrid _instance;

    #region Поля
    [BoxGroup("SETTINGS"), SerializeField] private float _cellSize = 10f;

    [BoxGroup("DEBUG"), SerializeField, ReadOnly]
    private Dictionary<Vector2Int, List<Pawn>> _grid = new();

    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;

    [BoxGroup("DEBUG Visuals"), SerializeField] private bool _drawGrid = true;
    [BoxGroup("DEBUG Visuals"), SerializeField] private bool _showOccupiedOnly = true;
    #endregion

    #region Свойства
    public static SpatialGrid Instance => _instance;
    public float CellSize => _cellSize;
    #endregion

    #region Unity Методы
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            DebugUtils.LogInstanceAlreadyExists(this, _instance);
        }
        else
        {
            _instance = this;
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGrid) return;

        // Визуализация сетки для отладки
        Gizmos.color = Color.white;
        Vector3 pos = transform.position;

        // Определяем границы видимости для отрисовки (например, 10x10 ячеек)
        int range = 10;
        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector2Int cell = WorldToGrid(pos) + new Vector2Int(x, y);
                bool isOccupied = _grid.ContainsKey(cell);

                if (_showOccupiedOnly && !isOccupied) continue;

                Gizmos.color = isOccupied ? Color.green : new Color(1, 1, 1, 0.1f);
                Vector3 cellCenter = new Vector3(cell.x * _cellSize + _cellSize * 0.5f, cell.y * _cellSize + _cellSize * 0.5f, 0);
                Gizmos.DrawWireCube(cellCenter, new Vector3(_cellSize, _cellSize, 0.1f));

                if (isOccupied)
                {
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(cellCenter, $"Pawns: {_grid[cell].Count}");
#endif
                }
            }
        }
    }
    #endregion

    #region Публичные методы
    public void AddPawn(Pawn pawn)
    {
        Vector2Int cell = WorldToGrid(pawn.transform.position);

        if (!_grid.TryGetValue(cell, out var list))
        {
            list = new List<Pawn>();
            _grid[cell] = list;
        }

        list.Add(pawn);
        pawn.SetCurrentGrid(cell);

        ColoredDebug.CLog(gameObject, "<color=cyan>[SYSTEM]</color> Pawn {0} зарегистрирован в сетке", _ColoredDebug, pawn.name);
    }

    public void UpdatePawnCell(Pawn pawn)
    {
        Vector2Int newCell = WorldToGrid(pawn.transform.position);

        if (newCell == pawn.СurrentGridCell)
            return;

        if (_grid.TryGetValue(pawn.СurrentGridCell, out var oldList))
        {
            oldList.Remove(pawn);
            if (oldList.Count == 0) _grid.Remove(pawn.СurrentGridCell);
        }

        if (!_grid.TryGetValue(newCell, out var newList))
        {
            newList = new List<Pawn>();
            _grid[newCell] = newList;
        }

        newList.Add(pawn);
        pawn.SetCurrentGrid(newCell);
    }

    public List<Pawn> GetNearbyPawns(Vector3 position, float radius)
    {
        List<Pawn> nearby = new List<Pawn>();
        Vector2Int centerCell = WorldToGrid(position);
        int cellRadius = Mathf.CeilToInt(radius / _cellSize);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                Vector2Int cell = centerCell + new Vector2Int(x, y);
                if (_grid.TryGetValue(cell, out var list))
                {
                    nearby.AddRange(list);
                }
            }
        }
        return nearby;
    }
    #endregion

    #region Личные методы
    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x / _cellSize), Mathf.FloorToInt(worldPos.y / _cellSize));
    }
    #endregion
}