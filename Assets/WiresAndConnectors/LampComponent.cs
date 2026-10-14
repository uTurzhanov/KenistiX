using UnityEngine;

public class LampComponent : CircuitComponent
{
    [Header("Lamp Settings")]
    public float ColdResistance = 10f;
    public float HotResistance = 100f;
    public float NominalCurrent = 0.3f;
    public float MaxForwardCurrent = 0.8f;
    public float MaxResistance = 9999999f;
    public bool IsBurnedOut = false;

    [Header("Visual Settings")]
    public Renderer LampRenderer;
    public Color HotColor = new Color(1f, 0.85f, 0.4f);
    public float MaxEmissionIntensity = 3f;

  
    [Header("Glow Animation")]
    public float WarmUpTime = 0.4f;
    public float CoolDownTime = 1.2f;
// Новый параметр:
    [Tooltip("Время затухания свечения при полном отключении питания")]
    public float PowerOffDecayTime = 0.5f;

    private MaterialPropertyBlock _propBlock;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorID     = Shader.PropertyToID("_BaseColor");

    // Целевой уровень свечения — определяется током (0..1), мгновенно
    private float _targetGlow = 0f;
    // Текущий визуальный уровень — плавно догоняет target в Update
    private float _visualGlow = 0f;
    public ParticleSystem BurnSparkEffect;

    

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();

        if (IsBurnedOut)
        {
            Resistance = MaxResistance;
            ApplyGlow(0f);
            return;
        }
        BurnSparkEffect = BurnSparkEffect.GetComponent<ParticleSystem>();
        // Электрика сразу в установившемся состоянии — не ждём прогрева
        Resistance = HotResistance;
        _targetGlow = 0f;  // визуально пока тёмная, разгорится когда пойдёт ток
        ApplyGlow(0f);
    }

    private void Update()
    {
        // 1. ПРИНУДИТЕЛЬНЫЙ СБРОС: 
        // Если цепь разорвана (определяем по KirchhoffManager), обнуляем напряжение
        bool isCircuitClosed = KirchhoffManager.Instance != null && KirchhoffManager.Instance.IsCircuitClosed;
        
        float currentVoltage = isCircuitClosed ? Voltage : 0f;
        
        // 2. Расчет цели свечения
        _targetGlow = Mathf.Clamp01(Mathf.Abs(currentVoltage) / NominalCurrent);

        // 3. Анимация
        float speed;
        if (_targetGlow > _visualGlow)
        {
            speed = 1f / WarmUpTime;
        }
        else
        {
            // Используем DecayTime, если питание пропало
            speed = (_targetGlow <= 0.001f) 
                ? 1f / PowerOffDecayTime 
                : 1f / CoolDownTime;
        }

        _visualGlow = Mathf.MoveTowards(_visualGlow, _targetGlow, speed * Time.deltaTime);
        ApplyGlow(_visualGlow);
    }

    public override void UpdateElectricalData(float newCurrent)
    {
        if (IsBurnedOut) 
        { 
            Resistance = MaxResistance; 
            _targetGlow = 0f; // Гасим лампу при сгорании
            base.UpdateElectricalData(0f); 
            return; 
        }

        float currentMagnitude = Mathf.Abs(newCurrent);
    
        // 1. Нормализуем ток для расчета сопротивления и свечения
        float t = Mathf.Clamp01(currentMagnitude / NominalCurrent);
    
        // 2. Расчет сопротивления (нелинейный)
        float nonLinearT = Mathf.Pow(t, 0.5f); 
        float newResistance = ColdResistance + (HotResistance - ColdResistance) * nonLinearT;

        // 3. ОБНОВЛЯЕМ ЦЕЛЬ СВЕЧЕНИЯ
        // Теперь свечение напрямую зависит от тока в текущей итерации расчёта
        _targetGlow = t; 

        // 4. Логика пересчета цепи
        if (Mathf.Abs(Resistance - newResistance) > (newResistance * 0.01f))
        {
            Resistance = newResistance;
            KirchhoffManager.Instance.RequestRecalculate();
            return; 
        }

        base.UpdateElectricalData(newCurrent);
    }

    private void ApplyGlow(float level)
    {
        if (LampRenderer == null) return;

        Color baseColor = Color.Lerp(Color.black, HotColor, level);
        float emissionIntensity = Mathf.Pow(level, 2f) * MaxEmissionIntensity;
        Color emissionColor = HotColor * emissionIntensity;

        LampRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(BaseColorID, baseColor);
        _propBlock.SetColor(EmissionColorID, emissionColor);
        LampRenderer.SetPropertyBlock(_propBlock);
    }

    private void BurnOut()
    {
        IsBurnedOut = true;
        Resistance = MaxResistance;
        _targetGlow = 0f;
        Debug.LogWarning($"{gameObject.name}: лампочка перегорела!");
        if (BurnSparkEffect != null)
        {
            // Если партиклы настроены на Loop, они будут играть постоянно.
            // Если они должны выстрелить один раз, просто Play().
            BurnSparkEffect.Play();
        }
        KirchhoffManager.Instance.RequestRecalculate();
    }
}