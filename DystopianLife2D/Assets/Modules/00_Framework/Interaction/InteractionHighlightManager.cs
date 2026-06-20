using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UniRx;
using UniRx.Triggers;

[System.Serializable]
public class InteractableHighlightSettings
{
    [PreviewField(50)] public Sprite DefaultNearbySprite; // Кружок
    [PreviewField(50)] public Sprite DefaultActionIcon;   // Бублик
    public Color ReachableColor = Color.yellow;           // Цвет для "досягаемых, но не в фокусе"
}

public class InteractionHighlightManager : MonoBehaviour
{
    [BoxGroup("Required"), Required, SerializeField]
    private HighlightInstance _prefab;

    [BoxGroup("SETTINGS"), SerializeField]
    private InteractableHighlightSettings _settings;

    [BoxGroup("SETTINGS"), SerializeField]
    private int _initialPoolSize = 5;

    private Dictionary<IInteractable, HighlightInstance> _activeHighlights = new Dictionary<IInteractable, HighlightInstance>();
    private Stack<HighlightInstance> _pool = new Stack<HighlightInstance>();
    private CompositeDisposable _disposables = new CompositeDisposable();
    private List<IInteractable> _keysToRemove = new List<IInteractable>(); // Кэшируем список, чтобы не создавать каждый кадр

    private void Awake()
    {
        InitializePool();
        SubscribeToUpdates();
    }

    private void OnDestroy() => _disposables.Dispose();

    public void UpdateHighlights(List<IInteractable> allNearby, IInteractable target, Color targetStateColor, Vector3 playerPos)
    {
        // 1. Скрываем те, что вышли из зоны радара
        List<IInteractable> toRemove = new List<IInteractable>();
        foreach (var kvp in _activeHighlights)
        {
            if (!allNearby.Contains(kvp.Key)) toRemove.Add(kvp.Key);
        }
        foreach (var item in toRemove) ReturnToPool(item);

        // 2. Обновляем визуальное состояние для каждого объекта в радиусе
        foreach (var item in allNearby)
        {
            if (item == null) continue;

            if (!_activeHighlights.TryGetValue(item, out HighlightInstance instance))
            {
                instance = GetFromPool();
                _activeHighlights.Add(item, instance);
            }

            float dist = Vector3.Distance(playerPos, item.InteractionPosition);
            bool isReachable = dist <= InteractionSystem.Instance.InteractionDistance;
            bool isTarget = (item == target);

            HighlightVisualState state;
            Color finalColor;

            // --- ЛОГИКА ПРИОРИТЕТОВ ---

            // ПРОВЕРКА 1: Объект в фокусе И мы до него дотягиваемся?
            if (isTarget && isReachable)
            {
                state = HighlightVisualState.Focused; // Бублик
                finalColor = targetStateColor;       // Зеленый/Красный
            }
            // ПРОВЕРКА 2: Мы просто дотягиваемся до объекта (но он не в фокусе)?
            else if (isReachable)
            {
                state = HighlightVisualState.Reachable; // Кружок + Анимация
                finalColor = _settings.ReachableColor;   // Желтый
            }
            // ПРОВЕРКА 3: Объект слишком далеко
            else
            {
                state = HighlightVisualState.OutOfRange; // Кружок + Статика
                finalColor = InteractionSystem.Instance.ColorDisabled; // Серый/Прозрачный
            }

            instance.SetPosition(item.InteractionPosition);
            instance.UpdateState(state, _settings, finalColor);
        }
    }

    #region Pool Logic
    private void InitializePool()
    {
        for (int i = 0; i < _initialPoolSize; i++) _pool.Push(CreateNewInstance(false));
    }

    private HighlightInstance CreateNewInstance(bool isActive)
    {
        var instance = Instantiate(_prefab, transform);
        instance.gameObject.SetActive(isActive);
        return instance;
    }

    private HighlightInstance GetFromPool()
    {
        var instance = (_pool.Count > 0) ? _pool.Pop() : CreateNewInstance(true);
        instance.gameObject.SetActive(true);
        return instance;
    }

    private void ReturnToPool(IInteractable key)
    {
        if (_activeHighlights.TryGetValue(key, out var instance))
        {
            instance.gameObject.SetActive(false);
            _pool.Push(instance);
            _activeHighlights.Remove(key);
        }
    }
    #endregion

    private void SubscribeToUpdates()
    {
        this.LateUpdateAsObservable().Subscribe(_ => SyncPositions()).AddTo(_disposables);
    }

    private void SyncPositions()
    {
        _keysToRemove.Clear();

        foreach (var kvp in _activeHighlights)
        {
            // 1. Правильная проверка интерфейса на "живость" в Unity
            var unityObj = kvp.Key as UnityEngine.Object;

            if (unityObj == null)
            {
                // Объект уничтожен — помечаем на удаление
                _keysToRemove.Add(kvp.Key);
                continue;
            }

            // 2. Безопасный доступ к позиции
            try
            {
                kvp.Value.SetPosition(kvp.Key.Position);
            }
            catch (UnityEngine.MissingReferenceException)
            {
                _keysToRemove.Add(kvp.Key);
            }
        }

        // 3. Удаляем мертвые объекты после завершения итерации словаря
        foreach (var key in _keysToRemove)
        {
            // Мы не можем вернуть в пул через ReturnToPool, 
            // так как объект-ключ уже мертв. Просто чистим словарь и визуализацию.
            if (_activeHighlights.TryGetValue(key, out var instance))
            {
                if (instance != null) instance.gameObject.SetActive(false);
                _pool.Push(instance);
            }
            _activeHighlights.Remove(key);
        }
    }
}