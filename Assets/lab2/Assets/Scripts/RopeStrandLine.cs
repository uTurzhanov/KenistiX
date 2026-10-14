using UnityEngine;

/// <summary>
/// Простой и надёжный способ отрисовки нити трифилярного подвеса:
/// прямая линия между фиксированной верхней точкой (на неподвижном
/// верхнем диске) и подвижной нижней точкой (на вращающейся платформе).
///
/// Не зависит от пивота/ориентации 3D-меша - просто рисует линию между
/// двумя мировыми точками каждый кадр. Толщина и материал линии
/// настраиваются в инспекторе компонента LineRenderer.
///
/// ВАЖНО: объект с этим скриптом не должен быть child-объектом
/// вращающейся платформы. Сами якоря (topAnchor/bottomAnchor) - да,
/// они находятся внутри своих родителей и поэтому автоматически
/// получают правильное мировое положение от движка.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class RopeStrandLine : MonoBehaviour
{
    [Tooltip("Точка на неподвижном верхнем диске. Не двигается.")]
    public Transform topAnchor;

    [Tooltip("Точка на вращающейся платформе. Двигается вместе с ней.")]
    public Transform bottomAnchor;

    private LineRenderer _line;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;
    }

    private void LateUpdate()
    {
        if (topAnchor == null || bottomAnchor == null) return;

        _line.SetPosition(0, topAnchor.position);
        _line.SetPosition(1, bottomAnchor.position);
    }
}
