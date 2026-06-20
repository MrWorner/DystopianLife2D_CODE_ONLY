// Требуется импорт Odin Inspector
using Sirenix.OdinInspector;
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// Копирует все .cs файлы из исходной папки (sourceDirectory)
/// в целевую папку (destinationDirectory),
/// полностью сохраняя иерархию каталогов.
/// ПОСЛЕ копирования удаляет все пустые папки из целевой директории.
/// </summary>
public class ScriptExporter : MonoBehaviour
{
    [Title("Настройки Экспорта Скриптов")]
    [InfoBox("Укажите папку, ИЗ КОТОРОЙ нужно скопировать скрипты. \nПо умолчанию это папка 'Assets' вашего проекта.")]
    [FolderPath(AbsolutePath = true, RequireExistingPath = true)]
    public string sourceDirectory = "";

    [InfoBox("Укажите папку, КУДА будут скопированы скрипты. \n(Например, папка вашего нового Git-репозитория)")]
    [FolderPath(AbsolutePath = true)]
    public string destinationDirectory = "";

    /// <summary>
    /// При запуске сцены или входе в PlayMode, 
    /// автоматически заполняет исходный путь папкой Assets, если он пуст.
    /// </summary>
    private void Awake()
    {
        if (string.IsNullOrEmpty(sourceDirectory))
        {
            // Application.dataPath указывает на папку Assets
            sourceDirectory = Application.dataPath;
        }
    }

    [Button("🚀 Запустить Копирование Скриптов и Очистку", ButtonSizes.Large)]
    [GUIColor(0.2f, 0.8f, 0.2f)] // Зеленая кнопка
    private void ExportScriptsAndClean()
    {
        // 1. Валидация (проверка) путей
        if (string.IsNullOrEmpty(sourceDirectory) || string.IsNullOrEmpty(destinationDirectory))
        {
            UnityEngine.Debug.LogError("Ошибка: Исходная или целевая папка не указана!");
            return;
        }

        if (!Directory.Exists(sourceDirectory))
        {
            UnityEngine.Debug.LogError($"Ошибка: Исходная папка не найдена: {sourceDirectory}");
            return;
        }

        if (Path.GetFullPath(sourceDirectory) == Path.GetFullPath(destinationDirectory))
        {
            UnityEngine.Debug.LogError("Ошибка: Исходная и целевая папки не могут совпадать!");
            return;
        }

        try
        {
            UnityEngine.Debug.Log($"Начинаю копирование из {sourceDirectory} в {destinationDirectory}...");

            // 2. Создаем корневую целевую папку, если ее нет
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // 3. Запускаем рекурсивный процесс КОПИРОВАНИЯ
            CopyScriptsRecursive(sourceDirectory, destinationDirectory);

            UnityEngine.Debug.Log("Копирование файлов завершено. Начинаю удаление пустых папок...");

            // 4. НОВЫЙ ШАГ: Запускаем рекурсивный процесс УДАЛЕНИЯ ПУСТЫХ ПАПОК
            DeleteEmptyFoldersRecursive(destinationDirectory);

            UnityEngine.Debug.Log($"<color=green><b>Очистка и экспорт успешно завершены!</b></color> Скрипты сохранены в: {destinationDirectory}");

            // 5. (Бонус) Открываем папку в проводнике
            OpenFolder(destinationDirectory);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Произошла фатальная ошибка во время копирования: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Главная рекурсивная функция копирования.
    /// </summary>
    /// <param name="sourceDir">Текущая сканируемая папка.</param>
    /// <param name="destDir">Соответствующая ей целевая папка.</param>
    private void CopyScriptsRecursive(string sourceDir, string destDir)
    {
        // --- Шаг 1: Рекурсия по всем ПОДПАПКАМ ---
        foreach (string dirPath in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(dirPath);
            string newDestDir = Path.Combine(destDir, dirName);

            // Мы не создаем папку здесь сразу.
            // Папка будет создана, только если в ней (или в ее дочерних папках)
            // найдется хотя бы один .cs файл.

            CopyScriptsRecursive(dirPath, newDestDir);
        }

        // --- Шаг 2: Копирование ФАЙЛОВ в текущей папке ---
        foreach (string filePath in Directory.GetFiles(sourceDir))
        {
            if (Path.GetExtension(filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                // А вот ТЕПЕРЬ, когда мы нашли .cs файл,
                // мы гарантируем, что целевая папка существует.
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(destDir, fileName);
                File.Copy(filePath, destFilePath, true);
            }
        }
    }

    /// <summary>
    /// (НОВАЯ ФУНКЦИЯ)
    /// Рекурсивно удаляет пустые папки, начиная с самого нижнего уровня.
    /// Это называется "Post-order traversal" (обход в обратном порядке).
    /// </summary>
    /// <param name="targetDir">Папка для проверки.</param>
    private void DeleteEmptyFoldersRecursive(string targetDir)
    {
        // 1. Сначала рекурсивно проверяем все ПОДПАПКИ
        // Мы должны сначала обработать дочерние папки, прежде чем проверять родительскую.
        foreach (string dir in Directory.GetDirectories(targetDir))
        {
            DeleteEmptyFoldersRecursive(dir);
        }

        // 2. После того, как все подпапки обработаны,
        // проверяем ТЕКУЩУЮ папку
        try
        {
            // Проверяем, пуста ли папка (нет ни файлов, ни других папок)
            if (Directory.GetFiles(targetDir).Length == 0 &&
                Directory.GetDirectories(targetDir).Length == 0)
            {
                // Если пуста - удаляем
                Directory.Delete(targetDir);
                // UnityEngine.Debug.Log($"Удалена пустая папка: {targetDir}"); // (Раскомментируйте для отладки)
            }
        }
        catch (Exception e)
        {
            // Эта ошибка может возникнуть, если к папке есть доступ
            // (например, она открыта в Проводнике), но это не критично.
            UnityEngine.Debug.LogWarning($"Не удалось удалить папку {targetDir}: {e.Message}");
        }
    }


    /// <summary>
    /// Вспомогательная функция для открытия папки в Проводнике Windows или Finder в macOS.
    /// </summary>
    private void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = Path.GetFullPath(path), // Убедимся, что путь абсолютный
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }
}