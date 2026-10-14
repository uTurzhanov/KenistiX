using UnityEngine;
 

public class PendulumAutoReset : MonoBehaviour
{
    bool flag = true;
    public GameObject Ball;

    void OnCollisionExit(Collision collision)
    {
        if(collision.gameObject.tag == "Pendulum" ){

            Debug.Log("Маятник ресетнут из за триггера");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.gameObject.tag == "Pendulum" && flag){
            Ball.transform.localPosition = new Vector3(0.1897197f, 0.6750974f, -0.6700006f);
            Debug.Log("Маятник ресетнут из за триггера");
            
        }
    }
}
