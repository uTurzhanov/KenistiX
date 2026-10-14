using UnityEngine;
using TMPro;
using System.Text;
using System.Globalization;

public class DigitalCaliper : MonoBehaviour
{
    [Header("Ссылки на объекты")]
    [SerializeField] private Transform zeroPoint;   // Пустышка на неподвижной губке
    [SerializeField] private Transform sliderPoint; // Пустышка на подвижной губке
    [SerializeField] private TextMeshPro display;   // Текстовый экран

    [Header("Настройки прибора")]
    [SerializeField] private float maxErrorMm = 0.03f; // Максимальная погрешность
    [SerializeField] private int maxMeasurementMm = 150; // Максимальная длина шкалы (15 см)

    [Header("Настройки аналогового дисплея")]
    [Tooltip("Сколько символов генерировать (Ширина видимого окна дисплея)")]
    [SerializeField] private int visibleWindowSize = 30;
    [Tooltip("Ширина одного символа. Подбирается под ваш шрифт, чтобы шкала не прыгала.")]
    [SerializeField] private float monospaceSpacingEm = 0.5f;
    [Tooltip("Абсолютный центр экрана (якорь от левого края). Меняйте, чтобы сдвинуть ВСЮ шкалу целиком.")]
    [SerializeField] private float fixedCenterEm = 8.5f;
    [Tooltip("Тонкая калибровка положения ТОЛЬКО красной стрелки.")]
    [SerializeField] private float pointerOffsetEm = 0f;
    [Tooltip("Показывать ли точную цифру под аналоговой шкалой? Для сдачи ставить false — иначе убивает чтение нониуса.")]
    [SerializeField] private bool showDigitalValue = true;

    [Header("Подсветка нониуса (0.1 мм)")]
    [Tooltip("Подсвечивать совпавшую риску нониуса зелёным. Номер риски студент всё равно считывает сам.")]
    [SerializeField] private bool highlightCoincident = true;
    [Tooltip("Показывать текстовую подсказку номера совпавшей риски. Для сдачи — false.")]
    [SerializeField] private bool showVernierHint = false;

    private float _currentError;
    private float _lastTrueDistMm;

    // Кэш перестройки: не трогаем текст каждый кадр (90 FPS), только при видимом изменении
    private float _lastBuiltExp = float.NaN;
    private int _lastStartIndex = int.MinValue;

    // Закэшированные полные строки основной шкалы
    private string _fullNumRow;
    private string _fullTickRow;
    private int _padding;

    // Переиспользуемые билдеры — без аллокаций каждый кадр
    private readonly StringBuilder _sbVernierTicks = new StringBuilder(512);
    private readonly StringBuilder _sbFinal = new StringBuilder(2048);

    private void Start()
    {
        if (display != null)
        {
            display.enableWordWrapping = false;
            display.richText = true;

            // Жестко привязываем начало координат к левому верхнему углу, чтобы <pos> работал корректно
            display.alignment = TextAlignmentOptions.TopLeft;
        }

        BuildAnalogScale();
        GenerateNewError();
    }

    private void GenerateNewError()
    {
        _currentError = Random.Range(-maxErrorMm, maxErrorMm);
    }

    // Красит один символ колонки курсора в красный. Вызывается только при перестройке текста — не каждый кадр.
    private static string MarkColumnRed(string row, int col)
    {
        if (string.IsNullOrEmpty(row) || col < 0 || col >= row.Length) return row;
        return row.Substring(0, col) + "<color=red>" + row[col] + "</color>" + row.Substring(col + 1);
    }

    private void BuildAnalogScale()
    {
        StringBuilder nums = new StringBuilder();
        StringBuilder ticks = new StringBuilder();

        for (int i = 0; i <= maxMeasurementMm; i += 10)
        {
            string numStr = i.ToString();
            nums.Append(numStr);
            nums.Append(new string(' ', 10 - numStr.Length));
            ticks.Append("|....:....");
        }

        _padding = visibleWindowSize + 2;
        _fullNumRow = new string(' ', _padding) + nums.ToString() + new string(' ', _padding);
        _fullTickRow = new string(' ', _padding) + ticks.ToString() + new string(' ', _padding);
    }

    private void Update()
    {
        if (zeroPoint == null || sliderPoint == null || display == null) return;

        float trueDistMeters = Vector3.Distance(zeroPoint.position, sliderPoint.position);
        float trueDistMm = trueDistMeters * 1000f;

        if (Mathf.Abs(trueDistMm - _lastTrueDistMm) > 0.1f)
        {
            GenerateNewError();
            _lastTrueDistMm = trueDistMm;
        }

        float finalExp = Mathf.Clamp(trueDistMm + _currentError, 0f, (float)maxMeasurementMm);

        // =========================================================
        // ТОЧНОЕ РАЗЛОЖЕНИЕ ДЛЯ НОНИУСА 0.1 мм
        // Сначала округляем до десятых, потом выделяем целое/десятые.
        // Так риска нониуса всегда совпадает с математикой прибора,
        // а не с усечённым Floor (12.96 -> 13.0 + риска 0, а не 12 + .96).
        // =========================================================
        int totalTenths = Mathf.Clamp(Mathf.RoundToInt(finalExp * 10f), 0, maxMeasurementMm * 10);
        int wholeMm = totalTenths / 10;
        int tenths = totalTenths % 10; // = индекс совпавшей риски нониуса (0..9)
        float fracShift = finalExp - wholeMm; // точный сдвиг шкалы относительно целого

        int startIndex = Mathf.Clamp(
            _padding + wholeMm - (visibleWindowSize / 2),
            0, _fullNumRow.Length - (visibleWindowSize + 1));

        // Перестраиваем текст только при видимом изменении:
        // порог 0.005 мм — вдвое меньше цены деления нониуса (0.1) и шага сдвига.
        if (!float.IsNaN(_lastBuiltExp)
            && Mathf.Abs(finalExp - _lastBuiltExp) < 0.005f
            && startIndex == _lastStartIndex)
            return;
        _lastBuiltExp = finalExp;
        _lastStartIndex = startIndex;

        // Rich Text мог выключить префаб/галка в инспекторе после Start —
        // без него все <color>/<pos> рисуются буквальным текстом. Форсим перед каждой перестройкой.
        if (!display.richText) display.richText = true;
        if (display.enableWordWrapping) display.enableWordWrapping = false;

        // Берем ровно столько текста, сколько влезает в окно
        string viewNums = _fullNumRow.Substring(startIndex, visibleWindowSize + 1);
        string viewTicks = _fullTickRow.Substring(startIndex, visibleWindowSize + 1);

        float leftShift = fracShift * monospaceSpacingEm;
        float trueVernierZeroEm = fixedCenterEm;

        // Базовые координаты для отрисовки основной шкалы
        float textPosEm = trueVernierZeroEm - ((visibleWindowSize / 2f) * monospaceSpacingEm) - leftShift;
        float pointerPosEm = trueVernierZeroEm + pointerOffsetEm;

        string textPosStr = textPosEm.ToString("F3", CultureInfo.InvariantCulture);

        // =========================================================
        // ГЕНЕРАЦИЯ ШКАЛЫ НОНИУСА (Десятые доли)
        // Классический нониус: 10 делений = 9 мм основной шкалы.
        // Физика совпадения: риска i стоит на wholeMm + frac + 0.9*i,
        // совпадает с целым миллиметром та, где frac + 0.9*i ~ целое,
        // т.е. i = десятые доли. Её подсвечиваем зелёным.
        // =========================================================
        _sbVernierTicks.Clear();

        // Классический нониус (10 делений = 9 мм основной шкалы)
        float vernierStepEm = 0.9f * monospaceSpacingEm;

        for (int i = 0; i <= 10; i++)
        {
            float posEm = trueVernierZeroEm + (i * vernierStepEm);
            string posStr = posEm.ToString("F3", CultureInfo.InvariantCulture);

            bool isZero = (i == 0);
            bool isHit = highlightCoincident && !isZero && (i == tenths || (tenths == 0 && i == 10));
            // Только hex + базовые имена: не все именованные цвета (lime и т.п.) понимает TMP-парсер
            string color = isZero ? "red" : (isHit ? "#00FF00" : "#00FFFF");

            _sbVernierTicks.Append($"<pos={posStr}em><color={color}>|</color>");
        }

        // =========================================================
        // СБОРКА ФИНАЛЬНОГО ТЕКСТА
        // =========================================================
        _sbFinal.Clear();
        _sbFinal.Append($"<mspace={monospaceSpacingEm.ToString(CultureInfo.InvariantCulture)}em><nobr>");

        // Курсор-волосок через ВСЕ строки табло (вместо стрелок — они съедали место и выталкивали текст за экран):
        // красная колонка в строках чисел и рисок + красная нулевая риска нониуса строго под ней.
        // Целые миллиметры считываются там, где волосок пересекает шкалу.
        // Цифр у нониуса нет: десятые = номер зелёной риски, считая от красной нулевой.
        // ВАЖНО: Floor, а не Round. Round переключал бы колонку уже на 0.5мм (на середине между рисками),
        // хотя волосок ещё не дошёл до следующей риски. Floor держит ячейку, СОДЕРЖАЩУЮ волосок:
        // переключение ровно на целом миллиметре, синхронно с нижней красной риской.
        int cursorCol = Mathf.Clamp(Mathf.FloorToInt((pointerPosEm - textPosEm) / monospaceSpacingEm), 0, visibleWindowSize);

        // Отрисовываем основную шкалу
        _sbFinal.AppendLine($"<pos={textPosStr}em>{MarkColumnRed(viewNums, cursorCol)}");
        _sbFinal.AppendLine($"<pos={textPosStr}em>{MarkColumnRed(viewTicks, cursorCol)}");

        // Неподвижная шкала нониуса: только риски (цифры убраны — не влезали в экранчик)
        _sbFinal.Append(_sbVernierTicks.ToString());

        if (showVernierHint)
        {
            _sbFinal.AppendLine();
            float hintPosEm = trueVernierZeroEm - ((visibleWindowSize / 2f) * monospaceSpacingEm);
            _sbFinal.Append($"<pos={hintPosEm.ToString(CultureInfo.InvariantCulture)}em><color=#AAAAAA>нониус: риска {tenths} → {wholeMm}.{tenths} мм</color>");
        }

        if (showDigitalValue)
        {
            _sbFinal.AppendLine();
            _sbFinal.AppendLine();
            string finalExpStr = finalExp.ToString("F2", CultureInfo.InvariantCulture);

            float hintPosEm = trueVernierZeroEm - ((visibleWindowSize / 2f) * monospaceSpacingEm);
            _sbFinal.Append($"<pos={hintPosEm.ToString(CultureInfo.InvariantCulture)}em><color=yellow>Подсказка: {finalExpStr} мм</color>");
        }

        _sbFinal.Append("</nobr></mspace>");
        display.text = _sbFinal.ToString();
    }
}
