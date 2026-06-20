using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    private static ScreenFader _instance;

    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField] private CanvasGroup _loadingCanvasGroup;
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField] private Transform _hourglassIconTransform;
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField] private TextMeshProUGUI _loadingText;
    #endregion

    #region Поля
    [BoxGroup("SETTINGS"), SerializeField, Range(0.1f, 3.0f)] private float _fadeDuration = 0.5f;
    [BoxGroup("SETTINGS"), SerializeField, Range(0.1f, 2.0f)] private float _rotationDuration = 0.5f;
    [BoxGroup("SETTINGS"), SerializeField, Range(0.0f, 1.0f)] private float _rotationPause = 0.1f;
    [BoxGroup("DEBUG"), SerializeField, ReadOnly] private Sequence _hourglassSequence;
    [BoxGroup("DEBUG"), SerializeField] protected bool _coloredDebug;
    #endregion

    #region Свойства
    public static ScreenFader Instance { get => _instance; }
    #endregion

    #region Методы UNITY
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            ///DebugUtils.LogInstanceAlreadyExists(this);
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (_loadingCanvasGroup == null) DebugUtils.LogMissingReference(this, nameof(_loadingCanvasGroup));
        if (_hourglassIconTransform == null) DebugUtils.LogMissingReference(this, nameof(_hourglassIconTransform));
        if (_loadingText == null) DebugUtils.LogMissingReference(this, nameof(_loadingText));

        DOTween.Init();

        if (_loadingCanvasGroup != null)
        {
            _loadingCanvasGroup.alpha = 0f;
            _loadingCanvasGroup.interactable = false;
            _loadingCanvasGroup.blocksRaycasts = false;
            _loadingCanvasGroup.gameObject.SetActive(true);
        }
    }
    #endregion

    #region Публичные методы
    [Button]
    public void ShowLoadingScreen()
    {
        ShowLoadingScreen(_fadeDuration);
    }

    public void ShowLoadingScreen(float duration)
    {
        if (_loadingCanvasGroup == null) return;
        ColoredDebug.CLog(gameObject, "<color=cyan>ScreenFader:</color> Плавно показываю экран загрузки за <color=yellow>{0}</color> сек.", _coloredDebug, duration);
        _hourglassIconTransform.rotation = new Quaternion();
        _loadingCanvasGroup.DOKill();
        _loadingCanvasGroup.interactable = true;
        _loadingCanvasGroup.blocksRaycasts = true;
        _loadingCanvasGroup.DOFade(1f, duration).OnComplete(StartHourglassAnimation);
    }

    [Button]
    public void HideLoadingScreen()
    {
        HideLoadingScreen(_fadeDuration);
    }

    public void HideLoadingScreen(float duration)
    {
        if (_loadingCanvasGroup == null) return;
        ColoredDebug.CLog(gameObject, "<color=cyan>ScreenFader:</color> Плавно скрываю экран загрузки за <color=yellow>{0}</color> сек.", _coloredDebug, duration);
        StopHourglassAnimation();
        _loadingCanvasGroup.DOKill();
        _loadingCanvasGroup.interactable = false;
        _loadingCanvasGroup.blocksRaycasts = false;
        _loadingCanvasGroup.DOFade(0f, duration);
    }
    #endregion

    #region Личные методы
    private void StartHourglassAnimation()
    {
        if (_hourglassIconTransform == null) return;
        ColoredDebug.CLog(gameObject, "<color=cyan>ScreenFader:</color> Запускаю анимацию песочных часов. Длительность: <color=yellow>{0}</color>, Пауза: <color=yellow>{1}</color>.", _coloredDebug, _rotationDuration, _rotationPause);
        StopHourglassAnimation();
        _hourglassIconTransform.localRotation = Quaternion.identity;
        _hourglassSequence = DOTween.Sequence();
        _hourglassSequence.Append(_hourglassIconTransform.DOLocalRotate(new Vector3(0, 0, -180), _rotationDuration, RotateMode.LocalAxisAdd).SetEase(Ease.Linear))
            .AppendInterval(_rotationPause)
            .Append(_hourglassIconTransform.DOLocalRotate(new Vector3(0, 0, -180), _rotationDuration, RotateMode.LocalAxisAdd).SetEase(Ease.Linear))
            .AppendInterval(_rotationPause)
            .SetLoops(-1, LoopType.Restart);
    }

    private void StopHourglassAnimation()
    {
        if (_hourglassSequence == null || !_hourglassSequence.IsActive()) return;
        ColoredDebug.CLog(gameObject, "<color=cyan>ScreenFader:</color> Останавливаю анимацию песочных часов.", _coloredDebug);
        _hourglassSequence?.Kill();
        _hourglassIconTransform?.DOKill();
    }
    #endregion
}