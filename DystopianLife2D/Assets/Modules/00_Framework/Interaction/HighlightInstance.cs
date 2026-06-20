using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// Перечисление состояний для удобства
public enum HighlightVisualState
{
    Hidden,
    OutOfRange, // Далеко: Кружок, Серый, Без анимации
    Reachable,  // Рядом: Кружок, Желтый, Анимация
    Focused     // В фокусе: Бублик, Зеленый/Красный, Скейл (без пульсации)
}

public class HighlightInstance : MonoBehaviour
{
    [BoxGroup("Required"), Required, SerializeField]
    private SpriteRenderer _renderer;

    [BoxGroup("Animation"), SerializeField]
    private float _pulseScale = 1.2f;
    [BoxGroup("Animation"), SerializeField]
    private float _pulseDuration = 0.6f;

    private Tween _pulseTween;
    private HighlightVisualState _currentState;

    public void UpdateState(HighlightVisualState state, InteractableHighlightSettings settings, Color overrideColor)
    {
        // Если состояние и цвет не изменились, выходим (оптимизация)
        if (_currentState == state && _renderer.color == overrideColor) return;

        _currentState = state;

        // Сброс твинов
        _pulseTween?.Kill();
        transform.localScale = Vector3.one;

        switch (state)
        {
            case HighlightVisualState.Hidden:
                gameObject.SetActive(false);
                break;

            case HighlightVisualState.OutOfRange:
                gameObject.SetActive(true);
                // Спрайт: Кружок
                _renderer.sprite = settings.DefaultNearbySprite;
                // Цвет: Серый/Прозрачный (берем из overrideColor)
                _renderer.color = overrideColor;
                // Анимация: Нет
                break;

            case HighlightVisualState.Reachable:
                gameObject.SetActive(true);
                // Спрайт: Кружок
                _renderer.sprite = settings.DefaultNearbySprite;
                // Цвет: Желтый
                _renderer.color = settings.ReachableColor;
                // Анимация: Пульсация
                StartPulseAnimation();
                break;

            case HighlightVisualState.Focused:
                gameObject.SetActive(true);
                // Спрайт: Бублик (Action Icon)
                _renderer.sprite = settings.DefaultActionIcon;
                // Цвет: Зеленый или Красный (передается из системы)
                _renderer.color = overrideColor;
                // Анимация: Небольшое увеличение, чтобы выделить выбор
                transform.localScale = Vector3.one * 1.15f;
                break;
        }
    }

    private void StartPulseAnimation()
    {
        _pulseTween = transform.DOScale(_pulseScale, _pulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }

    private void OnDisable()
    {
        _pulseTween?.Kill();
    }
}