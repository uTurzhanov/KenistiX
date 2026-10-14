using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Слегка изгибает ТОЛЬКО фоновую подложку панели (свой собственный Image-меш) в виде пологой
/// дуги - декоративный эффект. Текст/список, лежащие поверх этой подложки как дочерние объекты,
/// НЕ затрагиваются - у них свои собственные меши, этот эффект их не трогает вообще.
///
/// Полноценное искривление ВСЕГО содержимого (включая сам текст) - гораздо более сложная и хрупкая
/// задача (TextMeshPro не подхватывает такие эффекты так же просто, как Image, нужен отдельный
/// механизм искажения текстового меша с риском проблем на переносах строк/некоторых углах) - если
/// понадобится именно это, лучше делать отдельным, самостоятельным скриптом.
///
/// Повесить как ДОПОЛНИТЕЛЬНЫЙ компонент рядом с Image на фоне TutorialPanel (Add Component на
/// том же объекте, где уже есть Image).
/// </summary>
[RequireComponent(typeof(Graphic))]
public class CurvedPanelBackground : BaseMeshEffect
{
    [Tooltip("Насколько сильно изгибать - суммарный угол дуги по всей ширине панели, градусы. 0 = плоско.")]
    [Range(0f, 30f)]
    [SerializeField] private float curveAngle = 8f;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || curveAngle <= 0f)
            return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);

        float halfWidth = GetHalfWidth(verts);
        if (halfWidth <= 0f)
            return;

        float halfAngleRad = curveAngle * 0.5f * Mathf.Deg2Rad;
        float radius = halfWidth / Mathf.Sin(Mathf.Max(halfAngleRad, 0.001f));

        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex v = verts[i];

            float xNorm = Mathf.Clamp(v.position.x / halfWidth, -1f, 1f); // -1..1 от центра к краю
            float angle = xNorm * halfAngleRad;

            float newX = Mathf.Sin(angle) * radius;
            float newZ = (Mathf.Cos(angle) - 1f) * radius; // прогиб назад по Z у краёв

            v.position = new Vector3(newX, v.position.y, newZ);
            verts[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    private float GetHalfWidth(List<UIVertex> verts)
    {
        float minX = float.MaxValue, maxX = float.MinValue;

        foreach (UIVertex v in verts)
        {
            if (v.position.x < minX) minX = v.position.x;
            if (v.position.x > maxX) maxX = v.position.x;
        }

        return (maxX - minX) * 0.5f;
    }
}
