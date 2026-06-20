// ================== InteractionSystem.cs (FIXED) ================== //
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

public class InteractionSystem : MonoBehaviour
{
    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private CircleCollider2D _interactionTrigger;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private InteractionHighlightManager _highlightManager;
    #endregion

    #region Поля: Settings
    [BoxGroup("SETTINGS"), SerializeField, Tooltip("Частота обновления радара (сек)")]
    private float _interactionUpdateInterval = 0.1f;

    [BoxGroup("SETTINGS"), SerializeField]
    private LayerMask _interactableLayer;

    [BoxGroup("SETTINGS"), SerializeField, Tooltip("Дистанция взаимодействия (меньше радиуса радара)")]
    private float _interactionDistance = 2.0f;

    [BoxGroup("SETTINGS"), SerializeField, Tooltip("Игнорировать приоритеты при авто-выборе")]
    private bool _ignorePriority = true;

    [BoxGroup("SETTINGS/Manual Selection"), SerializeField]
    private float _movementResetThreshold = 0.5f;
    #endregion

    #region Поля: Visual Colors
    [BoxGroup("SETTINGS/Colors"), SerializeField]
    private Color _colorReady = Color.green;

    [BoxGroup("SETTINGS/Colors"), SerializeField]
    private Color _colorBlocked = Color.red;

    [BoxGroup("SETTINGS/Colors"), SerializeField]
    private Color _colorOutOfRange = Color.white;

    [BoxGroup("SETTINGS/Colors"), SerializeField]
    private Color _colorDisabled = new Color(0.7f, 0.7f, 0.7f, 0.4f);
    #endregion

    #region Поля: Debug
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private bool _isManualSelectionActive;
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private int _manualSelectedIndex = -1;
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private Vector3 _selectionOriginPosition;
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private List<IInteractable> _nearbyInteractables = new List<IInteractable>();
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private GameObject _closestInteractableObject;
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private bool _isActive;
    [BoxGroup("DEBUG"), ReadOnly, SerializeField] private bool _canInteractWaitClose;
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;
    #endregion

    #region Свойства
    private IInteractable _closestInteractable;
    private readonly CompositeDisposable _disposables = new CompositeDisposable();
    private Pawn _pawn;

    public static InteractionSystem Instance { get; private set; }
    // Публичные свойства для доступа из HighlightManager
    public float InteractionDistance => _interactionDistance;
    public Color ColorDisabled => _colorDisabled;
    #endregion

    #region Unity Методы
    private void Awake()
    {
        if (Instance) DebugUtils.LogInstanceAlreadyExists(this, Instance);
        else Instance = this;

        if (_interactionTrigger == null) DebugUtils.LogMissingReference(this, nameof(_interactionTrigger));
        HideIcon();
    }

    private void OnEnable() => SubscribeToEvents();
    private void OnDisable()
    {
        _disposables.Clear();
        HideIcon();
    }
    private void OnDestroy() => _disposables.Dispose();
    #endregion

    #region Публичные методы
    public void SetInteractionActive(bool active)
    {
        if (active) Activate();
        else Deactivate();
    }

    public void LinkToPawn(Pawn newPawn)
    {
        ResetState();
        _pawn = newPawn;

        if (_pawn != null)
        {
            transform.SetParent(_pawn.transform);
            transform.localPosition = Vector3.zero;
            if (_pawn.IsInVehicle) Deactivate(); else Activate();
        }
        else
        {
            transform.SetParent(null);
            Deactivate();
        }
    }

    public void Activate()
    {
        _isActive = true;
        if (_interactionTrigger != null) _interactionTrigger.enabled = true;
        ColoredDebug.CLog(gameObject, "<color=green>[SYSTEM]</color> Interaction ACTIVATED", _ColoredDebug);
    }

    public void Deactivate()
    {
        _isActive = false;
        if (_interactionTrigger != null) _interactionTrigger.enabled = false;
        ResetState();
        ColoredDebug.CLog(gameObject, "<color=yellow>[SYSTEM]</color> Interaction DEACTIVATED", _ColoredDebug);
    }
    #endregion

    #region Logic & UniRx
    private void SubscribeToEvents()
    {
        _disposables.Clear();

        Observable.Interval(System.TimeSpan.FromSeconds(_interactionUpdateInterval))
            .Where(_ => _isActive)
            .Subscribe(_ => FindNearbyInteractables())
            .AddTo(_disposables);

        this.UpdateAsObservable()
            .Where(_ => _isActive)
            .Subscribe(_ =>
            {
                CheckMovementReset();
                UpdateClosestInteractable();
                UpdateVisuals();
            })
            .AddTo(_disposables);

        this.UpdateAsObservable()
            .Where(_ => _isActive && _nearbyInteractables.Count > 1)
            .Subscribe(_ => HandleSelectionInput())
            .AddTo(_disposables);

        this.UpdateAsObservable()
            .Where(_ => _isActive && Input.GetKeyDown(KeyCode.F))
            .Subscribe(_ => TryInteract())
            .AddTo(_disposables);

        if (_interactionTrigger != null)
        {
            _interactionTrigger.OnTriggerExit2DAsObservable()
                .Subscribe(OnInteractableExited)
                .AddTo(_disposables);
        }
    }

    private void ResetState()
    {
        _nearbyInteractables.Clear();
        _closestInteractable = null;
        _closestInteractableObject = null;
        _canInteractWaitClose = false;
        _isManualSelectionActive = false;
        _manualSelectedIndex = -1;
        HideIcon();
        // Передаем transform.position при сбросе
        if (_highlightManager != null) _highlightManager.UpdateHighlights(_nearbyInteractables, null, Color.white, transform.position);
    }
    #endregion

    #region Selection Logic
    private void CheckMovementReset()
    {
        if (!_isManualSelectionActive) return;
        if (Vector3.Distance(transform.position, _selectionOriginPosition) > _movementResetThreshold)
        {
            _isManualSelectionActive = false;
            _manualSelectedIndex = -1;
        }
    }

    private void HandleSelectionInput()
    {
        bool next = Input.GetKeyDown(KeyCode.E);
        bool prev = Input.GetKeyDown(KeyCode.Q);

        if (!next && !prev) return;

        // 1. Получаем список только досягаемых предметов
        List<IInteractable> reachableItems = _nearbyInteractables.FindAll(item =>
            item != null && Vector3.Distance(transform.position, item.InteractionPosition) <= _interactionDistance);

        if (reachableItems.Count <= 1 && !_isManualSelectionActive) return;

        // 2. Определяем текущий индекс внутри СПИСКА ДОСЯГАЕМЫХ (а не общего списка)
        int currentReachableIndex = -1;
        if (_closestInteractable != null)
        {
            currentReachableIndex = reachableItems.IndexOf(_closestInteractable);
        }

        // Если текущий выбранный предмет недосягаем или не выбран, берем первый из доступных
        if (currentReachableIndex == -1)
        {
            currentReachableIndex = 0;
            _isManualSelectionActive = true;
            _selectionOriginPosition = transform.position;
        }
        else if (!_isManualSelectionActive)
        {
            // Активируем ручной режим, если еще не активен
            _isManualSelectionActive = true;
            _selectionOriginPosition = transform.position;
        }

        // 3. Листаем (безопасно)
        if (reachableItems.Count > 0)
        {
            if (next)
            {
                currentReachableIndex++;
                if (currentReachableIndex >= reachableItems.Count) currentReachableIndex = 0;
            }
            else if (prev)
            {
                currentReachableIndex--;
                if (currentReachableIndex < 0) currentReachableIndex = reachableItems.Count - 1;
            }

            // 4. Применяем выбор
            _closestInteractable = reachableItems[currentReachableIndex];

            // Сохраняем глобальный индекс просто для отладки
            _manualSelectedIndex = _nearbyInteractables.IndexOf(_closestInteractable);

            _pawn.LookAt(_closestInteractable.Transform.position, 0.25f);
        }
    }

    private void FindNearbyInteractables()
    {
        float r = _interactionTrigger != null ? _interactionTrigger.radius : 5.0f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, r, _interactableLayer);
        List<IInteractable> currentHits = new List<IInteractable>();

        foreach (var hit in hits)
        {
            if (TryGetInteractable(hit, out var interactable))
            {
                currentHits.Add(interactable);
                if (!_nearbyInteractables.Contains(interactable))
                    _nearbyInteractables.Add(interactable);
            }
        }

        for (int i = _nearbyInteractables.Count - 1; i >= 0; i--)
        {
            var item = _nearbyInteractables[i];
            bool isNull = item == null || item.Transform == null;
            if (isNull || !currentHits.Contains(item)) _nearbyInteractables.RemoveAt(i);
        }
    }

    private void UpdateClosestInteractable()
    {
        if (_nearbyInteractables.Count == 0)
        {
            _closestInteractable = null;
            _closestInteractableObject = null;
            return;
        }

        IInteractable target = null;

        if (_isManualSelectionActive)
        {
            // Проверяем валидность индекса ручного выбора
            if (_manualSelectedIndex >= _nearbyInteractables.Count || _manualSelectedIndex < 0)
            {
                _isManualSelectionActive = false;
                _manualSelectedIndex = -1;
            }
            else
            {
                target = _nearbyInteractables[_manualSelectedIndex];

                if (Vector3.Distance(transform.position, target.InteractionPosition) > _interactionDistance)
                {
                    _isManualSelectionActive = false;
                    _manualSelectedIndex = -1;
                    target = null;
                }
            }
        }

        if (!_isManualSelectionActive)
        {
            float minDistance = float.MaxValue;
            foreach (var item in _nearbyInteractables)
            {
                if (item == null || !item.CanInteract) continue;

                float dist = Vector3.Distance(transform.position, item.InteractionPosition);
                if (dist <= _interactionDistance && dist < minDistance)
                {
                    minDistance = dist;
                    target = item;
                }
            }

            if (target == null)
            {
                float minRadarDist = float.MaxValue;
                foreach (var item in _nearbyInteractables)
                {
                    float dist = Vector3.Distance(transform.position, item.InteractionPosition);
                    if (dist < minRadarDist)
                    {
                        minRadarDist = dist;
                        target = item;
                    }
                }
            }
        }

        _closestInteractable = target;
        _closestInteractableObject = _closestInteractable?.Transform.gameObject;

        _canInteractWaitClose = false;
        if (_closestInteractable != null)
        {
            float dist = Vector3.Distance(transform.position, _closestInteractable.InteractionPosition);
            if (dist <= _interactionDistance) _canInteractWaitClose = true;
        }
    }

    private void UpdateVisuals()
    {
        // Логика цвета только для ТЕКУЩЕГО (Фокусного) предмета
        Color targetStateColor = _colorDisabled;

        if (_closestInteractable != null)
        {
            float dist = Vector3.Distance(transform.position, _closestInteractable.InteractionPosition);
            bool inRange = dist <= _interactionDistance;

            if (inRange)
            {
                // Если готов - Зеленый, если заблокирован - Красный
                if (_closestInteractable.CanInteract)
                    targetStateColor = _colorReady;
                else
                    targetStateColor = _colorBlocked;
            }
            else
            {
                // Если выбран (вручную), но далеко - все равно серый
                targetStateColor = _colorDisabled;
            }
        }

        // Мы передаем targetStateColor. Менеджер применит его ТОЛЬКО к target (_closestInteractable).
        // Для остальных предметов Менеджер сам возьмет Желтый или Серый цвета.
        _highlightManager.UpdateHighlights(_nearbyInteractables, _closestInteractable, targetStateColor, transform.position);

        // UI логика (F button prompt)
        if (_closestInteractable != null && targetStateColor == _colorReady)
            ShowIconForInteractable(_closestInteractable);
        else
            HideIcon();
    }
    #endregion

    #region Helpers
    private bool TryGetInteractable(Collider2D col, out IInteractable result)
    {
        result = null;
        if (col == null) return false;
        if (col.TryGetComponent(out result)) return true;
        if (col.transform.parent != null && col.transform.parent.TryGetComponent(out result)) return true;
        return false;
    }

    private void OnInteractableExited(Collider2D other)
    {
        if (TryGetInteractable(other, out var interactable))
        {
            if (_nearbyInteractables.Contains(interactable))
                _nearbyInteractables.Remove(interactable);
        }
    }

    private void TryInteract()
    {
        if (_closestInteractable == null || !_canInteractWaitClose || !_closestInteractable.CanInteract || _pawn == null) return;

        ColoredDebug.CLog(gameObject, "<color=lime>[ACTION]</color> Interacting with {0}", _ColoredDebug, _closestInteractableObject.name);

        if (_closestInteractableObject != null)
        {
            // Поворачиваемся к объекту на 1.5 секунды (во время действия)
            _pawn.LookAt(_closestInteractableObject.transform.position, 0.25f);
        }

        _closestInteractable.Interact(_pawn);
    }

    private void ShowIconForInteractable(IInteractable interactable)
    {
        if (InteractableHintUI.Instance) InteractableHintUI.Instance.Show(interactable);
    }

    private void HideIcon()
    {
        if (InteractableHintUI.Instance) InteractableHintUI.Instance.Hide();
    }
    #endregion

    #region Editor
    private void OnDrawGizmosSelected()
    {
        if (_interactionTrigger != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _interactionTrigger.radius);
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionDistance);
    }
    #endregion
}