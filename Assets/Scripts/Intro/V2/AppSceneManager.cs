using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Централизованно хранит порядок сцен во всём приложении и умеет переходить к СЛЕДУЮЩЕЙ по
/// списку сцене, не заставляя каждую отдельную сцену/скрипт хардкодить имя следующей.
///
/// Единственный экземпляр на всё приложение (Singleton + DontDestroyOnLoad) - создаётся один раз
/// в самой первой сцене (той, что запускается при старте приложения) и переживает переходы между
/// остальными сценами сам, повторно создавать его в других сценах не нужно (и не получится -
/// Awake() сам уничтожит дубликат, если такой случайно окажется в другой сцене).
/// </summary>
public class AppSceneManager : MonoBehaviour
{
    public static AppSceneManager Instance { get; private set; }

    [Tooltip("Порядок сцен в приложении, ТОЧНЫЕ имена (как в Build Settings), по порядку прохождения. " +
             "LoadNextScene() берёт следующий элемент этого списка после текущей активной сцены.")]
    [SerializeField] private List<string> sceneOrder = new List<string>();

    /// <summary>
    /// Автосоздание менеджера ДО загрузки первой сцены - работает независимо от того, с какой
    /// сцены реально запустилось приложение (или Play Mode в редакторе). Без этого менеджер нужно
    /// было бы вручную класть в конкретную "первую" сцену, и он не появлялся бы, если приложение
    /// (или тестовый запуск) стартует с какой-то другой сцены.
    ///
    /// Берёт префаб из Assets/Resources/AppSceneManager.prefab (путь и имя ОБЯЗАТЕЛЬНО именно
    /// такие - Resources.Load ищет по строке "AppSceneManager" внутри любой папки Resources
    /// в проекте). См. инструкцию по созданию этого префаба в документации.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return; // уже существует - например, кто-то вручную положил его в текущую сцену

        GameObject prefab = Resources.Load<GameObject>("AppSceneManager");
        if (prefab == null)
        {
            Debug.LogWarning("[AppSceneManager] Не найден Resources/AppSceneManager.prefab - " +
                "менеджер не будет создан автоматически. Создайте префаб (см. документацию) или " +
                "кладите AppSceneManager в сцену вручную.");
            return;
        }

        // Awake() на созданном экземпляре сам выставит Instance и вызовет DontDestroyOnLoad -
        // тут больше ничего делать не нужно.
        Instantiate(prefab);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Загружает следующую по порядку сцену относительно текущей активной. Если текущая сцена не
    /// найдена в списке или уже последняя - ничего не загружает, пишет предупреждение в консоль.
    /// </summary>
    public void LoadNextScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentIndex = sceneOrder.IndexOf(currentSceneName);

        if (currentIndex < 0)
        {
            Debug.LogWarning($"[AppSceneManager] Текущая сцена '{currentSceneName}' не найдена в Scene Order - переход невозможен. Проверьте, что имя сцены точно совпадает с элементом списка.");
            return;
        }

        if (currentIndex + 1 >= sceneOrder.Count)
        {
            Debug.LogWarning("[AppSceneManager] Текущая сцена - последняя в Scene Order, дальше переходить некуда.");
            return;
        }

        SceneManager.LoadScene(sceneOrder[currentIndex + 1]);
    }

    /// <summary>
    /// Загружает предыдущую по порядку сцену относительно текущей активной. Если текущая сцена не
    /// найдена в списке или уже первая - ничего не загружает, пишет предупреждение в консоль.
    /// </summary>
    public void LoadPreviousScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentIndex = sceneOrder.IndexOf(currentSceneName);

        if (currentIndex < 0)
        {
            Debug.LogWarning($"[AppSceneManager] Текущая сцена '{currentSceneName}' не найдена в Scene Order - переход невозможен.");
            return;
        }

        if (currentIndex - 1 < 0)
        {
            Debug.LogWarning("[AppSceneManager] Текущая сцена - первая в Scene Order, назад переходить некуда.");
            return;
        }

        SceneManager.LoadScene(sceneOrder[currentIndex - 1]);
    }

    /// <summary>Явно загрузить конкретную сцену по имени, в обход порядка (например для кнопки "В меню"/рестарта).</summary>
    public void LoadSceneByName(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Индекс текущей сцены в Scene Order (или -1, если текущая сцена не значится в списке).</summary>
    public int CurrentSceneIndex => sceneOrder.IndexOf(SceneManager.GetActiveScene().name);

    public int TotalScenesInOrder => sceneOrder.Count;
}