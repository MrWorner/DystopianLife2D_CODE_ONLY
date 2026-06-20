using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

public enum GameScene
{
    LoadBoost,
    MainMenu,
    GameScene
}

public class SceneLoader : MonoBehaviour
{
    #region Поля
    //[BoxGroup("DEBUG"), SerializeField, ReadOnly] private GameScene _sceneToLoad;
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;

    #endregion

    #region Свойства
    private static SceneLoader _instance;
    public static SceneLoader Instance => _instance;

    //public GameScene SceneToLoad { get => _sceneToLoad; }
    #endregion

    #region Методы UNITY
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            ColoredDebug.CLog(gameObject, "<color=#FF6347>SceneLoader:</color> Найден дубликат. Уничтожаю лишний экземпляр <color=yellow>{0}</color>.", _ColoredDebug, name);
            Destroy(gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            ColoredDebug.CLog(gameObject, "<color=lime>SceneLoader:</color> Успешная инициализация синглтона.", _ColoredDebug);
        }
    }
    #endregion

    #region Публичные методы
    /// <summary>
    /// Загружает указанную сцену.
    /// </summary>
    /// <param name="newScene">Сцена для загрузки.</param>
    public void LoadNextScene(GameScene newScene)
    {
        // ИЗМЕНЕНО: Метод теперь принимает GameScene вместо string.
        // Больше нет необходимости изменять поле _sceneToLoad.
        StartCoroutine(LoadSceneAndFade(newScene));
    }
    #endregion

    #region Личные методы
    // ИЗМЕНЕНО: Корутина теперь принимает GameScene и сама получает имя сцены.
    private IEnumerator LoadSceneAndFade(GameScene scene)
    {
        string sceneName = GetSceneName(scene); // Получаем строковое имя сцены из enum.

        ColoredDebug.CLog(gameObject, "<color=cyan>SceneLoader:</color> Корутина <color=yellow>LoadSceneAndFade</color> запущена для сцены <color=yellow>{0}</color>.", _ColoredDebug, sceneName);
        if (ScreenFader.Instance != null)
        {
            ColoredDebug.CLog(gameObject, "<color=cyan>SceneLoader:</color> Показываю экран загрузки.", _ColoredDebug);
            ScreenFader.Instance.ShowLoadingScreen();
            //yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(1f);
        }

        ColoredDebug.CLog(gameObject, "<color=cyan>SceneLoader:</color> Начинаю асинхронную загрузку сцены <color=yellow>{0}</color>.", _ColoredDebug, sceneName);
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            if (asyncLoad.progress >= 0.9f)
            {
                ColoredDebug.CLog(gameObject, "<color=lime>SceneLoader:</color> Сцена <color=yellow>{0}</color> загружена. Разрешаю активацию.", _ColoredDebug, sceneName);
                asyncLoad.allowSceneActivation = true;
            }
            yield return null;
        }

        ColoredDebug.CLog(gameObject, "<color=lime>SceneLoader:</color> Асинхронная загрузка сцены <color=yellow>{0}</color> полностью завершена.", _ColoredDebug, sceneName);

        if (ScreenFader.Instance != null)
        {
            ColoredDebug.CLog(gameObject, "<color=cyan>SceneLoader:</color> Скрываю экран загрузки.", _ColoredDebug);
            ScreenFader.Instance.HideLoadingScreen();
        }

        ColoredDebug.CLog(gameObject, "<color=cyan>SceneLoader:</color> Корутина <color=yellow>LoadSceneAndFade</color> завершила свою работу.", _ColoredDebug);
    }

    private string GetSceneName(GameScene scene)
    {
        switch (scene)
        {
            case GameScene.LoadBoost:
                return "00_LoadBoost";
            case GameScene.MainMenu:
                return "01_MainMenu";
            case GameScene.GameScene:
                return "02_GameScene";
            default:
                Debug.LogError($"[SceneLoader] Имя для сцены '{scene}' не найдено!");
                return null;
        }
    }
    #endregion
}