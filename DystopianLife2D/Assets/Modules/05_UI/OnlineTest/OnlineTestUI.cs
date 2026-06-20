using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class OnlineTestUI : MonoBehaviour
{
    public static OnlineTestUI Instance { get; private set; } // Добавляем Instance

    public bool AutoHostOnStart = false;
    public Image hostButtonImage;
    public Image clientButtonImage;

    private bool _isStarted = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (AutoHostOnStart)
        {
            StartCoroutine(AutoConnectRoutine());
        }
    }

    // Метод для ПЕРЕПОДКЛЮЧЕНИЯ
    public void ReconnectClient()
    {
        Debug.Log("<color=red>[Network]</color> Переподключение: сброс сети...");
        NetworkManager.Singleton.Shutdown();
        StopAllCoroutines();
        _isStarted = false;

        // Через секунду пробуем зайти как клиент снова
        StartCoroutine(DelayedClientStart());
    }

    private IEnumerator DelayedClientStart()
    {
        yield return new WaitForSeconds(1.5f);
        StartClientButtonClick();
    }

    private IEnumerator AutoConnectRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        NetworkManager.Singleton.OnTransportFailure += HandleTransportFailure;
        Debug.Log("<color=orange>[Network]</color> Попытка запуска Хоста...");

        if (NetworkManager.Singleton.StartHost())
        {
            OnMultiplayerStarted(true);
            yield return new WaitForSeconds(0.1f);
        }
        /*
        else
        {
            StartClientButtonClick();
        }
        */
    }

    private void HandleTransportFailure()
    {
        NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
        NetworkManager.Singleton.Shutdown();
        StartClientButtonClick();
    }

    public void StartHostButtonClick()
    {
        if (_isStarted) return;
        if (NetworkManager.Singleton.StartHost()) OnMultiplayerStarted(true);
    }

    public void StartClientButtonClick()
    {
        if (_isStarted) return;

        // Если включен авто-хост, значит мы в режиме тестирования "на лету"
        // и нам нужна задержка, чтобы второй экземпляр (клиент) не обгонял первый (сервер)
        if (AutoHostOnStart)
        {
            StartCoroutine(DelayedClientStartRoutine());
        }
        else
        {
            // Обычный мгновенный запуск (если нажали кнопку вручную)
            ExecuteClientStart();
        }
    }

    private IEnumerator DelayedClientStartRoutine()
    {
        Debug.Log("<color=cyan>[Network]</color> AutoHostOnStart активен. Ждем 2 секунды перед подключением клиента...");

        // Визуально можно заблокировать кнопки или показать лоадер здесь
        yield return new WaitForSeconds(2.0f);

        Debug.Log("<color=cyan>[Network]</color> Время вышло. Подключаемся...");
        ExecuteClientStart();
    }

    private void ExecuteClientStart()
    {
        // Проверяем еще раз, не успел ли кто-то запустить сеть, пока мы ждали
        if (_isStarted) return;

        if (NetworkManager.Singleton.StartClient())
        {
            OnMultiplayerStarted(false);
        }
        else
        {
            Debug.LogError("[Network] Не удалось запустить клиент!");
            _isStarted = false; // Сбрасываем флаг, если запуск провалился
        }
    }

    private void OnMultiplayerStarted(bool isHost)
    {
        _isStarted = true;
        if (hostButtonImage) hostButtonImage.color = isHost ? Color.green : Color.grey;
        if (clientButtonImage) clientButtonImage.color = !isHost ? Color.green : Color.grey;

        // Запускаем менеджер владения
        PossessionManager.Instance.StartWithDelay();
    }
}