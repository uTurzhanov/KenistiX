using UnityEngine;

public class VRMarker : MonoBehaviour
{
    [Header("Настройки луча")]
    [SerializeField] private Transform tip;             // Пустой объект на кончике пишущего узла маркера
    [SerializeField] private float raycastDistance = 0.02f; // Расстояние взаимодействия (2 см)
    [SerializeField] private LayerMask whiteboardLayer; // Слой доски для оптимизации Raycast

    [Header("Настройки рисования")]
    [SerializeField] private int brushSize = 6;
    [SerializeField] private Color32 brushColor = Color.black;

    private Whiteboard _currentWhiteboard;
    private bool _isTouchingLastFrame;
    private Vector2 _lastTouchUv;

    private void Update()
    {
        // Пускаем короткий луч вперед из кончика маркера
        if (Physics.Raycast(tip.position, tip.forward, out RaycastHit hit, raycastDistance, whiteboardLayer))
        {
            if (hit.collider.TryGetComponent<Whiteboard>(out var whiteboard))
            {
                Vector2 currentTouchUv = hit.textureCoord; // Получаем точные UV-координаты точки касания

                if (!_isTouchingLastFrame)
                {
                    // Первое касание — просто ставим точку
                    whiteboard.DrawCircle(currentTouchUv, brushSize, brushColor);
                }
                else
                {
                    // Если ведем линию — интерполируем пиксели между старой и новой позицией
                    DrawInterpolatedLine(whiteboard, _lastTouchUv, currentTouchUv);
                }

                _lastTouchUv = currentTouchUv;
                _isTouchingLastFrame = true;
                return;
            }
        }

        // Если маркер оторвали от доски
        _isTouchingLastFrame = false;
    }

    private void DrawInterpolatedLine(Whiteboard whiteboard, Vector2 startUv, Vector2 endUv)
    {
        // Вычисляем количество шагов интерполяции на основе дистанции в пикселях
        float distance = Vector2.Distance(startUv, endUv);
        int steps = Mathf.CeilToInt(distance * whiteboard.TextureSize);

        for (int i = 0; i <= steps; i++)
        {
            float t = (steps == 0) ? 0f : (float)i / steps;
            Vector2 lerpUv = Vector2.Lerp(startUv, endUv, t);
            whiteboard.DrawCircle(lerpUv, brushSize, brushColor);
        }
    }
}