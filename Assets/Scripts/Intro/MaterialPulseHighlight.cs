using UnityEngine;

namespace VRIntro
{
    /// <summary>
    /// Пульсирует эмиссию (свечение) конкретного материала на 3D-модели контроллера —
    /// используется, когда нужная кнопка/стик имеет свой отдельный материал.
    ///
    /// Вешать НЕПОСРЕДСТВЕННО на тот GameObject, у которого в Renderer есть материал
    /// нужной кнопки. Если модель — один меш с несколькими материалами в списке,
    /// укажи Material Index нужного слота (какой именно — видно в Renderer → Materials
    /// в инспекторе, слоты пронумерованы сверху вниз с 0).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class MaterialPulseHighlight : MonoBehaviour
    {
        [Tooltip("Индекс материала в списке Renderer.materials, который нужно подсвечивать (0 = первый слот)")]
        [SerializeField] private int materialIndex = 0;

        [Tooltip("Цвет свечения при подсветке")]
        [SerializeField] private Color highlightColor = Color.yellow;

        [Tooltip("Сколько пульсаций в секунду")]
        [SerializeField] private float speed = 1.5f;

        [Tooltip("Максимальная интенсивность свечения (яркость на пике пульсации)")]
        [SerializeField] private float maxIntensity = 2f;

        private Renderer _renderer;
        private Material _instanceMaterial;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            // Обращение к .materials (а не .sharedMaterial) создаёт уникальные копии —
            // это важно, чтобы подсветка не влияла на другие копии контроллера в сцене
            var mats = _renderer.materials;

            if (materialIndex < 0 || materialIndex >= mats.Length)
            {
                Debug.LogWarning($"[MaterialPulseHighlight] materialIndex={materialIndex} вне диапазона " +
                                  $"(у Renderer всего {mats.Length} материалов) на объекте {name}.");
                enabled = false;
                return;
            }

            _instanceMaterial = mats[materialIndex];
            _instanceMaterial.EnableKeyword("_EMISSION");
        }

        private void Update()
        {
            if (_instanceMaterial == null) return;

            // 0..1, плавно туда-обратно
            float t = (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            _instanceMaterial.SetColor(EmissionColorId, highlightColor * (t * maxIntensity));
        }

        private void OnDestroy()
        {
            // Подчищаем инстанс материала, чтобы не плодить утечки при частом создании/удалении объекта
            if (_instanceMaterial != null)
                Destroy(_instanceMaterial);
        }
    }
}