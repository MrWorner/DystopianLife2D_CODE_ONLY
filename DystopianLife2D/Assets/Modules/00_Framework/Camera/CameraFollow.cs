// НАЗНАЧЕНИЕ: Умная камера с памятью зума для разных режимов и управлением колесиком мыши.
// ПРИМЕЧАНИЕ: Сохраняет настройки зума отдельно для пешехода и водителя.

using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public enum CameraMode { Walking, Driving }

    #region Поля: Required
    [BoxGroup("REQUIRED"), Required, SerializeField] private Camera _cam;
    #endregion

    #region Поля: Zoom Settings
    [TabGroup("ZOOM"), Title("General")]
    [BoxGroup("General"), SerializeField] private float _zoomSensitivity = 5f;
    [BoxGroup("General"), SerializeField] private float _zoomLerpSpeed = 4f;

    [TabGroup("ZOOM"), Title("Walking Limits")]
    [BoxGroup("Walking"), SerializeField] private float _minWalkZoom = 3f;
    [BoxGroup("Walking"), SerializeField] private float _maxWalkZoom = 8f;

    [TabGroup("ZOOM"), Title("Driving Limits")]
    [BoxGroup("Driving"), SerializeField] private float _minDriveZoom = 6f;
    [BoxGroup("Driving"), SerializeField] private float _maxDriveZoom = 15f;
    #endregion

    #region Поля: Follow Settings
    [TabGroup("FOLLOW"), Title("Walking")]
    [BoxGroup("Walking"), SerializeField] private float _walkSmooth = 5f;

    [TabGroup("FOLLOW"), Title("Driving")]
    [BoxGroup("Driving"), SerializeField] private float _driveSmooth = 3f;
    [BoxGroup("Driving"), SerializeField] private float _lookAheadAmount = 4f;
    [BoxGroup("Driving"), SerializeField] private float _lookAheadSpeed = 2f;
    #endregion

    #region Поля: State (Internal Memory)
    [BoxGroup("STATE"), ReadOnly, SerializeField] private Transform _target;
    [BoxGroup("STATE"), ReadOnly, SerializeField] private CameraMode _currentMode = CameraMode.Walking;

    [BoxGroup("STATE"), Title("Current Zoom Memory")]
    [ReadOnly, SerializeField] private float _currentWalkZoom = 5f;
    [ReadOnly, SerializeField] private float _currentDriveZoom = 10f;
    #endregion

    [BoxGroup("DEBUG"), SerializeField]
    protected bool _ColoredDebug;

    private Vector3 _lookAheadPos;
    private Rigidbody2D _targetRb;

    private static CameraFollow _instance;

    public static CameraFollow Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CameraFollow>();
                if (_instance == null)
                {
                    Debug.LogError("[CameraFollow] Instance not found in the scene.");
                }
            }
            return _instance;
        }
        internal set
        {
            _instance = value;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[CameraFollow] Duplicate instance detected, destroying this component.");
            Destroy(this);
        }
    }

    private void Reset() => _cam = GetComponent<Camera>();

    public void SetTarget(Transform target)
    {
        _target = target;
        if (_target != null) _targetRb = _target.GetComponent<Rigidbody2D>();
    }

    public void SetMode(CameraMode mode)
    {
        _currentMode = mode;
        ColoredDebug.CLog(gameObject, $"<color=cyan>[CAMERA]</color> Режим изменен на: {mode}", _ColoredDebug);
    }

    private void Update()
    {
        HandleManualZoom();
    }

    private void HandleManualZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.01f) return;

        if (_currentMode == CameraMode.Walking)
        {
            _currentWalkZoom -= scroll * _zoomSensitivity;
            _currentWalkZoom = Mathf.Clamp(_currentWalkZoom, _minWalkZoom, _maxWalkZoom);
        }
        else
        {
            _currentDriveZoom -= scroll * _zoomSensitivity;
            _currentDriveZoom = Mathf.Clamp(_currentDriveZoom, _minDriveZoom, _maxDriveZoom);
        }
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // 1. Применяем ЗУМ из соответствующей "памяти"
        float targetZoom = (_currentMode == CameraMode.Walking) ? _currentWalkZoom : _currentDriveZoom;
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetZoom, Time.deltaTime * _zoomLerpSpeed);

        // 2. Вычисляем ПОЗИЦИЮ
        Vector3 finalPosition = _target.position;
        finalPosition.z = -10;

        if (_currentMode == CameraMode.Driving && _targetRb != null)
        {
            // Система Look Ahead (смотрим вперед по движению машины)
            Vector2 localVel = _target.InverseTransformDirection(_targetRb.linearVelocity);
            float targetOffsetY = Mathf.Clamp(localVel.y / 5f, -1f, 1f) * _lookAheadAmount;

            Vector3 offset = _target.up * targetOffsetY;
            _lookAheadPos = Vector3.Lerp(_lookAheadPos, offset, Time.deltaTime * _lookAheadSpeed);
            finalPosition += _lookAheadPos;
        }
        else
        {
            _lookAheadPos = Vector3.zero;
        }

        // 3. Плавное следование
        float smooth = (_currentMode == CameraMode.Walking) ? _walkSmooth : _driveSmooth;
        transform.position = Vector3.Lerp(transform.position, finalPosition, Time.deltaTime * smooth);
    }
}