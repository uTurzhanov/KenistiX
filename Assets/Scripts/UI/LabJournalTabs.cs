using UnityEngine;
using UnityEngine.UI;

public class LabJournalTabs : MonoBehaviour
{
    [Header("Кнопки вкладок")]
    [SerializeField] private Button[] tabButtons;

    [Header("Панели содержимого")]
    [SerializeField] private GameObject[] tabPanels;

    [Header("Начальная вкладка")]
    [SerializeField] private int defaultTabIndex = 0;

    private void Awake()
    {
        if (tabButtons.Length != tabPanels.Length)
        {
            Debug.LogError(
                "LabJournalTabs: количество кнопок " +
                "не совпадает с количеством панелей."
            );

            enabled = false;
            return;
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tabIndex = i;

            tabButtons[i].onClick.AddListener(
                () => ShowTab(tabIndex)
            );
        }

        ShowTab(defaultTabIndex);
    }

    public void ShowTab(int selectedIndex)
    {
        if (selectedIndex < 0 ||
            selectedIndex >= tabPanels.Length)
        {
            return;
        }

        for (int i = 0; i < tabPanels.Length; i++)
        {
            bool isSelected = i == selectedIndex;

            tabPanels[i].SetActive(isSelected);

            // Активная кнопка становится синей
            // через Disabled Color.
            tabButtons[i].interactable = !isSelected;
        }
    }
}