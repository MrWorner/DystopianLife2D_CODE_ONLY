// НАЗНАЧЕНИЕ: Утилита для сборки множества C# файлов в один или несколько текстовых файлов (TXT) для удобной передачи кода ИИ, а также для копирования дополнительных файлов.
// ОСНОВНЫЕ ЗАВИСИМОСТИ: Sirenix.OdinInspector для кастомного инспектора.
// ПРИМЕЧАНИЕ: Скрипт предназначен для работы в редакторе Unity. Использует System.IO для работы с файлами.
using UnityEngine;
using Sirenix.OdinInspector;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

public class CodeAggregator : MonoBehaviour
{
#if UNITY_EDITOR
    #region Поля: Required
    [PropertyOrder(-10)]
    [BoxGroup("REQUIRED", ShowLabel = false)]
    [Tooltip("Папка, в которую будут сохранены итоговые файлы.")]
    [FolderPath(AbsolutePath = true), Required(InfoMessageType.Error), SerializeField]
    private string _outputFolderPath;
    #endregion

    #region Поля
    // --- AGGREGATION ---
    [BoxGroup("SETTINGS")]
    [BoxGroup("SETTINGS/Code Aggregation"), Tooltip("Пути к папкам, из которых нужно рекурсивно собрать все .cs файлы.")]
    [FolderPath(AbsolutePath = true, RequireExistingPath = true), SerializeField]
    private List<string> _sourceFolderPaths = new List<string>();

    [BoxGroup("SETTINGS/Code Aggregation"), Tooltip("Пути к отдельным .cs файлам, которые нужно добавить к агрегации.")]
    [FilePath(AbsolutePath = true, RequireExistingPath = true, Extensions = ".cs"), SerializeField]
    private List<string> _sourceFilePaths = new List<string>();

    [BoxGroup("SETTINGS/Code Aggregation"), Tooltip("Имя конечного файла, если агрегация идет в один файл.")]
    [SerializeField] private string _outputFileName = "AggregatedCode.txt";

    // --- ADDITIONAL FILES ---
    [BoxGroup("SETTINGS/Additional Files to Copy"), Tooltip("Выберите дополнительные файлы (любого типа), которые нужно скопировать в папку назначения.")]
    [FilePath(AbsolutePath = true, RequireExistingPath = true), SerializeField]
    private List<string> _filesToCopyPaths = new List<string>();

    // --- FEATURES ---
    [BoxGroup("SETTINGS/Aggregation Features"), Tooltip("Разделить весь код на несколько файлов?")]
    [SerializeField] private bool _splitOutput = true;

    [BoxGroup("SETTINGS/Aggregation Features"), EnableIf("_splitOutput"), Tooltip("На сколько файлов разделить весь код.")]
    [Range(2, 20), SerializeField] private int _numberOfFiles = 10;

    [BoxGroup("SETTINGS/Aggregation Features"), EnableIf("_splitOutput"), Tooltip("Создать отдельный файл-манифест с картой всего проекта?")]
    [SerializeField] private bool _createManifestFile = true;

    // --- FORMATTING ---
    [BoxGroup("SETTINGS/Aggregation Formatting"), Tooltip("В начале каждого TXT файла добавлять 'оглавление' со списком классов?")]
    [SerializeField] private bool _addClassListHeader = true;

    [BoxGroup("SETTINGS/Aggregation Formatting"), Tooltip("Перед кодом каждого класса добавлять комментарий с его оригинальным путем в проекте?")]
    [SerializeField] private bool _addOriginalPathComment = true;

    // --- GENERAL ---
    [BoxGroup("SETTINGS/General Actions"), Tooltip("Если включено, все .txt файлы в папке назначения будут УДАЛЕНЫ перед созданием новых агрегированных файлов.")]
    [SerializeField] private bool _cleanOutputFolderFirst = true;

    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug = false;
    #endregion

    #region Публичные методы
    /// <summary>
    /// Запускает настроенные процессы: агрегацию C# файлов и/или копирование дополнительных файлов.
    /// </summary>
    [Button("Запустить процесс", ButtonSizes.Large), GUIColor(0.2f, 0.8f, 0.4f)]
    [BoxGroup("ACTIONS", ShowLabel = false)]
    public void StartProcess()
    {
        ColoredDebug.CLog(gameObject, "<color=cyan>CodeAggregator:</color> Запуск процесса...", _ColoredDebug);

        bool isAggregationRequested = _sourceFolderPaths.Any() || _sourceFilePaths.Any();
        bool isCopyRequested = _filesToCopyPaths.Any();

        if (!isAggregationRequested && !isCopyRequested)
        {
            ColoredDebug.CLog(gameObject, "<color=red>Ошибка!</color> Не выбраны ни файлы для агрегации, ни файлы для копирования.", true);
            return;
        }

        // NOTE: Валидация папки назначения вынесена наверх, так как она общая для обеих операций.
        if (string.IsNullOrEmpty(_outputFolderPath))
        {
            ColoredDebug.CLog(gameObject, "<color=red>Ошибка!</color> Не указана папка назначения.", true);
            return;
        }

        try
        {
          
            if (!Directory.Exists(_outputFolderPath))
            {
                Directory.CreateDirectory(_outputFolderPath);
                ColoredDebug.CLog(gameObject, "<color=cyan>CodeAggregator:</color> Создана папка назначения: <color=white>{0}</color>", _ColoredDebug, _outputFolderPath);
            }

            bool aggregationSucceeded = false;
            if (isAggregationRequested)
            {
                aggregationSucceeded = RunAggregation();
            }

            bool copySucceeded = false;
            if (isCopyRequested)
            {
                copySucceeded = RunCopying();
            }

            if (aggregationSucceeded || copySucceeded)
            {
                ColoredDebug.CLog(gameObject, "<color=lime>Процесс успешно завершен.</color>", _ColoredDebug);
                OpenFolderInWindowsExplorer(_outputFolderPath);
            }
        }
        catch (System.Exception e)
        {
            ColoredDebug.CLog(gameObject, $"<color=red>[CodeAggregator] Произошла непредвиденная ошибка:</color> {e.Message}\n{e.StackTrace}", true);
        }

        
    }
    #endregion

    #region Основные процессы
    /// <summary>
    /// Выполняет всю логику агрегации C# кода.
    /// </summary>
    /// <returns>True, если операция прошла успешно.</returns>
    private bool RunAggregation()
    {
        if (string.IsNullOrEmpty(_outputFileName))
        {
            ColoredDebug.CLog(gameObject, "<color=red>Ошибка агрегации!</color> Не указано имя выходного файла.", true);
            return false;
        }

        CleanOutputFolder();
        List<string> uniqueFilePaths = GetUniqueFilePaths();
        if (uniqueFilePaths.Count == 0)
        {
            ColoredDebug.CLog(gameObject, "<color=orange>Агрегация:</color> Не найдено ни одного .cs файла для обработки.", _ColoredDebug);
            return false;
        }

        ColoredDebug.CLog(gameObject, "<color=cyan>Агрегация:</color> Найдено и отсортировано по алфавиту <color=yellow>{0}</color> уникальных C# файлов.", _ColoredDebug, uniqueFilePaths.Count);

        if (_splitOutput)
        {
            ProcessSplitting(uniqueFilePaths);
        }
        else
        {
            ProcessSingleFile(uniqueFilePaths);
        }
        return true;
    }

    /// <summary>
    /// Выполняет всю логику копирования дополнительных файлов.
    /// </summary>
    /// <returns>True, если хотя бы один файл был скопирован.</returns>
    private bool RunCopying()
    {
        int copiedFilesCount = 0;
        foreach (var filePath in _filesToCopyPaths)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                ColoredDebug.CLog(gameObject, $"<color=orange>Копирование:</color> Пропущен неверный путь или файл не существует: <color=white>{filePath}</color>", _ColoredDebug);
                continue;
            }

            string fileName = Path.GetFileName(filePath);
            string destinationPath = Path.Combine(_outputFolderPath, fileName);

            File.Copy(filePath, destinationPath, true); // true - перезаписать
            copiedFilesCount++;
        }

        if (copiedFilesCount > 0)
        {
            ColoredDebug.CLog(gameObject, $"<color=lime>Копирование:</color> Успешно скопировано <color=yellow>{copiedFilesCount}</color> доп. файлов.", _ColoredDebug);
            return true;
        }

        ColoredDebug.CLog(gameObject, "<color=orange>Копирование:</color> Не найдено корректных файлов для копирования.", _ColoredDebug);
        return false;
    }

    private void ProcessSingleFile(List<string> filePaths)
    {
        string outputFilePath = Path.Combine(_outputFolderPath, _outputFileName);
        var combinedCode = new StringBuilder();
        var fileNames = filePaths.Select(Path.GetFileName).ToList();

        BuildHeader(combinedCode, fileNames);

        // filePaths уже отсортирован из GetUniqueFilePaths()
        foreach (string filePath in filePaths)
        {
            AppendFileContent(combinedCode, filePath);
        }

        File.WriteAllText(outputFilePath, combinedCode.ToString());
        ColoredDebug.CLog(gameObject, "<color=lime>Агрегация успешно завершена.</color> Код объединен в: <color=white>{0}</color>", _ColoredDebug, outputFilePath);
    }

    private void ProcessSplitting(List<string> filePaths)
    {
        // filePaths уже отсортирован по имени файла
        var allFilesSortedByName = filePaths.Select(path => new FileData(path)).ToList();

        long totalLineCount = allFilesSortedByName.Sum(f => f.LineCount);
        long targetLinesPerChunk = totalLineCount / _numberOfFiles;

        var chunks = new List<List<FileData>>();
        var currentChunk = new List<FileData>();
        long currentChunkLines = 0;

        for (int i = 0; i < allFilesSortedByName.Count; i++)
        {
            var fileData = allFilesSortedByName[i];

            // --- Логика принятия решения ---
            // Начинаем ли мы новый чанк ПЕРЕД добавлением этого файла?

            // Мы "закрываем" текущий чанк и начинаем новый, если:
            // 1. Текущий чанк не пустой (в него уже что-то добавили).
            // 2. Он уже превысил СРЕДНИЙ (целевой) размер.
            // 3. Мы еще не достигли лимита чанков (т.е. это не последний чанк, который должен забрать всё оставшееся).

            bool isOverAverage = currentChunkLines >= targetLinesPerChunk;
            bool canCreateNewChunk = chunks.Count < _numberOfFiles - 1; // -1, т.к. последний чанк заберет всё

            if (currentChunk.Any() && isOverAverage && canCreateNewChunk)
            {
                // Закрываем старый чанк
                chunks.Add(currentChunk);

                // Начинаем новый чанк
                currentChunk = new List<FileData>();
                currentChunkLines = 0;

                // Пересчитываем целевой размер для ОСТАВШИХСЯ файлов и чанков для лучшей балансировки
                long remainingLines = allFilesSortedByName.Skip(i).Sum(f => f.LineCount);
                int remainingChunks = _numberOfFiles - chunks.Count;

                if (remainingChunks > 0)
                {
                    targetLinesPerChunk = remainingLines / remainingChunks;
                }
                else
                {
                    // На всякий случай, если что-то пошло не так, просто делаем лимит огромным
                    targetLinesPerChunk = long.MaxValue;
                }
            }

            // Добавляем текущий файл в текущий (возможно, новый) чанк
            currentChunk.Add(fileData);
            currentChunkLines += fileData.LineCount;
        }

        // Добавляем последний оставшийся чанк
        if (currentChunk.Any())
        {
            chunks.Add(currentChunk);
        }

        string baseFileName = Path.GetFileNameWithoutExtension(_outputFileName);
        string extension = Path.GetExtension(_outputFileName);
        int partsCreated = chunks.Count(c => c.Any());
        var partFileNames = new List<string>();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (chunk.Count == 0) continue;

            string partFileName = $"{baseFileName}_Part_{i + 1:D2}{extension}";
            partFileNames.Add(partFileName);
            string outputFilePath = Path.Combine(_outputFolderPath, partFileName);
            var combinedCode = new StringBuilder();

            // NOTE: fileNamesInChunk уже отсортирован по алфавиту, т.к. мы его так и собирали
            var fileNamesInChunk = chunk.Select(f => f.FileName).ToList();

            BuildHeader(combinedCode, fileNamesInChunk, i + 1, partsCreated);

            // Сортировка не нужна, чанк уже собран по алфавиту
            foreach (var file in chunk)
            {
                AppendFileContent(combinedCode, file.FullPath);
            }

            File.WriteAllText(outputFilePath, combinedCode.ToString());
            ColoredDebug.CLog(gameObject, $"<color=lime>Часть {i + 1} ({chunk.Count} файлов) создана:</color> <color=white>{outputFilePath}</color>", _ColoredDebug);
        }

        if (_createManifestFile)
        {
            // Передаем 'allFilesSortedByName', который гарантированно отсортирован по имени
            GenerateManifestFile(allFilesSortedByName, chunks, partFileNames);
        }

        ColoredDebug.CLog(gameObject, "<color=lime>Агрегация успешно завершена.</color> <color=yellow>{0}</color> файлов распределено по <color=yellow>{1}</color> частям.", _ColoredDebug, allFilesSortedByName.Count, partsCreated);
    }
    #endregion

    #region Хелперы для сборки файлов
    private void GenerateManifestFile(List<FileData> allFilesSortedByName, List<List<FileData>> chunks, List<string> partFileNames)
    {
        var manifestBuilder = new StringBuilder();
        string manifestFilePath = Path.Combine(_outputFolderPath, "_MANIFEST.txt");

        manifestBuilder.AppendLine($"// ======================================================");
        manifestBuilder.AppendLine($"// МАНИФЕСТ ПРОЕКТА");
        manifestBuilder.AppendLine($"// Сгенерировано: {System.DateTime.Now}");
        manifestBuilder.AppendLine($"// ======================================================");
        manifestBuilder.AppendLine();
        manifestBuilder.AppendLine($"// ---------- ОБЩАЯ СВОДКА ----------");
        manifestBuilder.AppendLine($"// Всего скриптов: {allFilesSortedByName.Count}");
        manifestBuilder.AppendLine($"// Всего строк кода (приблизительно): {allFilesSortedByName.Sum(f => f.LineCount)}");
        manifestBuilder.AppendLine($"// Разделено на: {partFileNames.Count} частей");
        manifestBuilder.AppendLine();
        manifestBuilder.AppendLine($"// ---------- КАРТА ФАЙЛОВ ----------");
        foreach (var name in partFileNames) manifestBuilder.AppendLine($"// - {name}");
        manifestBuilder.AppendLine();
        manifestBuilder.AppendLine($"// ---------- ГЛОБАЛЬНЫЙ ИНДЕКС КЛАССОВ ----------");

        // 1. Создаем карту поиска "ИмяФайла -> ИмяЧасти" для быстрой идентификации
        var fileToChunkMap = new Dictionary<string, string>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (chunk.Count == 0) continue;

            // partFileNames[i] - это имя файла, например "AllCode_Part_01.txt"
            string partName = partFileNames[i];

            foreach (var file in chunk)
            {
                if (!fileToChunkMap.ContainsKey(file.FileName))
                {
                    fileToChunkMap.Add(file.FileName, partName);
                }
            }
        }

        // 2. Итерируем ГЛОБАЛЬНЫЙ список allFilesSortedByName, который уже отсортирован по имени
        foreach (var file in allFilesSortedByName)
        {
            // 3. Ищем имя части в карте
            string partName = fileToChunkMap.TryGetValue(file.FileName, out var name) ? name : "НЕ НАЙДЕНО";
            manifestBuilder.AppendLine($"// {file.FileName,-40} -> находится в {partName}");
        }

        File.WriteAllText(manifestFilePath, manifestBuilder.ToString());
        ColoredDebug.CLog(gameObject, "<color=cyan>Файл-манифест успешно создан:</color> <color=white>{0}</color>", _ColoredDebug, manifestFilePath);
    }

    private void BuildHeader(StringBuilder sb, List<string> fileNames, int partNumber = 0, int totalParts = 0)
    {
        string title = partNumber > 0
            ? $"АГРЕГАЦИЯ КОДА ({System.DateTime.Now}) - ЧАСТЬ {partNumber}/{totalParts}"
            : $"АГРЕГАЦИЯ КОДА ({System.DateTime.Now})";

        sb.AppendLine($"// ============== {title} ============= //");
        if (_addClassListHeader && fileNames.Any())
        {
            sb.AppendLine("//");
            sb.AppendLine($"// ------------ СОДЕРЖАНИЕ (Файлов: {fileNames.Count}) ------------ //");

            // NOTE: Сортируем имена файлов для оглавления (хотя они, скорее всего, и так отсортированы)
            foreach (var name in fileNames.OrderBy(n => n)) sb.AppendLine($"//   - {name}");

            sb.AppendLine("// ------------------------------------------------ //");
        }
        sb.AppendLine($"// ================================================================================= //");
        sb.AppendLine();
    }

    private void AppendFileContent(StringBuilder sb, string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        sb.AppendLine($"// ================== НАЧАЛО ФАЙЛА: {fileName} ================== //");

        if (_addOriginalPathComment)
        {
            string relativePath = filePath.StartsWith(Application.dataPath)
                ? "Assets" + filePath.Substring(Application.dataPath.Length)
                : filePath;
            sb.AppendLine($"// Оригинальный путь: {relativePath.Replace('\\', '/')}");
        }

        sb.AppendLine();
        sb.AppendLine(File.ReadAllText(filePath));
        sb.AppendLine();
        sb.AppendLine($"// =================== КОНЕЦ ФАЙЛА: {fileName} =================== //");
        sb.AppendLine();
        sb.AppendLine();
    }
    #endregion

    #region Личные методы
    private sealed class FileData
    {
        public string FullPath { get; }
        public string FileName { get; }
        public int LineCount { get; }

        public FileData(string path)
        {
            FullPath = path;
            FileName = Path.GetFileName(path);
            // NOTE: File.ReadLines().Count() - надежный, но может быть медленным для тысяч файлов.
            // Для скорости можно использовать StreamReader, но это усложнит код.
            // Оставляем ReadLines для простоты.
            LineCount = File.ReadLines(path).Count();
        }
    }

    private void CleanOutputFolder()
    {
        if (!_cleanOutputFolderFirst || !Directory.Exists(_outputFolderPath)) return;
        string[] txtFiles = Directory.GetFiles(_outputFolderPath, "*.txt");
        if (txtFiles.Length > 0)
        {
            ColoredDebug.CLog(gameObject, $"<color=orange>Очистка папки... Удаляется <color=yellow>{txtFiles.Length}</color> .txt файлов.</color>", _ColoredDebug);
            foreach (string file in txtFiles) File.Delete(file);
        }
    }

    private List<string> GetUniqueFilePaths()
    {
        var allFilePaths = new HashSet<string>();
        _sourceFolderPaths.ForEach(path =>
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
                foreach (var file in files) allFilePaths.Add(file);
            }
        });
        _sourceFilePaths.ForEach(path =>
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                allFilePaths.Add(path);
            }
        });

        // Сортируем все найденные пути по имени файла
        return allFilePaths.OrderBy(p => Path.GetFileName(p)).ToList();
    }

    private void OpenFolderInWindowsExplorer(string path)
    {
        if (Application.platform != RuntimePlatform.WindowsEditor) return;
        Process.Start("explorer.exe", path.Replace('/', '\\'));
    }
    #endregion
#endif
}