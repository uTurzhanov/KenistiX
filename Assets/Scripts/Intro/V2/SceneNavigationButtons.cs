using UnityEngine;

/// <summary>
/// Тонкая прослойка для UI-кнопок "Следующая сцена"/"Предыдущая сцена"/"Конкретная сцена" в любой
/// сцене приложения.
///
/// AppSceneManager создаётся во время выполнения (через Resources + RuntimeInitializeOnLoadMethod),
/// поэтому в момент редактирования сцены его физически не существует - нельзя перетащить его
/// экземпляр в поле OnClick() у кнопки в инспекторе, там ссылаться не на что. Этот компонент -
/// обычный объект сцены, существующий уже на этапе редактирования, поэтому НА НЕГО кнопку
/// подписать можно как обычно, а сам он внутри уже находит реальный AppSceneManager.Instance
/// и вызывает нужный метод.
///
/// Повесить на любой объект в сцене (например на сам объект с кнопками). Для кнопок меню
/// лабораторных работ используется LoadScene(string) - у неё есть параметр, Unity сама покажет
/// текстовое поле в инспекторе под OnClick(), куда вписывается точное имя нужной сцены - на
/// каждую кнопку меню вешается один и тот же метод, просто с разным именем сцены в параметре.
/// </summary>
public class SceneNavigationButtons : MonoBehaviour
{
    public void NextScene()
    {
        if (AppSceneManager.Instance == null)
        {
            Debug.LogWarning("[SceneNavigationButtons] AppSceneManager.Instance не найден - переход невозможен.");
            return;
        }

        AppSceneManager.Instance.LoadNextScene();
    }

    public void PreviousScene()
    {
        if (AppSceneManager.Instance == null)
        {
            Debug.LogWarning("[SceneNavigationButtons] AppSceneManager.Instance не найден - переход невозможен.");
            return;
        }

        AppSceneManager.Instance.LoadPreviousScene();
    }

    /// <summary>Переход на конкретную сцену по имени, в обход порядка - для кнопок меню лабораторных
    /// работ (выбор конкретной лабы) и для кнопки "Назад в меню" внутри самих лабораторных.</summary>
    public void LoadScene(string sceneName)
    {
        if (AppSceneManager.Instance == null)
        {
            Debug.LogWarning("[SceneNavigationButtons] AppSceneManager.Instance не найден - переход невозможен.");
            return;
        }

        AppSceneManager.Instance.LoadSceneByName(sceneName);
    }
}
