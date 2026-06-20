using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;

public class InteractableHintUI : MonoBehaviour
{
    private static InteractableHintUI _instance;

    public static InteractableHintUI Instance
    {
        get
        {
            if (_instance == null)
            {
                // Если Instance пуст, ищем его на сцене
                // FindFirstObjectByType — современный и быстрый аналог FindObjectOfType
                _instance = Object.FindFirstObjectByType<InteractableHintUI>();

                if (_instance == null)
                {
                    //Debug.LogError("[InteractableHintUI] Объект не найден на сцене! Убедитесь, что он существует.");
                }
            }
            return _instance;
        }
    }

    [Header("Components")]
    [SerializeField] private SpriteRenderer _iconRenderer;
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private float _smoothTime = 0.1f;
    [SerializeField] private Sprite _DefaultSprite;

    private Transform _currentTarget;
    private Vector3 _currentVelocity;
    private bool _isVisible;

    private void Awake()
    {
        // Логика Singleton для предотвращения дублей
        if (_instance == null)
        {
            _instance = this;
            Hide();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Показать подсказку над конкретным объектом
    /// </summary>
    public void Show(IInteractable interactable)
    {
        if (interactable == null) return;

        _currentTarget = interactable.Transform;

        _hintText.text = interactable.GetInteractionHint() + " [F]";
        _iconRenderer.sprite = interactable.GetInteractionIcon();

        if (_iconRenderer.sprite == null)
        {
            _iconRenderer.sprite = _DefaultSprite;
        }

        _isVisible = true;
        if (_iconRenderer != null && _hintText != null)
        {
            _iconRenderer.gameObject.SetActive(true);
            _hintText.gameObject.SetActive(true);
        }

        // Устанавливаем начальную позицию сразу, чтобы не было прыжка
        transform.position = _currentTarget.position;
    }

    /// <summary>
    /// Скрыть подсказку
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        _currentTarget = null;

        if (_iconRenderer != null && _hintText != null)
        {
            _iconRenderer.gameObject.SetActive(false);
            _hintText.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!_isVisible || _currentTarget == null) return;

        Vector3 targetPos = _currentTarget.position;
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _currentVelocity, _smoothTime);
    }
}