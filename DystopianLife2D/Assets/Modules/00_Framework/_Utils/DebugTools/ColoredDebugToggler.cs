// НАЗНАЧЕНИЕ: Утилита для массового включения/отключения флагов _ColoredDebug во всех компонентах на сцене.
// ОСНОВНЫЕ ЗАВИСИСИМОСТИ: Sirenix.OdinInspector, UnityEditor, System.Reflection.
// ПРИМЕЧАНИЕ: Этот скрипт предназначен для работы исключительно в редакторе Unity.
using UnityEngine;
using Sirenix.OdinInspector;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ColoredDebugToggler : MonoBehaviour
{
#if UNITY_EDITOR
    #region Поля
    [BoxGroup("DEBUG"), SerializeField] private bool _ColoredDebug;
    #endregion Поля

    #region Публичные методы
    [Button("Включить ВСЕ ColoredDebug логи", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 0.4f)]
    [BoxGroup("ACTIONS", ShowLabel = false)]
    public void EnableAllLogs()
    {
        ToggleAllLogs(true);
    }

    [Button("Выключить ВСЕ ColoredDebug логи", ButtonSizes.Large), GUIColor(0.8f, 0.4f, 0.4f)]
    [BoxGroup("ACTIONS")]
    public void DisableAllLogs()
    {
        ToggleAllLogs(false);
    }
    #endregion Публичные методы

    #region Личные методы
    /// <summary>
    /// Находит все MonoBehaviour на сцене и изменяет значение их поля _ColoredDebug.
    /// </summary>
    /// <param name="isEnabled">Целевое состояние флага.</param>
    private void ToggleAllLogs(bool isEnabled)
    {
        // Находим абсолютно все компоненты типа MonoBehaviour на активной сцене.
        MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int changedCount = 0;

        ColoredDebug.CLog(gameObject, "<color=cyan>ColoredDebugToggler:</color> Поиск компонентов с полем '_ColoredDebug'. Всего компонентов на сцене: <color=yellow>{0}</color>.", _ColoredDebug, allComponents.Length);

        foreach (var component in allComponents)
        {
            if (component == null) continue;

            // Используем рефлексию, чтобы получить доступ к полю по его имени
            FieldInfo field = component.GetType().GetField("_ColoredDebug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Проверяем, что поле найдено и что его тип - bool
            if (field != null && field.FieldType == typeof(bool))
            {
                // Устанавливаем новое значение
                field.SetValue(component, isEnabled);
                // Помечаем объект как "измененный", чтобы Unity сохранил изменения
                EditorUtility.SetDirty(component);
                changedCount++;
            }
        }

        ColoredDebug.CLog(gameObject, "<color=lime>ColoredDebugToggler:</color> Операция завершена. Изменено <color=yellow>{0}</color> компонентов. Новое состояние логов: <color=lime>{1}</color>.", _ColoredDebug, changedCount, isEnabled ? "Включено" : "Выключено");

        // Сохраняем изменения в сцене
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
    #endregion Личные методы

#endif
}
