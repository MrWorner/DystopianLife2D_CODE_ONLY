using UnityEngine;

public enum HighlightState
{
    None,       // Не подсвечивается
    Nearby,     // Просто находится рядом (обычная подсветка)
    Closest     // Самый близкий (особая подсветка)
}

/// <summary>
/// Базовый интерфейс для всех объектов, с которыми можно взаимодействовать
/// </summary>
public interface IInteractable
{
    /// <summary>Позиция объекта в мире</summary>
    Vector3 Position { get; }

    /// <summary>Transform объекта</summary>
    Transform Transform { get; }

    /// <summary>Можно ли сейчас взаимодействовать с объектом</summary>
    bool CanInteract { get; }
    // Точка, до которой считаем дистанцию
    Vector3 InteractionPosition { get; }
    /// <summary>Иконка для отображения (спрайт кнопки взаимодействия)</summary>
    Sprite GetInteractionIcon();

    /// <summary>Текст подсказки (опционально)</summary>
    string GetInteractionHint();

    /// <summary>Приоритет взаимодействия (чем больше - тем важнее)</summary>
    int InteractionPriority { get; }

    /// <summary>Выполнить взаимодействие</summary>
    void Interact(Pawn interactor);

    //void SetHighlight(HighlightState state);

    Sprite InteractionIcon { get; } // Иконка действия
    Sprite NearbySprite { get; }    // Спрайт пульсации
    Color HighlightColor { get; }   // Цвет (Красный/Желтый/Оранжевый)
}