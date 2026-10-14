using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пересчитывает электрическую цепь методом узловых потенциалов (Modified Nodal Analysis),
/// что корректно поддерживает как последовательные, так и параллельные участки цепи.
///
/// МОДЕЛЬ ГРАФА:
/// В существующей системе компоненты делятся на два электрических типа:
///  - "Узлы" (Terminal, клеммы PowerSupplyUnit) — Resistance == 0, представляют точку
///    с единым потенциалом. У узла может быть 2+ соединений (точка ветвления).
///  - "Резистивные элементы" (ResistorComponent, WireComponent) — Resistance > 0,
///    у них ровно 2 соединения (2 вывода/лида) и они работают как РЕБРО графа
///    между двумя узлами, а не как узел сами по себе.
///
/// Поэтому при построении графа для решения мы "схлопываем" резистивные элементы
/// в рёбра между их соседями, а потенциалы/токи считаем для узлов. После решения
/// результат (Current/Voltage/Potential) записывается обратно на оригинальные
/// объекты — в том числе на сами резисторы/провода — чтобы UI продолжал работать
/// как раньше.
/// </summary>
public class KirchhoffManager : MonoBehaviour
{
    public static KirchhoffManager Instance { get; private set; }

    [Header("Главный прибор (Источник + Мультиметры)")]
    public PowerSupplyUnit MainPowerSupply;

    // Является ли цепь замкнутой по результату последнего пересчёта.
    // PowerSupplyUnit читает это, чтобы не показывать "застрявшие" старые значения.
    public bool IsCircuitClosed { get; private set; }

    // Флаг для отложенного пересчёта (см. RequestRecalculate) —
    // решает проблему реентерабельного вызова RecalculateCircuit из середины
    // самого RecalculateCircuit (например, когда резистор сгорает во время расчёта).
    private bool _recalculatePending = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void LateUpdate()
    {
        // Выполняем отложенный пересчёт ПОСЛЕ того, как текущий кадр/расчёт точно завершён.
        if (_recalculatePending)
        {
            _recalculatePending = false;
            RecalculateCircuit();
        }
    }

    /// <summary>
    /// Безопасный способ запросить пересчёт из кода, который может выполняться
    /// ВНУТРИ самого пересчёта (например, ResistorComponent.BurnOut()).
    /// Вместо немедленного рекурсивного вызова — откладывает до LateUpdate.
    /// </summary>
    public void RequestRecalculate()
    {
        _recalculatePending = true;
    }

    // Вызывается из SocketEventListener при любом изменении графа.
    // ВАЖНО: вызывать этот метод напрямую только когда вы точно знаете,
    // что не находитесь уже внутри стека вызовов пересчёта.
    // Из кода компонентов цепи (резистор, провод и т.п.) используйте RequestRecalculate().
    public void RecalculateCircuit()
    {
        if (MainPowerSupply == null)
        {
            Debug.LogError("[Kirchhoff] MainPowerSupply не назначен на KirchhoffManager! Проверьте Inspector.");
            return;
        }
        if (MainPowerSupply.PowerMinus == null || MainPowerSupply.PowerPlus == null)
        {
            Debug.LogError("[Kirchhoff] PowerPlus или PowerMinus не назначены на PowerSupplyUnit! Проверьте Inspector.");
            return;
        }

        ResetAllComponentsData();

        // --- ВРЕМЕННАЯ ДИАГНОСТИКА: печатаем все связи в графе ---
        // Удалите этот блок (или закомментируйте), когда разберётесь в проблеме.
        DebugPrintConnectionGraph();

        // 1. Волна IsMinusConnected — нужна только для UI вольтметра (проверка "подключён ли щуп")
        HashSet<CircuitComponent> visited = new HashSet<CircuitComponent>();
        MainPowerSupply.PowerMinus.PropagateMinusConnection(true, visited);

        IsCircuitClosed = MainPowerSupply.PowerPlus.IsMinusConnected;

        if (!IsCircuitClosed)
        {
            Debug.Log("Цепь разорвана.");
            return;
        }

        Debug.Log("Цепь замкнута! Запускаем узловой анализ.");
        SolveCircuit();
    }

    // ---------------------------------------------------------------
    //                      УЗЛОВОЙ АНАЛИЗ (MNA)
    // ---------------------------------------------------------------

    private class Edge
    {
        public CircuitComponent NodeA;
        public CircuitComponent NodeB;

        // Все физические объекты, представляющие это ребро (обычно один — резистор
        // или провод, чьё сопротивление и есть Resistance этого ребра). Используется,
        // чтобы записать Current/Voltage/Potential обратно на оригинальные объекты для UI.
        public List<CircuitComponent> Sources;
    }

    private void SolveCircuit()
    {
        // Собираем граф: каждый CircuitComponent — узел, каждая физическая связь — ребро.
        HashSet<CircuitComponent> nodes = new HashSet<CircuitComponent>();
        List<Edge> edges = new List<Edge>();

        CollectGraph(MainPowerSupply.PowerPlus, nodes, edges);

        if (!nodes.Contains(MainPowerSupply.PowerMinus))
        {
            Debug.LogWarning("[Kirchhoff] Минус не найден среди узлов графа после обхода.");
            return;
        }

        SolveOnePass(nodes, edges);
    }

    // Выполняет ОДНО решение системы уравнений для текущих Resistance всех рёбер
    // (включая уже обновлённые диоды) и записывает результат на компоненты.
    // Возвращает false, если решение не удалось (вырожденная матрица).
    private bool SolveOnePass(HashSet<CircuitComponent> nodes, List<Edge> edges)
    {
        CircuitComponent ground = MainPowerSupply.PowerMinus;
        CircuitComponent vSource = MainPowerSupply.PowerPlus;

        List<CircuitComponent> freeNodes = new List<CircuitComponent>();
        Dictionary<CircuitComponent, int> nodeIndex = new Dictionary<CircuitComponent, int>();

        foreach (var n in nodes)
        {
            if (n == ground || n == vSource) continue;
            nodeIndex[n] = freeNodes.Count;
            freeNodes.Add(n);
        }

        int unknownCount = freeNodes.Count;

        if (unknownCount == 0)
        {
            ApplyDirectShort(edges, vSource, ground);
            return true;
        }

        float[,] G = new float[unknownCount, unknownCount];
        float[] I = new float[unknownCount];

        foreach (var edge in edges)
        {
            float resistance = Mathf.Max(GetLinkResistance(edge.NodeA, edge.NodeB), MIN_RESISTANCE);
            float conductance = 1f / resistance;

            bool aIsGround = edge.NodeA == ground;
            bool aIsSource = edge.NodeA == vSource;
            bool bIsGround = edge.NodeB == ground;
            bool bIsSource = edge.NodeB == vSource;

            int ia = (!aIsGround && !aIsSource) ? nodeIndex[edge.NodeA] : -1;
            int ib = (!bIsGround && !bIsSource) ? nodeIndex[edge.NodeB] : -1;

            if (ia >= 0) G[ia, ia] += conductance;
            if (ib >= 0) G[ib, ib] += conductance;
            if (ia >= 0 && ib >= 0)
            {
                G[ia, ib] -= conductance;
                G[ib, ia] -= conductance;
            }

            if (ia >= 0 && bIsSource) I[ia] += conductance * MainPowerSupply.OutputVoltage;
            if (ib >= 0 && aIsSource) I[ib] += conductance * MainPowerSupply.OutputVoltage;
        }

        float[] potentials = GaussianSolve(G, I, unknownCount);

        if (potentials == null)
        {
            Debug.LogWarning("[Kirchhoff] Не удалось решить систему уравнений (вырожденная матрица).");
            return false;
        }

        vSource.Potential = MainPowerSupply.OutputVoltage;
        ground.Potential = 0f;
        for (int i = 0; i < unknownCount; i++)
        {
            freeNodes[i].Potential = potentials[i];
        }

        float totalCurrentFromSource = 0f;
        Dictionary<CircuitComponent, float> currentAccum = new Dictionary<CircuitComponent, float>();

        foreach (var edge in edges)
        {
            float va = GetNodePotential(edge.NodeA, vSource, ground, nodeIndex, potentials);
            float vb = GetNodePotential(edge.NodeB, vSource, ground, nodeIndex, potentials);

            float resistance = Mathf.Max(GetLinkResistance(edge.NodeA, edge.NodeB), MIN_RESISTANCE);
            float current = (va - vb) / resistance;
            float absCurrent = Mathf.Abs(current);

            // ВАЖНО: ток через ребро протекает через ОБА его конца — не только через
            // "резистивные" Sources (резистор/провод/диод), но и через обычные узлы
            // (Terminal, клеммы приборов), если они физически стоят в этой ветке цепи.
            // Например, AmmeterPlus/AmmeterMinus — это Terminal, а не резистивный компонент,
            // но амперметр должен показывать именно ток, протекающий через его клеммы.
            // Раньше Current обновлялся только для edge.Sources, из-за чего у обычных
            // терминалов в последовательной цепи Current оставался 0 после ResetAllComponentsData
            // и амперметр всегда показывал ноль, даже когда цепь была замкнута и реально работала.
            AccumulateCurrent(currentAccum, edge.NodeA, absCurrent);
            AccumulateCurrent(currentAccum, edge.NodeB, absCurrent);

            if (edge.NodeA == vSource || edge.NodeB == vSource)
            {
                totalCurrentFromSource += absCurrent;
            }
        }

        foreach (var kvp in currentAccum)
        {
            kvp.Key.UpdateElectricalData(kvp.Value);
        }

        MainPowerSupply.PowerPlus.Current = totalCurrentFromSource;
        return true;
    }

    // Накопление тока через узел. Узел может быть инцидентен нескольким рёбрам
    // (развилка) — в этом случае берём МАКСИМАЛЬНЫЙ ток среди его рёбер, а не сумму,
    // потому что для последовательного участка (Terminal с ровно 2 соединениями,
    // например AmmeterPlus-AmmeterMinus) оба смежных ребра несут практически
    // одинаковый ток, и складывать их было бы неверно (это не точка ветвления тока,
    // а просто два конца одной и той же последовательной цепи через этот узел).
    // Для настоящих точек ветвления (3+ соединений) физически корректная сумма токов
    // по всем ветвям равна входящему току только если считать со знаком (направление),
    // что для целей отображения на приборах (всегда модуль тока) не требуется —
    // максимум среди инцидентных рёбер достаточно надёжно отражает "ток через прибор"
    // для практических целей мультиметра.
    private void AccumulateCurrent(Dictionary<CircuitComponent, float> currentAccum, CircuitComponent node, float current)
    {
        if (!currentAccum.ContainsKey(node))
        {
            currentAccum[node] = current;
        }
        else
        {
            currentAccum[node] = Mathf.Max(currentAccum[node], current);
        }
    }

    private const float MIN_RESISTANCE = 0.0001f;

    private float GetNodePotential(
        CircuitComponent node,
        CircuitComponent vSource,
        CircuitComponent ground,
        Dictionary<CircuitComponent, int> nodeIndex,
        float[] potentials)
    {
        if (node == vSource) return MainPowerSupply.OutputVoltage;
        if (node == ground) return 0f;
        return potentials[nodeIndex[node]];
    }

    // Короткое замыкание: PowerPlus соединён с PowerMinus без свободных промежуточных узлов
    // (то есть напрямую одним или несколькими параллельными резистивными элементами).
    private void ApplyDirectShort(List<Edge> edges, CircuitComponent vSource, CircuitComponent ground)
    {
        float totalConductance = 0f;
        List<Edge> directEdges = new List<Edge>();

        foreach (var edge in edges)
        {
            bool connectsSourceToGround =
                (edge.NodeA == vSource && edge.NodeB == ground) ||
                (edge.NodeA == ground && edge.NodeB == vSource);

            if (connectsSourceToGround)
            {
                directEdges.Add(edge);
                totalConductance += 1f / Mathf.Max(GetLinkResistance(edge.NodeA, edge.NodeB), MIN_RESISTANCE);
            }
        }

        vSource.Potential = MainPowerSupply.OutputVoltage;
        ground.Potential = 0f;

        foreach (var edge in directEdges)
        {
            float resistance = Mathf.Max(GetLinkResistance(edge.NodeA, edge.NodeB), MIN_RESISTANCE);
            float current = MainPowerSupply.OutputVoltage / resistance;
            foreach (var src in edge.Sources)
            {
                src.UpdateElectricalData(current);
                src.Potential = MainPowerSupply.OutputVoltage;
            }
        }

        MainPowerSupply.PowerPlus.Current = MainPowerSupply.OutputVoltage * totalConductance;
    }

    // Метод Гаусса с выбором главного элемента (partial pivoting) для устойчивости
    private float[] GaussianSolve(float[,] G, float[] I, int n)
    {
        float[,] a = (float[,])G.Clone();
        float[] b = (float[])I.Clone();

        for (int col = 0; col < n; col++)
        {
            int pivotRow = col;
            float maxVal = Mathf.Abs(a[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                if (Mathf.Abs(a[row, col]) > maxVal)
                {
                    maxVal = Mathf.Abs(a[row, col]);
                    pivotRow = row;
                }
            }

            if (maxVal < 1e-9f) return null; // вырожденная матрица — изолированный узел/обрыв

            if (pivotRow != col)
            {
                for (int k = 0; k < n; k++)
                {
                    (a[col, k], a[pivotRow, k]) = (a[pivotRow, k], a[col, k]);
                }
                (b[col], b[pivotRow]) = (b[pivotRow], b[col]);
            }

            for (int row = col + 1; row < n; row++)
            {
                float factor = a[row, col] / a[col, col];
                for (int k = col; k < n; k++)
                {
                    a[row, k] -= factor * a[col, k];
                }
                b[row] -= factor * b[col];
            }
        }

        float[] result = new float[n];
        for (int row = n - 1; row >= 0; row--)
        {
            float sum = b[row];
            for (int k = row + 1; k < n; k++)
            {
                sum -= a[row, k] * result[k];
            }
            result[row] = sum / a[row, row];
        }

        return result;
    }

    // Обходит граф (BFS) от startNode. В этой модели КАЖДЫЙ CircuitComponent — это узел
    // (включая резисторы и провода), а КАЖДАЯ физическая связь (запись в ConnectedComponents)
    // становится одним ребром. Сопротивление ребра берётся с того конца связи, который физически
    // обладает сопротивлением (резистор, провод) — у обычных терминалов/клемм Resistance == 0.
    //
    // Это полностью устраняет необходимость различать "резистор как ребро" и "резистор как узел" —
    // больше не важно, сколько у резистора/провода соединений: 2 (последовательный участок),
    // 1 (висящий конец) или 3+ (развилка, например точка подключения вольтметра параллельно
    // основной цепи — совершенно нормальная и часто встречающаяся схема).
    private void CollectGraph(
        CircuitComponent startNode,
        HashSet<CircuitComponent> nodes,
        List<Edge> edges)
    {
        HashSet<CircuitComponent> visited = new HashSet<CircuitComponent>();
        // Чтобы не добавить одно и то же физическое ребро дважды (A->B и B->A — это одна связь),
        // отслеживаем уже обработанные пары.
        HashSet<(CircuitComponent, CircuitComponent)> processedPairs = new HashSet<(CircuitComponent, CircuitComponent)>();

        Queue<CircuitComponent> queue = new Queue<CircuitComponent>();
        queue.Enqueue(startNode);
        visited.Add(startNode);
        nodes.Add(startNode);

        while (queue.Count > 0)
        {
            CircuitComponent current = queue.Dequeue();

            foreach (var neighbor in current.ConnectedComponents)
            {
                // Связь A-B и B-A — одна и та же физическая связь. Обрабатываем её только один раз.
                var pairForward = (current, neighbor);
                var pairBackward = (neighbor, current);
                if (processedPairs.Contains(pairForward) || processedPairs.Contains(pairBackward))
                    continue;
                processedPairs.Add(pairForward);

                edges.Add(new Edge
                {
                    NodeA = current,
                    NodeB = neighbor,
                    Sources = GetResistiveSources(current, neighbor)
                });

                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    nodes.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    // Определяет сопротивление одной физической связи между двумя узлами.
    // Берём сопротивление с той стороны, которая физически обладает сопротивлением
    // (любой "резистивный" компонент — резистор, провод, катушка, лампа, диод, LED).
    // Если сопротивление есть с обеих сторон — складываем (физически корректно
    // для последовательного соединения на одном ребре).
    private float GetLinkResistance(CircuitComponent a, CircuitComponent b)
    {
        float r = 0f;

        if (IsResistiveComponent(a)) r += (a.Resistance);
        else if (IsResistiveComponent(b)) r += (b.Resistance);

        return r;
    }

    // Возвращает список оригинальных объектов, чьи UI-данные (Current/Voltage/Potential)
    // нужно обновить по результату расчёта этого ребра.
    private List<CircuitComponent> GetResistiveSources(CircuitComponent a, CircuitComponent b)
    {
        List<CircuitComponent> sources = new List<CircuitComponent>();
        if (IsResistiveComponent(a)) sources.Add(a);
        if (IsResistiveComponent(b)) sources.Add(b);
        return sources;
    }

    // Любой компонент, у которого Resistance физически означает что-то (в отличие от
    // терминалов/клемм с Resistance == 0 по умолчанию).
    private bool IsResistiveComponent(CircuitComponent c)
    {
        // ВАЖНО: DiodComponent добавлен сюда. Без этого его поле Resistance
        // (которое DiodComponent сам обновляет по модели Шокли) полностью
        // игнорировалось бы решателем — GetLinkResistance возвращал бы 0,
        // и любая ветвь с диодом считалась бы почти коротким замыканием
        // независимо от реального тока через диод.
        return c is ResistorComponent || c is WireComponent || c is DiodComponent || c is LedComponent || c is LampComponent || c is DiodeComponent;
    }

    // --- ВРЕМЕННАЯ ДИАГНОСТИКА ---
    // Печатает в консоль ПОЛНЫЙ список связей всех CircuitComponent на сцене,
    // чтобы можно было увидеть реальную топологию графа и найти разрыв.
    private void DebugPrintConnectionGraph()
    {
        CircuitComponent[] allComps = FindObjectsByType<CircuitComponent>(FindObjectsSortMode.None);
        Debug.Log($"[ДИАГНОСТИКА] Всего CircuitComponent на сцене: {allComps.Length}");
        foreach (var comp in allComps)
        {
            string neighbors = comp.ConnectedComponents.Count == 0
                ? "(нет связей)"
                : string.Join(", ", comp.ConnectedComponents.ConvertAll(c => $"{c.gameObject.name}{GetRoleTag(c)}[id:{c.GetInstanceID()}]"));

            Debug.Log($"[ДИАГНОСТИКА] {comp.gameObject.name}{GetRoleTag(comp)}[id:{comp.GetInstanceID()}] (тип: {comp.GetType().Name}, R={comp.Resistance}) -> связан с: {neighbors}");
        }
    }

    // Возвращает тег роли компонента (если он назначен на одно из полей PowerSupplyUnit),
    // и уникальный InstanceID — чтобы различать объекты с одинаковым именем (например, несколько "Plus"/"Minus").
    private string GetRoleTag(CircuitComponent comp)
    {
        if (comp == MainPowerSupply.PowerPlus) return " [POWER_PLUS]";
        if (comp == MainPowerSupply.PowerMinus) return " [POWER_MINUS]";
        if (comp == MainPowerSupply.AmmeterPlus) return " [AMMETER_PLUS]";
        if (comp == MainPowerSupply.AmmeterMinus) return " [AMMETER_MINUS]";
        if (comp == MainPowerSupply.VoltmeterPlus) return " [VOLTMETER_PLUS]";
        if (comp == MainPowerSupply.VoltmeterMinus) return " [VOLTMETER_MINUS]";
        return "";
    }

    private void ResetAllComponentsData()
    {
        CircuitComponent[] allComps = FindObjectsByType<CircuitComponent>(FindObjectsSortMode.None);
        foreach (var comp in allComps)
        {
            comp.Current = 0f;
            comp.Voltage = 0f;
            comp.Potential = 0f;
            comp.IsMinusConnected = false;
        }
    }
}
