using DG.Tweening; // Не забудьте импортировать DOTween
using Unity.Netcode;
using UnityEngine;

public abstract class InteractableBase : NetworkBehaviour, IInteractable
{
    #region Данные для HighlightSystem
    [Header("Interaction Visuals")]
    [SerializeField] protected Sprite nearbyPulseSprite;  // Спрайт для пульсации (опционально)
    [SerializeField] protected Sprite actionIconSprite;   // Иконка действия (руль, рука и т.д.)
    [SerializeField] protected Color interactionColor = Color.yellow; // Цвет типа действия
    #endregion

    // Реализация свойств интерфейса (Data Only)
    public virtual bool CanInteract => true;
    public virtual Vector3 Position => transform.position;
    public virtual Transform Transform => transform;

    // Спрайты для новой системы
    public Sprite InteractionIcon => actionIconSprite;
    public Sprite NearbySprite => nearbyPulseSprite;
    public virtual Color HighlightColor => interactionColor;

    public abstract Vector3 InteractionPosition { get; }
    public abstract int InteractionPriority { get; }

    // Метод взаимодействия
    public abstract void Interact(Pawn actor);

    // Старые методы для совместимости (можно оставить пустыми или пометить Obsolete)
    /*
    public virtual void SetHighlight(HighlightState state)
    {
        // Логика перенесена в InteractionHighlightManager
    }
    */

    public abstract Sprite GetInteractionIcon(); // Для UI
    public abstract string GetInteractionHint(); // Для UI
}