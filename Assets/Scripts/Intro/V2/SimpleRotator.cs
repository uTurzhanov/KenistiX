using UnityEngine;

/// <summary>
/// ѕростое посто€нное вращение объекта вокруг заданной оси - дл€ демонстрационных 3D-моделей
/// (например, ControllerModelsDisplay на шагах 3-4), чтобы показать модель со всех сторон,
/// не заморачива€сь с Animator/Animation Clip.
///
/// ѕовесить пр€мо на объект, который должен вращатьс€ (либо на родител€, если контроллеров
/// несколько и они должны крутитьс€ вместе как одна группа).
/// </summary>
public class SimpleRotator : MonoBehaviour
{
    [Tooltip("ќсь вращени€ в локальных координатах объекта. ѕо умолчанию - вокруг вертикали (Y).")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Tooltip("—корость вращени€ в градусах в секунду. 360 / requiredSeconds даст один полный оборот " +
             "за requiredSeconds - например, 90 = полный оборот за 4 секунды.")]
    [SerializeField] private float degreesPerSecond = 90f;

    private void Update()
    {
        transform.Rotate(rotationAxis.normalized, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}