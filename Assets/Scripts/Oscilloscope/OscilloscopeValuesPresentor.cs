using TMPro;
using UnityEngine;

public class OscilloscopeValuesPresentor : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _valuesText;

    private ReactableValue<float> _ch1VoltsDiv;
    private ReactableValue<float> _ch2VoltsDiv;

    private ReactableValue<float> _timeDiv;

    public void Initailize(ReactableValue<float> ch1VoltsDiv, ReactableValue<float> ch2VoltsDiv, ReactableValue<float> timeDiv)
    { 
        _ch1VoltsDiv = ch1VoltsDiv;
        _ch2VoltsDiv = ch2VoltsDiv;

        _timeDiv = timeDiv;

        _ch1VoltsDiv.OnValueChanged += UpdateValues;
        _ch2VoltsDiv.OnValueChanged += UpdateValues;
        _timeDiv.OnValueChanged += UpdateValues;

        UpdateValues();
    }

    private void UpdateValues(float _)
    {
        UpdateValues();
    }
    private void UpdateValues()
    {
        _valuesText.text = $"CH 1 Volts/Div:{_ch1VoltsDiv.CurrentValue}\tCH 2 Volts/Div:{_ch2VoltsDiv.CurrentValue}\tTime/Div:{_timeDiv.CurrentValue}";
    }

    private void OnDestroy()
    {
        _ch1VoltsDiv.OnValueChanged -= UpdateValues;
        _ch2VoltsDiv.OnValueChanged -= UpdateValues;
        _timeDiv.OnValueChanged -= UpdateValues;
    }
}
