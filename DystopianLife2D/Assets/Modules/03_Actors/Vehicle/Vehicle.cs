// НАЗНАЧЕНИЕ: Хаб автомобиля (аналог Pawn для человека).
// ЗАВИСИМОСТИ: CarController, Rigidbody2D, PossessionManager, IEnterable

using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UniRx;

public class Vehicle : MonoBehaviour
{
    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private CarController _carController;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Rigidbody2D _rb;
    #endregion

    #region Поля
    [BoxGroup("SETTINGS"), SerializeField]
    private List<VehicleSeat> _seats = new();

    [BoxGroup("DEBUG"), SerializeField]
    protected bool _ColoredDebug = false;
    #endregion

    #region Свойства
    public List<VehicleSeat> Seats => _seats;

    public bool HasAvailableSeats => _seats.Any(seat => !seat.IsOccupied);

    public CarController CarController { get => _carController;}
    #endregion

    #region Unity Методы
    private void Awake()
    {
        // Проверка обязательных ссылок (Required)
        if (_carController == null)
            DebugUtils.LogMissingReference(this, nameof(_carController));

        if (_rb == null)
            DebugUtils.LogMissingReference(this, nameof(_rb));

        // Начальное состояние
        if (_carController) _carController.enabled = false;

        ColoredDebug.CLog(gameObject, "<color=cyan>[INFO]</color> Vehicle инициализирован.", _ColoredDebug);
    }
    #endregion

    public int GetAvailableSeatsCount()
    {
        int count = 0;
        foreach (var seat in Seats)
        {
            if (!seat.IsOccupied)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Получить первое свободное место
    /// </summary>
    public VehicleSeat GetFirstAvailableSeat()
    {
        foreach (var seat in Seats)
        {
            if (!seat.IsOccupied)
                return seat;
        }
        return null;
    }

    public VehicleSeat GetDriverSeat()
    {
        foreach (var seat in Seats)
        {
            if (seat.Type == VehicleSeat.SeatType.Driver)
                return seat;
        }
        return null;
    }
}
