using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Globalization;

// Электронные весы. Триггер-зона над поверхностью суммирует Rigidbody.mass.
// Погрешность как у DigitalCaliper: показ = true + Random(-err, +err), лэтч при изменении массы.
// ВАЖНО: повесь этот скрипт на ТОТ ЖЕ объект, где лежит триггер-бокс. Иначе OnTriggerEnter не прилетит.
[RequireComponent(typeof(BoxCollider))]
public class DigitalScale : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Бокс-триггер поверх поверхности весов. Если пусто — возьмется свой BoxCollider.")]
    [SerializeField] private BoxCollider triggerZone;
    [Tooltip("Сюда можно тащить и 3D TextMeshPro, и UI TextMeshProUGUI с Canvas.")]
    [SerializeField] private TMP_Text display;

    [Header("Погрешность как у штангеля")]
    [Tooltip("Максимальная добавка в кг. 0.00015 кг = 0.15 г")]
    [SerializeField] private float maxErrorKg = 0.00015f;
    [Tooltip("Порог перегенерации ошибки в кг. 0.00005 кг = 0.05 г")]
    [SerializeField] private float regenThresholdKg = 0.00005f;

    [Header("Тара (опционально)")]
    [SerializeField] private bool useTare = false;

    private readonly HashSet<Rigidbody> _bodies = new HashSet<Rigidbody>();
    private float _currentError;
    private float _lastTrueKg;
    private float _tareKg;
    private string _lastText = "";
    private float _lastDisplayKg = float.NaN;

    private void Awake()
    {
        if (triggerZone == null)
            triggerZone = GetComponent<BoxCollider>();

        if (triggerZone != null && !triggerZone.isTrigger)
            triggerZone.isTrigger = true;

        GenerateNewError();
    }

    private void OnValidate()
    {
        if (triggerZone != null && !triggerZone.isTrigger)
            triggerZone.isTrigger = true;
    }

    private void GenerateNewError()
    {
        _currentError = Random.Range(-maxErrorKg, maxErrorKg);
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;
        // Свои детали весов игнорируем: все что внутри того же корня весов
        Transform scaleRoot = transform.parent != null ? transform.parent : transform;
        if (rb.transform == scaleRoot || rb.transform.IsChildOf(scaleRoot)) return;
        _bodies.Add(rb);
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;
        _bodies.Remove(rb);
    }

    private float SumTrueMass()
    {
        float sum = 0f;
        // Чистим уничтоженные заодно без аллокаций
        _bodies.RemoveWhere(b => b == null);
        foreach (var b in _bodies)
        {
            if (b == null) continue;
            sum += b.mass;
        }
        return sum;
    }

    private void Update()
    {
        float trueKg = SumTrueMass();

        if (useTare)
            trueKg = Mathf.Max(0f, trueKg - _tareKg);

        // Лэтч ошибки как у штангеля: только при заметном изменении
        if (Mathf.Abs(trueKg - _lastTrueKg) > regenThresholdKg)
        {
            GenerateNewError();
            _lastTrueKg = trueKg;
        }

        float displayKg = trueKg + _currentError;

        // Пустая чаша — чистый ноль. Ограничения по весу нет: любой предмет взвешивается.
        _bodies.RemoveWhere(b => b == null);
        if (_bodies.Count == 0)
            displayKg = 0f;

        displayKg = Mathf.Max(0f, displayKg);

        // Обновляем текст только при изменении — экономим перф для 90 FPS
        if (Mathf.Approximately(displayKg, _lastDisplayKg) && _lastText != "")
            return;

        _lastDisplayKg = displayKg;

        // Показ в граммах: 3-47 г читаются лучше чем 0.003 кг
        // Латиница g специально: в LiberationSans SDF кириллическое г может не отрисоваться
        float displayG = displayKg * 1000f;
        string s = displayG.ToString("F2", CultureInfo.InvariantCulture) + " g";

        if (s != _lastText)
        {
            _lastText = s;
            if (display != null)
                display.text = s;
        }
    }

    // Тара на кнопку (повесь на UI или контроллер при желании)
    public void Tare()
    {
        _tareKg = SumTrueMass();
        GenerateNewError();
    }

    public void ClearTare()
    {
        _tareKg = 0f;
        GenerateNewError();
    }

    // API для логгера фото-замеров (как GetDisplayValue у штангеля)
    public float GetTrueMassKg()
    {
        float t = SumTrueMass();
        if (useTare) t = Mathf.Max(0f, t - _tareKg);
        return t;
    }

    public float GetDisplayMassKg()
    {
        return _lastDisplayKg;
    }

    public string GetDisplayString()
    {
        return _lastText;
    }

    public bool HasSample()
    {
        _bodies.RemoveWhere(b => b == null);
        return _bodies.Count > 0;
    }
}
