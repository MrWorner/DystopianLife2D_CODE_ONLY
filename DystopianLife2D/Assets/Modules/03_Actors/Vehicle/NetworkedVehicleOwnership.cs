using Unity.Netcode;
using UnityEngine;
using Sirenix.OdinInspector;

// Этот класс управляет тем, КТО является хозяином машины в сети.
public class NetworkedVehicleOwnership : NetworkBehaviour
{
    private NetworkObject _netObject;

    private void Awake()
    {
        _netObject = GetComponent<NetworkObject>();
    }

    /// <summary>
    /// Вызывается (обычно из VehicleSeat) когда игрок садится за руль.
    /// Передает права владения (Ownership) этому клиенту.
    /// </summary>
    public void SetDriver(ulong clientId)
    {
        // Смена владельца возможна ТОЛЬКО на сервере
        if (!IsServer) return;

        // Если у машины уже есть владелец и это не сервер, сначала забираем права
        if (_netObject.OwnerClientId != NetworkManager.ServerClientId && _netObject.OwnerClientId != clientId)
        {
            Debug.Log("<color=green>IT WORKS! netObject.RemoveOwnership();</color>", this);
            _netObject.RemoveOwnership();
        }

        // Передаем владение водителю
        _netObject.ChangeOwnership(clientId);
        Debug.Log($"[VehicleOwnership] Права на машину переданы клиенту {clientId}");
    }

    /// <summary>
    /// Вызывается когда водитель выходит. Возвращает права серверу.
    /// </summary>
    public void RemoveDriver()
    {
        if (!IsServer) return;

        // Возвращаем владение серверу (делаем объект нейтральным)
        _netObject.RemoveOwnership();
        Debug.Log("[VehicleOwnership] Права на машину возвращены Серверу");
    }
}