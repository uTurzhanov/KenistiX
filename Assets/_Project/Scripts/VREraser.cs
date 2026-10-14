using UnityEngine;

public class VREraser : MonoBehaviour
{
    [Header("Настройки луча")]
    [SerializeField] private Transform erasingSurface;
    [SerializeField] private float raycastDistance = 0.02f;
    [SerializeField] private LayerMask whiteboardLayer;

    [Header("Настройки стирания (Прямоугольник)")]
    [SerializeField] private int eraserWidth = 40;  // Ширина губки в пикселях
    [SerializeField] private int eraserHeight = 15; // Высота губки в пикселях
    [SerializeField] private Color32 eraseColor = new Color32(0, 0, 0, 0);

    private bool _isTouchingLastFrame;
    private Vector2 _lastTouchUv;

    private void Update()
    {
        if (Physics.Raycast(erasingSurface.position, erasingSurface.forward, out RaycastHit hit, raycastDistance, whiteboardLayer))
        {
            if (hit.collider.TryGetComponent<Whiteboard>(out var whiteboard))
            {
                Vector2 currentTouchUv = hit.textureCoord;

                if (!_isTouchingLastFrame)
                {
                    whiteboard.DrawRectangle(currentTouchUv, eraserWidth, eraserHeight, eraseColor);
                }
                else
                {
                    EraseInterpolatedLine(whiteboard, _lastTouchUv, currentTouchUv);
                }

                _lastTouchUv = currentTouchUv;
                _isTouchingLastFrame = true;
                return;
            }
        }
        _isTouchingLastFrame = false;
    }

    private void EraseInterpolatedLine(Whiteboard whiteboard, Vector2 startUv, Vector2 endUv)
    {
        float distance = Vector2.Distance(startUv, endUv);
        // Рассчитываем шаги на основе самого маленького измерения губки, чтобы не было пробелов
        float minDimension = Mathf.Min(eraserWidth, eraserHeight);
        int steps = Mathf.CeilToInt(distance * (whiteboard.TextureSize / (minDimension / 2f))); 

        for (int i = 0; i <= steps; i++)
        {
            float t = (steps == 0) ? 0f : (float)i / steps;
            Vector2 lerpUv = Vector2.Lerp(startUv, endUv, t);
            whiteboard.DrawRectangle(lerpUv, eraserWidth, eraserHeight, eraseColor);
        }
    }
}