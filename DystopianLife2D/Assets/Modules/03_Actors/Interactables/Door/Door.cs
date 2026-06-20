using Sirenix.OdinInspector;
using UnityEngine;

public interface IDoor : IInteractable
{
    bool IsLocked { get; }
    bool IsOpen { get; }
    void Open(Pawn opener);
    void Close();
    void Lock();
    void Unlock(string keyId);
}

public class Door : InteractableBase, IDoor
{
    [BoxGroup("Door Settings"), SerializeField]
    private bool _isLocked = false;

    [BoxGroup("Door Settings"), SerializeField]
    private bool _isOpen = false;

    [BoxGroup("Door Settings"), SerializeField]
    private string _requiredKeyId = "";

    [BoxGroup("Door Settings"), SerializeField]
    private Sprite _doorIcon;

    [BoxGroup("Animation"), SerializeField]
    private Animator _animator;

    [BoxGroup("Sounds"), SerializeField]
    private SoundType _openSound = SoundType.ButtonClick;

    [BoxGroup("Sounds"), SerializeField]
    private SoundType _lockedSound = SoundType.ButtonClick;

    // IInteractable
    public override Vector3 Position => transform.position;
    public override Transform Transform => transform;
    public override bool CanInteract => !_isOpen;
    public override int InteractionPriority => 5;

    public override Sprite GetInteractionIcon() => _doorIcon;

    public override string GetInteractionHint()
    {
        if (_isOpen) return "Закрыть [E]";
        if (_isLocked) return $"Заперто (нужен ключ)";
        return "Открыть [E]";
    }

    public override void Interact(Pawn interactor)
    {
        if (_isOpen)
        {
            Close();
        }
        else if (_isLocked)
        {
            /*
            // Проверить есть ли у игрока ключ
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory != null && inventory.HasKey(_requiredKeyId))
            {
                Unlock(_requiredKeyId);
                Open(interactor);
            }
            else
            {
                ColoredDebug.CLog(gameObject, "<color=red>Дверь заперта!</color>");
                SoundManager.Instance?.PlaySound(_lockedSound);
            }
            */
        }
        else
        {
            Open(interactor);
        }
    }

    // IDoor
    public bool IsLocked => _isLocked;
    public bool IsOpen => _isOpen;

    public override Vector3 InteractionPosition => transform.position;

    public void Open(Pawn opener)
    {
        if (_isLocked) return;

        _isOpen = true;
        //ColoredDebug.CLog(gameObject, "<color=green>Дверь открыта!</color>");

        if (_animator != null)
            _animator.SetTrigger("Open");

        //SoundManager.Instance?.PlaySound(_openSound);
    }

    public void Close()
    {
        _isOpen = false;
        //ColoredDebug.CLog(gameObject, "<color=yellow>Дверь закрыта</color>");

        if (_animator != null)
            _animator.SetTrigger("Close");
    }

    public void Lock()
    {
        _isLocked = true;
        _isOpen = false;
    }

    public void Unlock(string keyId)
    {
        if (keyId == _requiredKeyId)
        {
            _isLocked = false;
            //ColoredDebug.CLog(gameObject, "<color=lime>Дверь разблокирована!</color>");
        }
    }
}
