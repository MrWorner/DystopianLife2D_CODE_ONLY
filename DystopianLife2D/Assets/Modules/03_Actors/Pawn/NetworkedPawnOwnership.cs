// Файл: NetworkedPawnOwnership.cs
using Pathfinding;
using Pathfinding.RVO;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class NetworkedPawnOwnership : NetworkBehaviour
{
    // ИЗМЕНЕНИЕ 1: Инициализируем ulong.MaxValue, чтобы 0 (Хост) считался игроком
    public readonly NetworkVariable<ulong> PawnOwnerId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private ulong CurrentOwner => PawnOwnerId.Value;

    // ИЗМЕНЕНИЕ 2: Бот - это когда владелец MaxValue
    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool IsOwnedByBot => PawnOwnerId.Value == ulong.MaxValue;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool IsOwnedByMe
    {
        get
        {
            if (!IsClient || NetworkManager.Singleton == null) return false;
            return PawnOwnerId.Value == NetworkManager.Singleton.LocalClientId;
        }
    }

    private Pawn _pawn;
    private NetworkObject _netObject; // Кэш

    private void Awake()
    {

        _pawn = GetComponent<Pawn>();
        _netObject = GetComponent<NetworkObject>();
    }

    public override void OnNetworkSpawn()
    {
        /*
        if (IsServer && IsOwner) // Если это Хост и он владеет объектом
        {
            // Явно записываем ID хоста, чтобы клиенты видели его как занятого
            PawnOwnerId.Value = NetworkManager.Singleton.LocalClientId;
        }
        */

        PawnOwnerId.OnValueChanged += OnOwnerChanged;
        ApplyOwnershipState(PawnOwnerId.Value);
    }

    public override void OnNetworkDespawn()
    {
        PawnOwnerId.OnValueChanged -= OnOwnerChanged;
    }

    public void RequestOwnership()
    {
        if (!IsClient || NetworkManager.Singleton == null) return;
        RequestOwnershipServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void ReleaseOwnership()
    {
        if (!IsClient || NetworkManager.Singleton == null) return;
        ReleaseOwnershipServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    // ИЗМЕНЕНИЕ 3: Проверка на занятость с учетом нового ID
    public bool IsOccupiedByPlayer()
    {
        return PawnOwnerId.Value != ulong.MaxValue;
    }

    public bool IsOccupiedBy(ulong clientId)
    {
        return PawnOwnerId.Value == clientId;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestOwnershipServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        // Если уже занято кем-то другим (не MaxValue и не этот же клиент)
        if (PawnOwnerId.Value != ulong.MaxValue && PawnOwnerId.Value != clientId)
        {
            Debug.LogWarning($"[Ownership] Pawn {name} уже занят клиентом {PawnOwnerId.Value}");
            return;
        }

        PawnOwnerId.Value = clientId;

        if (_netObject.OwnerClientId != clientId)
        {
            _netObject.ChangeOwnership(clientId);
        }

        Debug.Log($"[Ownership] Pawn {name} теперь принадлежит клиенту {clientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleaseOwnershipServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        if (PawnOwnerId.Value == clientId)
        {
            PawnOwnerId.Value = ulong.MaxValue; // Сброс на "Бота"

            if (_netObject.OwnerClientId != NetworkManager.ServerClientId)
            {
                _netObject.RemoveOwnership();
            }

            Debug.Log($"[Ownership] Pawn {name} освобожден клиентом {clientId}");
        }
    }

    private void OnOwnerChanged(ulong oldOwner, ulong newOwner)
    {
        ApplyOwnershipState(newOwner);
    }

    private void ApplyOwnershipState(ulong ownerId)
    {
        if (_pawn == null) return;
        if (NetworkManager.Singleton == null) return;

        ulong myClientId = NetworkManager.Singleton.LocalClientId;

        // ИЗМЕНЕНИЕ 6: Логика сравнения
        bool isOccupied = ownerId != ulong.MaxValue;
        bool isLocalPlayer = isOccupied && (ownerId == myClientId);
        bool isBot = !isOccupied; // Если не занят игроком, значит бот

        _pawn.PlayerController.enabled = false;
        if (_pawn.BotController != null)
        {
            _pawn.BotController.enabled = false;
        }

        _pawn.AIPath.enabled = isBot;
        _pawn.Seeker.enabled = isBot;
        _pawn.RVOController.enabled = isBot;
        _pawn.AIDestinationSetter.enabled = isBot;

        StartCoroutine(ApplyControllersNextFrame(isLocalPlayer, isBot));
    }

    private System.Collections.IEnumerator ApplyControllersNextFrame(bool isLocalPlayer, bool isBot)
    {
        yield return null;
        if (_pawn == null) yield break;

        _pawn.PlayerController.enabled = isLocalPlayer;

        if (_pawn.BotController != null)
        {
            // Бот включается только на Сервере, если это Бот
            _pawn.BotController.enabled = isBot && IsServer;
        }

        if (isLocalPlayer) _pawn.SetAsLocalPlayer();
        else _pawn.RemoveLocalPlayerStatus();

        // Debug.Log($"[Ownership] Pawn {name}: Owner={_ownerClientId.Value}, IsLocalPlayer={isLocalPlayer}, IsBot={isBot}");
    }
}