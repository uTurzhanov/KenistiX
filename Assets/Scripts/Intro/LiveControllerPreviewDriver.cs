using UnityEngine;
using UnityEngine.InputSystem;

namespace VRIntro
{
    /// <summary>
    /// Читает реальный ввод со стика игрока (через Input System) и в реальном времени
    /// наклоняет соответствующую кость 3D-модели контроллера в HUD — если игрок
    /// наклоняет физический стик, виртуальный стик в модели наклоняется точно так же.
    ///
    /// Настройка: перетащи сюда Input Action Reference нужного стика (Vector2,
    /// например "XRI LeftHand/Thumbstick" или аналогичный raw-экшен в твоём Input
    /// Actions ассете) и кость стика в 3D-модели (например left_b_thumbstick).
    /// </summary>
    public class LiveControllerPreviewDriver : MonoBehaviour
    {
        [Header("Левый стик (движение)")]
        [SerializeField] private InputActionReference leftStickAction;
        [SerializeField] private Transform leftStickBone;

        [Header("Правый стик (поворот)")]
        [SerializeField] private InputActionReference rightStickAction;
        [SerializeField] private Transform rightStickBone;

        [Header("Настройка наклона")]
        [Tooltip("Максимальный угол наклона стика в градусах при полном отклонении")]
        [SerializeField] private float maxTiltAngle = 20f;

        [Tooltip("Если наклон выглядит вывернутым не в ту сторону — переключи (инвертирует ось X)")]
        [SerializeField] private bool invertX = false;

        [Tooltip("Если наклон выглядит вывернутым не в ту сторону — переключи (инвертирует ось Y/Z)")]
        [SerializeField] private bool invertY = false;

        private Quaternion _leftBaseRotation;
        private Quaternion _rightBaseRotation;

        private void Awake()
        {
            if (leftStickBone != null) _leftBaseRotation = leftStickBone.localRotation;
            if (rightStickBone != null) _rightBaseRotation = rightStickBone.localRotation;
        }

        private void OnEnable()
        {
            leftStickAction?.action?.Enable();
            rightStickAction?.action?.Enable();
        }

        private void OnDisable()
        {
            // Не выключаем Enable() глобально на действии — оно может использоваться
            // ещё где-то в игре (тем же экшеном для реальной локомоции). Просто
            // возвращаем модель в нейтральное положение, чтобы не застыла в наклоне
            ResetToNeutral();
        }

        private void Update()
        {
            ApplyStick(leftStickAction, leftStickBone, _leftBaseRotation);
            ApplyStick(rightStickAction, rightStickBone, _rightBaseRotation);
        }

        private void ApplyStick(InputActionReference actionRef, Transform bone, Quaternion baseRotation)
        {
            if (actionRef == null || actionRef.action == null || bone == null) return;

            Vector2 value = actionRef.action.ReadValue<Vector2>();

            float x = invertX ? -value.x : value.x;
            float y = invertY ? -value.y : value.y;

            // Наклон вперёд/назад стика соответствует вращению вокруг локальной оси X,
            // наклон влево/вправо — вокруг локальной оси Z. Если в твоей модели кость
            // ориентирована иначе — поменяй местами оси ниже или подбери через invertX/invertY
            Quaternion tilt = Quaternion.Euler(y * maxTiltAngle, 0f, -x * maxTiltAngle);
            bone.localRotation = baseRotation * tilt;
        }

        private void ResetToNeutral()
        {
            if (leftStickBone != null) leftStickBone.localRotation = _leftBaseRotation;
            if (rightStickBone != null) rightStickBone.localRotation = _rightBaseRotation;
        }
    }
}