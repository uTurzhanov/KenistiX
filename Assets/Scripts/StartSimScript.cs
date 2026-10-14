using UnityEngine;




public class StartSimScript : MonoBehaviour
{
    public PendulumTimer PT;


    public GameObject Trigger;
    public GameObject[] PresetPositions;

    public GameObject ball;

    private Transform ballDefaultPosition;


    public void StartSim()
    {

        Trigger.SetActive(true);
        PT.ContinueMeasure();
        for (int i = 0; i < PresetPositions.Length; i++)
        {
            PresetPositions[i].SetActive(false);


        }

    }

    public void StopSim()
    {
        Trigger.SetActive(false);

        for (int i = 0; i < PresetPositions.Length; i++)
        {
            PresetPositions[i].SetActive(true);


        }

        PT.ResetMeasurement();



    }


    public void resetBall()
    {


        ball.transform.localPosition = new Vector3(0.1897197f, 0.6750974f, -0.6700006f);

    }


}