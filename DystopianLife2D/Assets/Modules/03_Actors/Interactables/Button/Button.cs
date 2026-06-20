using Sirenix.OdinInspector;
using UnityEngine;


public interface IActivatable : IInteractable
{
    bool IsActivated { get; }
    void Activate(Pawn activator);
    void Deactivate();
}

public class Button : InteractableBase, IActivatable
{
    [BoxGroup("Button Settings"), SerializeField]
    private bool _isActivated = false;

    [BoxGroup("Button Settings"), SerializeField]
    private bool _canBeDeactivated = true;

    [BoxGroup("Button Settings"), SerializeField]
    private float _autoDeactivateTime = 0f; // 0 = не деактивируется

    [BoxGroup("Visual"), SerializeField]
    private SpriteRenderer _buttonSprite;

    [BoxGroup("Visual"), SerializeField]
    private Color _normalColor = Color.white;

    [BoxGroup("Visual"), SerializeField]
    private Color _activatedColor = Color.green;

    [BoxGroup("Effects"), SerializeField]
    private GameObject[] _objectsToActivate;

    private float _deactivateTimer = 0f;

    // IInteractable
    public override Vector3 Position => transform.position;
    public override Transform Transform => transform;
    public override bool CanInteract => !_isActivated || _canBeDeactivated;
    public override int InteractionPriority => 5;
    public override Vector3 InteractionPosition => transform.position;
    public override Sprite GetInteractionIcon() => null;

    public override string GetInteractionHint()
    {
        return _isActivated ? "Деактивировать [E]" : "Активировать [E]";
    }

    public override void Interact(Pawn interactor)
    {
        if (_isActivated && _canBeDeactivated)
        {
            Deactivate();
        }
        else if (!_isActivated)
        {
            Activate(interactor);
        }
    }

    // IActivatable
    public bool IsActivated => _isActivated;

    public void Activate(Pawn activator)
    {
        _isActivated = true;

        if (_buttonSprite != null)
            _buttonSprite.color = _activatedColor;

        // Активировать связанные объекты
        foreach (var obj in _objectsToActivate)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        //ColoredDebug.CLog(gameObject, "<color=green>Кнопка активирована!</color>");

        if (_autoDeactivateTime > 0f)
        {
            _deactivateTimer = _autoDeactivateTime;
        }
    }

    public void Deactivate()
    {
        _isActivated = false;

        if (_buttonSprite != null)
            _buttonSprite.color = _normalColor;

        // Деактивировать связанные объекты
        foreach (var obj in _objectsToActivate)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        //ColoredDebug.CLog(gameObject, "<color=yellow>Кнопка деактивирована</color>");
    }

    private void Update()
    {
        if (_isActivated && _autoDeactivateTime > 0f)
        {
            _deactivateTimer -= Time.deltaTime;
            if (_deactivateTimer <= 0f)
            {
                Deactivate();
            }
        }
    }
}