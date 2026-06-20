
/// <summary>
/// Интерфейс для транспорта, в который можно сесть
/// </summary>
public interface IEnterable
{
    /// <summary>Есть ли свободные места</summary>
    bool HasAvailableSeats { get; }

    /// <summary>Войти в транспорт</summary>
    void Enter(Pawn pawn);
}