
/// <summary>
/// Интерфейс для предметов, которые можно подобрать
/// </summary>
public interface IPickable : IInteractable
{
    /// <summary>Тип предмета</summary>
    string ItemType { get; }

    /// <summary>Подобрать предмет</summary>
    void PickUp(Pawn picker);
}