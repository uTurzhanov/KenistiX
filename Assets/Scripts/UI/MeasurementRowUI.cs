using TMPro;
using UnityEngine;

public class MeasurementRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private TMP_Text voltageText;
    [SerializeField] private TMP_Text currentText;
    [SerializeField] private TMP_Text resistanceText;
    [SerializeField] private TMP_Text timeText;

    public void Setup(
        int number,
        float voltage,
        float current,
        float resistance,
        string sessionTime)
    {
        numberText.text = number.ToString();
        voltageText.text = $"{voltage:F2} V";
        currentText.text = $"{current:F3} A";
        resistanceText.text = $"{resistance:F2} Ω";
        timeText.text = sessionTime;
    }
}