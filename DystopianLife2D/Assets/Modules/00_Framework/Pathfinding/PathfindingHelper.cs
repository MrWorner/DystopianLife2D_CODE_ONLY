using Pathfinding;
using UnityEngine;

public static class PathfindingHelper
{
   public static void RegeneradeNavMesh()
   {
        // Это заставит систему пересчитать все препятствия в реальном времени, после генерации карты
        AstarPath.active.Scan();
    }

    public static void RefreshNavMesh(Collider2D newObstruction)
    {
        var guo = new GraphUpdateObject(newObstruction.bounds);
        AstarPath.active.UpdateGraphs(guo);
    }

    public static void ClearNavMeshAfterDestruction(Bounds bounds)
    {
        // Мы передаем Bounds, потому что самого объекта уже может не быть в памяти
        var guo = new GraphUpdateObject(bounds);

        // По умолчанию GUO пересчитывает область, проверяя наличие коллизий.
        // Если коллизии нет — путь открыт.
        AstarPath.active.UpdateGraphs(guo);
    }

    /* ПРИМЕР ИСПОЛЬЗОВАНИЯ В КОДЕ ОБЪЕКТА
    public void OnWallDestroyed()
    {
        Bounds b = GetComponent<BoxCollider2D>().bounds; // Берем границы
        Destroy(gameObject); // Удаляем стену
        PathfindingHelper.ClearNavMeshAfterDestruction(b); // Обновляем карту
    }
    */
}
