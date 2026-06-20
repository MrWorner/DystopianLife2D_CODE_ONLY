// ================== PickableItem.cs ================== //
// НАЗНАЧЕНИЕ: Компонент для предметов, которые можно подобрать
// Реализует интерфейс IPickable для взаимодействия с InteractionSystem
// ===================================================== //

using Sirenix.OdinInspector;
using UniRx;
using Unity.Netcode;
using UnityEngine;

public enum ItemType
{
    Trash,      // Мусор
    Food,       // Еда
    Weapon,     // Оружие
    Money,      // Деньги
    Key,        // Ключ
    Document,   // Документ
    Other       // Другое
}

[RequireComponent(typeof(CircleCollider2D))]
public class PickableItem : InteractableBase, IPickable
{
    #region Поля: Settings

    [BoxGroup("Item Settings"), SerializeField]
    private ItemType _itemType = global::ItemType.Trash;

    [BoxGroup("Item Settings"), SerializeField]
    private string _itemName = "Item";

    [BoxGroup("Item Settings"), SerializeField, TextArea]
    private string _description = "Обычный предмет";

    [BoxGroup("Item Settings"), SerializeField]
    private Sprite _itemIcon;

    [BoxGroup("Item Settings"), SerializeField]
    private Sprite _interactionIcon;

    [BoxGroup("Interaction"), SerializeField]
    private int _interactionPriority = 0;

    [BoxGroup("Interaction"), SerializeField]
    private string _interactionHint = "Поднять [F]";

    [BoxGroup("Interaction"), SerializeField]
    private bool _canBePickedUp = true;

    [BoxGroup("Effects"), SerializeField]
    private bool _playPickupSound = true;

    [BoxGroup("Effects"), SerializeField]
    private SoundType _pickupSoundType = SoundType.ButtonClick;

    [BoxGroup("Effects"), SerializeField]
    private bool _destroyOnPickup = true;

    [BoxGroup("Effects"), SerializeField, ShowIf(nameof(_destroyOnPickup))]
    private float _destroyDelay = 0f;

    #endregion

    #region Поля: Components

    private CircleCollider2D _collider;
    private SpriteRenderer _spriteRenderer;

    #endregion

    #region Поля: Debug

    [BoxGroup("DEBUG"), ReadOnly, SerializeField]
    private bool _isPickedUp = false;
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;
    #endregion

    #region Реализация IInteractable

    public override Vector3 Position => transform.position;
    public override Transform Transform => transform;
    public override bool CanInteract => _canBePickedUp && !_isPickedUp;
    public override int InteractionPriority => _interactionPriority;

    public override Sprite GetInteractionIcon()
    {
        return _interactionIcon;
    }

    public override string GetInteractionHint()
    {
        return _interactionHint;
    }

    public override void Interact(Pawn interactor)
    {
        if (!CanInteract) return;

        PickUp(interactor);
    }

    #endregion

    #region Реализация IPickable

    public string ItemType => _itemType.ToString();

    public void PickUp(Pawn picker)
    {
        if (_isPickedUp) return;

        // ✅ НОВОЕ: Отправляем запрос на сервер
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            PickUpServerRpc(picker.GetComponent<NetworkObject>().NetworkObjectId);
        }
        else
        {
            // Локальная версия (для одиночной игры)
            PerformPickUp(picker);
        }
    }

    #endregion

    public override Vector3 InteractionPosition => transform.position;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickUpServerRpc(ulong pickerNetId)
    {
        // Синхронизируем подбор на всех клиентах
        PickUpClientRpc(pickerNetId);
    }

    [ClientRpc]
    private void PickUpClientRpc(ulong pickerNetId)
    {
        // Находим Pawn по NetworkObjectId
        var pickerNetObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[pickerNetId];
        var picker = pickerNetObj.GetComponent<Pawn>();

        if (picker != null)
        {
            PerformPickUp(picker);
        }
    }

    private void PerformPickUp(Pawn picker)
    {
        if (_isPickedUp) return;
        _isPickedUp = true;

        ColoredDebug.CLog(gameObject,
            $"<color=yellow>Предмет подобран: {_itemName} (тип: {_itemType})</color>",
            _ColoredDebug);

        if (_playPickupSound)
        {
            // SoundManager.Instance?.PlaySound(_pickupSoundType);
        }

        OnItemPickedUp(picker);

        if (_destroyOnPickup)
        {
            if (_destroyDelay > 0)
            {
                DisableVisuals();
                Destroy(gameObject, _destroyDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            DisableVisuals();
            _canBePickedUp = false;
        }
    }









    #region Unity Callbacks

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        SetupCollider();
    }

    #endregion

    #region Инициализация

    private void InitializeComponents()
    {
        _collider = GetComponent<CircleCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_collider == null)
        {
            DebugUtils.LogMissingReference(this, nameof(_collider));
        }
    }

    private void SetupCollider()
    {
        if (_collider != null)
        {
            _collider.isTrigger = true;
            // Коллайдер нужен только для обнаружения InteractionSystem'ом
        }
    }

    #endregion

    #region Визуальные эффекты

    private void DisableVisuals()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }

        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }

    #endregion

    #region События подбора

    /// <summary>
    /// Вызывается когда предмет подобран
    /// Переопределите в наследниках для добавления функциональности
    /// </summary>
    protected virtual void OnItemPickedUp(Pawn picker)
    {
        // Здесь можно добавить логику:
        // - Добавление предмета в инвентарь
        // - Изменение состояния игрока
        // - Запуск квестов
        // - И т.д.

        ColoredDebug.CLog(gameObject,
            $"<color=cyan>Игрок {picker.name} подобрал {_itemName}</color>", _ColoredDebug);
    }

    #endregion

    #region Публичные методы

    /// <summary>
    /// Получить информацию о предмете
    /// </summary>
    public ItemInfo GetItemInfo()
    {
        return new ItemInfo
        {
            Name = _itemName,
            Type = _itemType,
            Description = _description,
            Icon = _itemIcon
        };
    }

    /// <summary>
    /// Установить возможность подбора
    /// </summary>
    public void SetCanBePickedUp(bool canPickUp)
    {
        _canBePickedUp = canPickUp;
    }

    #endregion

    #region Editor

    private void OnDrawGizmosSelected()
    {
        if (_collider != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _collider.radius);
        }

        // Отображение типа предмета
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.5f,
            $"{_itemType}: {_itemName}",
            new GUIStyle { normal = new GUIStyleState { textColor = Color.yellow } }
        );
#endif
    }

    [Button("Test Pickup"), BoxGroup("DEBUG")]
    private void TestPickup()
    {
        var player = Pawn.LocalPlayer;
        if (player != null)
        {
            PickUp(player);
        }
        else
        {
            ColoredDebug.CLog(gameObject, "<color=red>Player not found!</color>", _ColoredDebug);
        }
    }
    #endregion
}

// ===================================================== //
// ВСПОМОГАТЕЛЬНЫЕ СТРУКТУРЫ
// ===================================================== //

/// <summary>
/// Информация о предмете для передачи в другие системы
/// </summary>
[System.Serializable]
public struct ItemInfo
{
    public string Name;
    public ItemType Type;
    public string Description;
    public Sprite Icon;
}
