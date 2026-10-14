using UnityEngine;
using TMPro;
public class MainMenuLoader : MonoBehaviour
{
    public string LoadSceneName = "210";
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
        
    public void SetLabNum0() => PlayerPrefs.SetString("LabNum", "0");
    public void SetLabNum1() => PlayerPrefs.SetString("LabNum", "1");
    public void SetLabNum2() => PlayerPrefs.SetString("LabNum", "2");
    public void SetLabNum3() => PlayerPrefs.SetString("LabNum", "3");
    public void SetLabNum4() => PlayerPrefs.SetString("LabNum", "4");
    public void SetLabNum5() => PlayerPrefs.SetString("LabNum", "5");
    public void SetLabNum6() => PlayerPrefs.SetString("LabNum", "6");
    public void SetLabNum7() => PlayerPrefs.SetString("LabNum", "7");

    public void LoadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(LoadSceneName);
    }

    public void ClseApplication()
    {
        Application.Quit();


#if UNITY_EDITOR
        // Stops play mode inside the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
