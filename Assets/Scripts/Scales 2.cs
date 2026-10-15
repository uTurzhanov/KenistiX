using TMPro;
using UnityEngine;

public class Scales2 : MonoBehaviour
{
    public float addedError;

    public TMP_Text display;

    private float totalWeight = 0f;


    private float AddError(float inp)
    {



        float error = UnityEngine.Random.Range(-addedError, addedError);

        Debug.Log($"Добавленная ошибка {error}");

        return inp + error;

    }

    void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.GetComponent<MockWeight>() != null)
        {
            totalWeight += other.gameObject.GetComponent<MockWeight>().weight;
            display.text = (totalWeight + AddError(addedError)).ToString("F3") + " g";

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
