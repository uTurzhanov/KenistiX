using TMPro;
using UnityEngine;

public class Scales2 : MonoBehaviour
{
    public TMP_Text display;

    private float totalWeight = 0f;


    void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.GetComponent<MockWeight>() != null)
        {
            totalWeight += other.gameObject.GetComponent<MockWeight>().weight;
            display.text = totalWeight.ToString() + " g";

        }

    }


    void OnTriggerExit(Collider other)
    {
        if(other.gameObject.GetComponent<MockWeight>() != null)
        {
            totalWeight -= other.gameObject.GetComponent<MockWeight>().weight;
            display.text = totalWeight.ToString() + " g";

        }
    }

}
