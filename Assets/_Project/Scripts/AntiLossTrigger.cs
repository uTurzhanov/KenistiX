using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class AntiLossTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, есть ли у упавшего объекта скрипт ResettableObject
        if (other.TryGetComponent<ResettableObject>(out var resettableObject))
        {
            resettableObject.ResetToStart();
        }
    }
}