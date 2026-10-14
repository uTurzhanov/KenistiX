
using UnityEngine;

public class SocketController : MonoBehaviour
{

    public GameObject centerRing;
    public GameObject centerCylinder;
    public GameObject edgeCylinder1;
    public GameObject edgeCylinder2;

    private TagFilteredSocket centerRingSocket;
    private TagFilteredSocket centerCylinderSocket;
    private TagFilteredSocket edgeCylinder1Socket;
    private TagFilteredSocket edgeCylinder2Socket;

    public BodyConfigController BCC;

    //cfgs
    //0 - empty platform
    //1 - ring
    //2 - cylinder
    //3 - 2 cylinders

    void Start()
    {
        centerRingSocket = centerRing.GetComponent<TagFilteredSocket>();
        centerCylinderSocket = centerCylinder.GetComponent<TagFilteredSocket>();
        edgeCylinder1Socket = edgeCylinder1.GetComponent<TagFilteredSocket>();
        edgeCylinder2Socket = edgeCylinder2.GetComponent<TagFilteredSocket>();
    }



    public void updCfg()
    {
        if (centerRingSocket.isOccupied)
        {  
            centerCylinder.SetActive(false);
            edgeCylinder1.SetActive(false);
            edgeCylinder2.SetActive(false);
            BCC.ChangeConfig(1);
        }

        if (centerCylinderSocket.isOccupied)
        {
            centerRing.SetActive(false);
            edgeCylinder1.SetActive(false);
            edgeCylinder2.SetActive(false);
            BCC.ChangeConfig(2);

        }

        if(edgeCylinder1Socket.isOccupied || edgeCylinder2Socket.isOccupied)
        {
            centerRing.SetActive(false);
            centerCylinder.SetActive(false);
            BCC.ChangeConfig(3);
        }

        if(!centerCylinderSocket.isOccupied && !centerRingSocket.isOccupied && !edgeCylinder1Socket.isOccupied && !edgeCylinder2Socket.isOccupied)
        {
            BCC.ChangeConfig(0);
            centerCylinder.SetActive(true);
            centerRing.SetActive(true);
            edgeCylinder1.SetActive(true);
            edgeCylinder2.SetActive(true);

        }


    }
}
