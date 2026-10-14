using UnityEngine;

public class Whiteboard : MonoBehaviour
{
    [Header("Настройки текстуры")]
    [SerializeField] private int textureSize = 1024; // Оптимально для Quest 3 (четко и не ест много памяти)
    [SerializeField] private Color32 backgroundColor = new Color32(0, 0, 0, 0);

    private Texture2D _texture;
    private MeshRenderer _meshRenderer;
    private Color32[] _blankPixels;

    public int TextureSize => textureSize;

    private void Start()
    {
        _meshRenderer = GetComponent<MeshRenderer>();

        // Создаем динамическую текстуру без мипмапов для экономии ресурсов
        _texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        _meshRenderer.material.mainTexture = _texture;

        // Заготавливаем массив дефолтных пикселей для быстрой очистки
        _blankPixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < _blankPixels.Length; i++)
        {
            _blankPixels[i] = backgroundColor;
        }

        Clear();
    }

    // Метод полной очистки доски (для губки или кнопки сброса)
    public void Clear()
    {
        _texture.SetPixels32(_blankPixels);
        _texture.Apply(false);
    }

    // Метод рисования круга по UV-координатам
    public void DrawCircle(Vector2 uv, int radius, Color32 color)
    {
        int centerX = (int)(uv.x * textureSize);
        int centerY = (int)(uv.y * textureSize);

        // Проходим только по квадрату, в который вписана кисть, а не по всей текстуре
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius) // Проверка нахождение пикселя внутри радиуса круга
                {
                    int pixelX = centerX + x;
                    int pixelY = centerY + y;

                    // Защита от выхода за границы текстуры
                    if (pixelX >= 0 && pixelX < textureSize && pixelY >= 0 && pixelY < textureSize)
                    {
                        _texture.SetPixel(pixelX, pixelY, color);
                    }
                }
            }
        }
        
        // Применяем изменения без обновления мипмапов (быстрый Apply)
        _texture.Apply(false);
    }

    // Метод стирания прямоугольной областью
    public void DrawRectangle(Vector2 uv, int width, int height, Color32 color)
    {
        int centerX = (int)(uv.x * textureSize);
        int centerY = (int)(uv.y * textureSize);

        int halfWidth = width / 2;
        int halfHeight = height / 2;

        // Проходим по прямоугольнику вместо круга
        for (int x = -halfWidth; x <= halfWidth; x++)
        {
            for (int y = -halfHeight; y <= halfHeight; y++)
            {
                int pixelX = centerX + x;
                int pixelY = centerY + y;

                // Защита от выхода за границы текстуры
                if (pixelX >= 0 && pixelX < textureSize && pixelY >= 0 && pixelY < textureSize)
                {
                    _texture.SetPixel(pixelX, pixelY, color);
                }
            }
        }
        
        _texture.Apply(false);
    }
}