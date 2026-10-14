using UnityEngine;

public class WireSpawner : MonoBehaviour
{
    [Header("Настройки спавнера")]
    public GameObject wirePrefab;
    public Transform spawnPoint;

    [Header("Настройки вытягивания")]
    [Tooltip("Дистанция (в метрах), на которую нужно оттащить штекер, чтобы появился новый провод")]
    public float spawnDistance = 0.2f; // 0.2f = 20 сантиметров

    private GameObject currentWire;
    private WirePinIdentity[] currentPins;

    private void Start()
    {
        SpawnWire();
    }

    private void Update()
    {
        // РЕШЕНИЕ ПРОБЛЕМЫ 1: 
        // Если WiresDestroyer удалил провода на сцене, объект currentWire уничтожается 
        // и становится null. Мы это замечаем и мгновенно спавним новый!
        if (currentWire == null)
        {
            SpawnWire();
            return;
        }

        // РЕШЕНИЕ ПРОБЛЕМЫ 2:
        // Вместо глючных триггеров и событий захвата, мы просто измеряем расстояние 
        // от спавнера до штекеров провода.
        Vector3 spawnerPos = spawnPoint != null ? spawnPoint.position : transform.position;
        bool isPulledAway = false;

        // Проверяем, оттащил ли игрок хотя бы один штекер от точки спавна
        if (currentPins != null)
        {
            foreach (WirePinIdentity pin in currentPins)
            {
                if (pin != null)
                {
                    // Если дистанция больше заданного порога (например, 20 см)
                    if (Vector3.Distance(pin.transform.position, spawnerPos) > spawnDistance)
                    {
                        isPulledAway = true;
                        break;
                    }
                }
            }
        }

        // Если провод вытащили на достаточное расстояние - спавним новый
        if (isPulledAway)
        {
            currentWire = null;
            currentPins = null;
            SpawnWire();
        }
    }

    private void SpawnWire()
    {
        if (wirePrefab == null) return;
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        currentWire = Instantiate(wirePrefab, position, rotation);

        // Запоминаем все штекеры этого провода, чтобы отслеживать, когда игрок их потянет
        currentPins = currentWire.GetComponentsInChildren<WirePinIdentity>();
    }
}