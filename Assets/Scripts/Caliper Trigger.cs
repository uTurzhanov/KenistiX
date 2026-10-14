using System.Diagnostics;
using TMPro;
using UnityEngine;

public class CaliperTrigger : MonoBehaviour
{
    //Руслан вонючка
    public int measurementType = 0;
    public TMP_Text display;

    public bool isOuterRadius = true;

    void OnTriggerEnter(Collider other)
    {


        if( other.gameObject.tag == "SMALL")
        {
            switch (measurementType){

                case 0:
                    display.text = "38 mm";
                    break;
                case 1:
                    display.text = "80 mm";
                    break;
                case 2:
                    display.text = "38 mm";
                    break;

            }

        }

        if(other.gameObject.tag == "BIG")
        {
            if(isOuterRadius){
                switch (measurementType)
                {

                case 0:
                    display.text = "119.8 mm";
                    break;
                case 1:
                    display.text = "10 mm";
                    break;
                case 2:
                    display.text = "119.8 mm";
                    break;

            }
            }
            else
            {
                display.text = "75.5 mm";
            }



        }


    }

    void OnTriggerExit(Collider other)
    {
        
        display.text = "WTN CLMP";


    }

    public void setMeasurementType(int MT)
    {
        measurementType = MT;
    }




}
