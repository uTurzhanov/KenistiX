using UnityEngine;

public class CircuitControllerHall : MonoBehaviour
{
    public TagFilteredSocketHall socketSolenoidPlus;
    public TagFilteredSocketHall socketSolenoidMinus;
    public TagFilteredSocketHall socket3;
    public TagFilteredSocketHall socket4;


    public HallSensorSimulator HES;

    public void checkCircuit()
    {
        if (socketSolenoidPlus.isOccupied && socketSolenoidMinus.isOccupied && socket3.isOccupied && socket4.isOccupied)
        {
            if (socketSolenoidPlus.connectedGameObject.GetComponent<WirePolarity>().polarity == "minus")
            {
                HES.SetPolarity(-1);
            }
            else
            {
                HES.SetPolarity(1);
            }


        }
        else
            HES.SetPolarity(0);



    }

    

}
