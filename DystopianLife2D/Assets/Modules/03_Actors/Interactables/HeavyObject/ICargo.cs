// ================== ICargo.cs ================== //
// НАЗНАЧЕНИЕ: Интерфейс для любых объектов, которые можно переносить как груз
// Применим к ящикам, бочкам, раненым, телевизорам и т.д.
// ================================================ //

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Интерфейс для грузов, которые Pawn может нести и размещать в транспорте
/// </summary>
public interface ICargo
{
    /// <summary>Название предмета для отображения в UI</summary>
    string CargoName { get; }

    /// <summary>Transform объекта груза</summary>
    Transform Transform { get; }

    /// <summary>NetworkObject для сетевой синхронизации</summary>
    NetworkObject NetworkObject { get; }

    /// <summary>Текущее состояние груза</summary>
    CargoState CurrentState { get; }

    /// <summary>Кто несет груз (если CargoState.Carried)</summary>
    Pawn CurrentCarrier { get; }

    /// <summary>В какой машине находится (если CargoState.InVehicle)</summary>
    VehicleSeat CurrentSeat { get; }

    /// <summary>Поднять груз (вызывается только на сервере)</summary>
    void PickUp_Server(Pawn picker);

    /// <summary>Бросить груз с импульсом (вызывается только на сервере)</summary>
    void Drop_Server(Vector3 targetPosition);

    /// <summary>Поместить в транспорт (вызывается только на сервере)</summary>
    void PlaceInVehicle_Server(VehicleSeat seat);

    /// <summary>Забрать из транспорта (вызывается только на сервере)</summary>
    void TakeFromVehicle_Server(Pawn taker, Transform holdPoint);
}

/// <summary>
/// Состояния груза
/// </summary>
public enum CargoState
{
    OnGround,   // Лежит на земле
    Carried,    // В руках у Pawn
    InVehicle   // В транспорте
}
