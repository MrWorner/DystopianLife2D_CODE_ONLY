using Sirenix.OdinInspector;
using UnityEngine;


public interface IContainer : IInteractable
{
    bool IsEmpty { get; }
    ItemInfo[] GetContents();
    void OpenContainer(Pawn opener);
}

public class LootContainer : InteractableBase, IContainer
{
    [BoxGroup("Container"), SerializeField]
    private ItemInfo[] _items;

    [BoxGroup("Container"), SerializeField]
    private bool _isLocked = false;

    [BoxGroup("Container"), SerializeField]
    private string _requiredKeyId = "";

    [BoxGroup("Visual"), SerializeField]
    private Sprite _containerIcon;

    [BoxGroup("Visual"), SerializeField]
    private Animator _animator;

    private bool _isOpened = false;

    // IInteractable
    public override Vector3 InteractionPosition => transform.position;
    public override Vector3 Position => transform.position;
    public override Transform Transform => transform;
    public override bool CanInteract => !_isOpened;
    public override int InteractionPriority => 6;

    public override Sprite GetInteractionIcon() => _containerIcon;

    public override string GetInteractionHint()
    {
        if (_isLocked) return "Заперто";
        if (_isOpened) return "Пусто";
        return "Открыть контейнер";
    }

    public override void Interact(Pawn interactor)
    {
        OpenContainer(interactor);
    }

    // IContainer
    public bool IsEmpty => _items == null || _items.Length == 0 || _isOpened;

    public ItemInfo[] GetContents() => _items;

    public void OpenContainer(Pawn opener)
    {
        /*
        if (_isOpened) return;

        if (_isLocked)
        {
            var inventory = opener.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.HasKey(_requiredKeyId))
            {
                ColoredDebug.CLog(gameObject, "<color=red>Контейнер заперт!</color>");
                return;
            }
        }

        _isOpened = true;

        if (_animator != null)
            _animator.SetTrigger("Open");

        // Выдать предметы игроку
        var playerInventory = opener.GetComponent<PlayerInventory>();
        if (playerInventory != null)
        {
            foreach (var item in _items)
            {
                playerInventory.AddItem(item);
               // ColoredDebug.CLog(gameObject,$"<color=yellow>Получен предмет: {item.Name}</color>");
            }
        }
        */
        //ColoredDebug.CLog(gameObject, "<color=lime>Контейнер открыт!</color>");
    }
}