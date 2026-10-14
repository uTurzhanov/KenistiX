using UnityEngine;

public class OscilloscopeLevel : MonoBehaviour
{
    [SerializeField] private OscilloscopeCore _oscilloscopeCore;
    [SerializeField] private OscilloscopeDisplay _oscilloscopeDisplay;

    private void Start()
    {
        _oscilloscopeDisplay.Initialize();
        _oscilloscopeCore.Initialize();
    }
}
