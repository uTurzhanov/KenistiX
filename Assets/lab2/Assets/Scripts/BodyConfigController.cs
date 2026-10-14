using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Модель, которая может быть смещена вдоль оси платформы на расстояние d
/// (используется для цилиндров - и в центре (d=0), и со смещением).
/// </summary>
[Serializable]
public class BodyOffsetModel
{
    [Tooltip("3D-модель тела. Должна быть child-объектом платформы.")]
    public Transform model;

    [Tooltip("Знак смещения вдоль offsetAxis. Для одного цилиндра: +1. " +
             "Для пары цилиндров: один +1, другой -1 (по разные стороны от оси).")]
    public float sign = 1f;
}

/// <summary>
/// Одна конфигурация тела на платформе: что показать, какие у тела
/// характеристики (масса/радиусы - задаются здесь, а не в игре),
/// и нужно ли поле ввода расстояния d.
/// </summary>
[Serializable]
public class BodyConfig
{
    [Tooltip("Название, отображаемое между стрелками на экране")]
    public string displayName = "Только платформа";

    [Header("Модели для этой конфигурации")]
    [Tooltip("Модели, которые НИКОГДА не смещаются по оси (просто включаются/выключаются) - " +
             "например диск или кольцо, которые всегда лежат строго в центре")]
    public GameObject[] staticModels;

    [Tooltip("Модели, позиция которых вдоль offsetAxis пересчитывается каждый раз " +
             "(цилиндры - и в центре с d=0, и со смещением с d>0). Если один и тот же " +
             "объект цилиндра используется и в центре, и со смещением в разных " +
             "конфигурациях - указывайте его именно здесь, а не в staticModels, иначе " +
             "его позиция может 'застыть' от предыдущей конфигурации.")]
    public BodyOffsetModel[] offsetModels;

    [Header("Параметры тела (задаются здесь, НЕ вводятся в игре)")]
    [Tooltip("Масса ОДНОГО экземпляра тела, кг. Для 'двух цилиндров' - масса одного цилиндра.")]
    public float mass = 0f;

    [Tooltip("Внутренний радиус, м. 0 для диска/цилиндра, >0 для кольца.")]
    public float innerRadius = 0f;

    [Tooltip("Внешний (или единственный) радиус тела, м.")]
    public float outerRadius = 0f;

    [Tooltip("Сколько одинаковых экземпляров тела. 1 для диска/кольца/одного цилиндра, 2 для двух цилиндров.")]
    public int instanceCount = 1;

    [Tooltip("Показывать ли поле ввода расстояния d для этой конфигурации " +
             "(true только для 'цилиндр на расстоянии d' и 'два цилиндра на расстоянии d')")]
    public bool requiresOffsetInput = false;
}

/// <summary>
/// Управляет выбором тела на платформе через две стрелки на экране устройства.
///
/// Что делает при каждом переключении конфигурации или изменении d:
///  1) скрывает все модели тел, показывает только нужные для текущей конфигурации;
///  2) для моделей с возможным смещением - выставляет их позицию вдоль offsetAxis
///     равной 0 (центр) или +-d (смещение), в зависимости от текущей конфигурации;
///  3) выводит название и автоматически сформированное описание характеристик
///     (масса, радиус(ы)) - эти числа берутся из инспектора, не из игры;
///  4) показывает/прячет поле ввода d;
///  5) считает суммарную массу и момент инерции тела (с учётом теоремы Штейнера,
///     если есть смещение) и передаёт их в TrifilarPlatformSimulator.SetBodyContribution.
/// </summary>
public class BodyConfigController : MonoBehaviour
{
    [Header("Ссылка на платформу")]
    public TrifilarPlatformSimulator platform;

    [Header("Список конфигураций (порядок = порядок переключения стрелками)")]
    public BodyConfig[] configs;

    [Header("UI на экране устройства")]
    public Button prevButton;
    public Button nextButton;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_InputField distanceInput;

    [Header("Настройки смещения")]
    [Tooltip("Локальная ось платформы, вдоль которой откладывается расстояние d")]
    public Vector3 offsetAxis = Vector3.right;

    [Tooltip("Расстояние d по умолчанию, м")]
    public float defaultDistance = 0.1f;

    private int _currentIndex;
    private float _currentDistance;
    private GameObject[] _allManagedModels;

    private void Start()
    {
        CollectAllManagedModels();

        _currentDistance = defaultDistance;
        distanceInput.text = _currentDistance.ToString("0.###");
        distanceInput.contentType = TMP_InputField.ContentType.DecimalNumber;
        distanceInput.onEndEdit.AddListener(OnDistanceChanged);

        //cfgs
        //0 - empty platform
        //1 - ring
        //2 - cylinder
        //3 - 2 cylinders


        _currentIndex = 0;
        ApplyConfig();
    }

    private void CollectAllManagedModels()
    {
        var set = new HashSet<GameObject>();
        foreach (var cfg in configs)
        {
            if (cfg.staticModels != null)
            {
                foreach (var m in cfg.staticModels)
                {
                    if (m != null) set.Add(m);
                }
            }

            if (cfg.offsetModels != null)
            {
                foreach (var om in cfg.offsetModels)
                {
                    if (om.model != null) set.Add(om.model.gameObject);
                }
            }
        }

        _allManagedModels = new GameObject[set.Count];
        set.CopyTo(_allManagedModels);
    }

    public void ChangeConfig(int delta)
    {
        _currentIndex = delta;
        ApplyConfig();
    }

    private void OnDistanceChanged(string text)
    {
        if (float.TryParse(text, out float d) && d >= 0f)
        {
            _currentDistance = d;
        }
        else
        {
            distanceInput.text = _currentDistance.ToString("0.###");
        }

        ApplyConfig();
    }

    private void ApplyConfig()
    {
        BodyConfig cfg = configs[_currentIndex];

        // Скрываем абсолютно все управляемые модели тел
        foreach (var go in _allManagedModels)
        {
            if (go != null) go.SetActive(false);
        }

        // Расстояние, реально используемое для смещения и для расчёта Штейнера:
        // 0, если эта конфигурация не требует поля ввода d
        float distance = cfg.requiresOffsetInput ? _currentDistance : 0f;
        /*
        if (cfg.staticModels != null)
        {
            foreach (var m in cfg.staticModels)
            {
                if (m != null) m.SetActive(true);
            }
        }

        if (cfg.offsetModels != null)
        {
            Vector3 axis = offsetAxis.normalized;

            foreach (var om in cfg.offsetModels)
            {
                if (om.model == null) continue;

                om.model.gameObject.SetActive(true);

                // Убираем старую компоненту позиции вдоль оси, ставим новую (0 или +-d)
                Vector3 pos = om.model.localPosition;
                Vector3 alongAxisOld = Vector3.Project(pos, axis);
                Vector3 rest = pos - alongAxisOld;
                om.model.localPosition = rest + axis * (om.sign * distance);
            }
        }
            */
        

        nameText.text = cfg.displayName;
        descriptionText.text = BuildDescription(cfg);

        distanceInput.gameObject.SetActive(cfg.requiresOffsetInput);

        // Считаем вклад тела в общую массу/момент инерции и передаём в платформу
        float ownJ = 0.5f * cfg.mass * (cfg.innerRadius * cfg.innerRadius + cfg.outerRadius * cfg.outerRadius);
        float steinerJ = cfg.mass * distance * distance;
        float totalJ = cfg.instanceCount * (ownJ + steinerJ);
        float totalMass = cfg.instanceCount * cfg.mass;

        platform.SetBodyContribution(totalMass, totalJ);
    }

    private string BuildDescription(BodyConfig cfg)
    {
        if (cfg.mass <= 0f)
        {
            return "Тело отсутствует";
        }

        string radiusPart = cfg.innerRadius > 0f
            ? $"r1 = {cfg.innerRadius:0.000} м, r2 = {cfg.outerRadius:0.000} м"
            : $"r = {cfg.outerRadius:0.000} м";

        string countPart = cfg.instanceCount > 1 ? $", {cfg.instanceCount} шт." : "";

        return $"m = {cfg.mass:0.000} кг{countPart}, {radiusPart}";
    }
}
