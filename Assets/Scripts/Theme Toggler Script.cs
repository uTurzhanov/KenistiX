using UnityEngine;

public class ThemeTogglerScript : MonoBehaviour
{
    public GameObject sunLight;
    public GameObject moonLight;
    public GameObject sunDark;
    public GameObject moonDark;

    public GameObject canvasLight;
    public GameObject canvasDark;

    private bool curState = false;

    public void updateTheme()
    {
        if(!curState){
            sunLight.SetActive(false);
            moonLight.SetActive(false);

            sunDark.SetActive(true);
            moonDark.SetActive(true);

        }
        else
        {
            sunLight.SetActive(true);
            moonLight.SetActive(true);

            sunDark.SetActive(false);
            moonDark.SetActive(false);

        }

        curState = !curState;
    }
}
