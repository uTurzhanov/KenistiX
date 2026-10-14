using UnityEngine;

public class PlayerPrefsEnable : MonoBehaviour
{
    public string LabNum = "1";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (PlayerPrefs.HasKey("LabNum"))
        {
            if (LabNum == PlayerPrefs.GetString("LabNum"))
            {
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            return;
        }
    }
    public void SetLabNum7() => PlayerPrefs.SetString("LabNum", "7");

    // Update is called once per frame
    void Update()
    {
        
    }
}
