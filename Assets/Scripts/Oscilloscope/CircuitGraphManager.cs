using System.Collections.Generic;
using UnityEngine;

public class CircuitGraphManager : MonoBehaviour
{
    public static CircuitGraphManager Instance { get; private set; }

    [SerializeField] private ACCircuitSolver solver;

    [Header("Клеммы приборов (по 2 на каждый элемент)")]
    [SerializeField] private CircuitTerminal genTerm1;
    [SerializeField] private CircuitTerminal genTerm2;

    [SerializeField] private CircuitTerminal ampTerm1;
    [SerializeField] private CircuitTerminal ampTerm2;

    [SerializeField] private CircuitTerminal resTerm1;
    [SerializeField] private CircuitTerminal resTerm2;

    [SerializeField] private CircuitTerminal indTerm1;
    [SerializeField] private CircuitTerminal indTerm2;

    [SerializeField] private CircuitTerminal capTerm1;
    [SerializeField] private CircuitTerminal capTerm2;

    private readonly List<CircuitPatchWire> _wires = new List<CircuitPatchWire>();
    private readonly Dictionary<CircuitTerminal, CircuitTerminal> _parent = new Dictionary<CircuitTerminal, CircuitTerminal>();

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterWire(CircuitPatchWire wire)
    {
        if (!_wires.Contains(wire))
        {
            _wires.Add(wire);
            EvaluateTopology();
        }
    }

    public void UnregisterWire(CircuitPatchWire wire)
    {
        if (_wires.Remove(wire))
            EvaluateTopology();
    }

    public void EvaluateTopology()
    {
        if (solver == null) return;

        // 1. Строим группы электрически связанных клемм (узлы)
        _parent.Clear();
        CircuitTerminal[] allTerms = { genTerm1, genTerm2, ampTerm1, ampTerm2, resTerm1, resTerm2, indTerm1, indTerm2, capTerm1, capTerm2 };
        foreach (var t in allTerms)
        {
            if (t != null) _parent[t] = t;
        }

        foreach (var wire in _wires)
        {
            if (wire != null && wire.IsConnectedBothEnds)
                Union(wire.TerminalA, wire.TerminalB);
        }

        // 2. Проверяем включение амперметра последовательно с генератором
        var g1 = Find(genTerm1);
        var g2 = Find(genTerm2);
        var a1 = Find(ampTerm1);
        var a2 = Find(ampTerm2);

        if (g1 == g2 || a1 == a2)
        {
            solver.currentTopology = CircuitTopology.Open; // Короткое замыкание источника или амперметра
            return;
        }

        CircuitTerminal feedNet = null;
        CircuitTerminal returnNet = null;

        // Определяем питающий узел (после амперметра) и обратный узел (второй полюс генератора)
        if (g1 == a1) { feedNet = a2; returnNet = g2; }
        else if (g1 == a2) { feedNet = a1; returnNet = g2; }
        else if (g2 == a1) { feedNet = a2; returnNet = g1; }
        else if (g2 == a2) { feedNet = a1; returnNet = g1; }

        if (feedNet == null || feedNet == returnNet)
        {
            solver.currentTopology = CircuitTopology.Open;
            return;
        }

        // Получаем узлы элементов R, L, C
        var r1 = Find(resTerm1); var r2 = Find(resTerm2);
        var l1 = Find(indTerm1); var l2 = Find(indTerm2);
        var c1 = Find(capTerm1); var c2 = Find(capTerm2);

        // 3. Проверка ПАРАЛЛЕЛЬНОГО контура (C || L между feedNet и returnNet, R отключен)
        bool cAcross = IsConnectedBetween(c1, c2, feedNet, returnNet);
        bool lAcross = IsConnectedBetween(l1, l2, feedNet, returnNet);
        bool rDisconnected = (r1 == r2) || (r1 != feedNet && r1 != returnNet && r2 != feedNet && r2 != returnNet);

        if (cAcross && lAcross && rDisconnected)
        {
            solver.currentTopology = CircuitTopology.Parallel;
            return;
        }

        // 4. Проверка ПОСЛЕДОВАТЕЛЬНОГО контура (цепочка из R, L, C в любом порядке между feedNet и returnNet)
        if (IsSeriesChain(feedNet, returnNet, new[] { (r1, r2), (l1, l2), (c1, c2) }))
        {
            solver.currentTopology = CircuitTopology.Series;
            return;
        }

        solver.currentTopology = CircuitTopology.Open;
    }

    private bool IsConnectedBetween(CircuitTerminal t1, CircuitTerminal t2, CircuitTerminal netA, CircuitTerminal netB)
    {
        return (t1 == netA && t2 == netB) || (t1 == netB && t2 == netA);
    }

    private bool IsSeriesChain(CircuitTerminal startNet, CircuitTerminal endNet, (CircuitTerminal a, CircuitTerminal b)[] edges)
    {
        bool[] used = new bool[edges.Length];
        CircuitTerminal current = startNet;

        for (int step = 0; step < edges.Length; step++)
        {
            int foundIndex = -1;
            CircuitTerminal nextNet = null;

            for (int i = 0; i < edges.Length; i++)
            {
                if (used[i]) continue;
                var (u, v) = edges[i];
                if (u == v) return false; // Элемент закорочен сам на себя

                if (u == current || v == current)
                {
                    if (foundIndex != -1) return false; // Обнаружено ветвление (не последовательная цепь)
                    foundIndex = i;
                    nextNet = (u == current) ? v : u;
                }
            }

            if (foundIndex == -1) return false; // Обрыв цепи
            used[foundIndex] = true;
            current = nextNet;

            // Не должны прийти в конечный узел раньше последнего шага
            if (step < edges.Length - 1 && current == endNet) return false;
        }

        return current == endNet;
    }

    private CircuitTerminal Find(CircuitTerminal x)
    {
        if (x == null) return null;
        if (!_parent.ContainsKey(x)) _parent[x] = x;
        if (_parent[x] != x)
            _parent[x] = Find(_parent[x]);
        return _parent[x];
    }

    private void Union(CircuitTerminal a, CircuitTerminal b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (rootA != null && rootB != null && rootA != rootB)
            _parent[rootA] = rootB;
    }

    public float GetVoltageBetweenTerminals(CircuitTerminal termA, CircuitTerminal termB)
    {
        if (solver == null || termA == null || termB == null) return 0f;
        if (solver.currentTopology == CircuitTopology.Open) return 0f;

        var netA = Find(termA);
        var netB = Find(termB);
        if (netA == null || netB == null || netA == netB) return 0f; // Щупы на одном проводе (0 В)

        var g1 = Find(genTerm1);
        var g2 = Find(genTerm2);
        var a1 = Find(ampTerm1);
        var a2 = Find(ampTerm2);

        // Определяем питающий узел (после амперметра) и обратный узел
        CircuitTerminal feedNet = null;
        CircuitTerminal returnNet = null;
        if (g1 == a1) { feedNet = a2; returnNet = g2; }
        else if (g1 == a2) { feedNet = a1; returnNet = g2; }
        else if (g2 == a1) { feedNet = a2; returnNet = g1; }
        else if (g2 == a2) { feedNet = a1; returnNet = g1; }

        // 1. Общее напряжение на входе контура (U_изм)
        if (feedNet != null && returnNet != null && IsConnectedBetween(netA, netB, feedNet, returnNet))
            return solver.TotalVoltageRms;

        // 2. Напряжение на резисторе (U_R)
        if (IsConnectedBetween(netA, netB, Find(resTerm1), Find(resTerm2)))
            return solver.ResistorVoltageRms;

        // 3. Напряжение на конденсаторе (U_C)
        if (IsConnectedBetween(netA, netB, Find(capTerm1), Find(capTerm2)))
            return solver.CapacitorVoltageRms;

        // 4. Напряжение на катушке (U_L)
        if (IsConnectedBetween(netA, netB, Find(indTerm1), Find(indTerm2)))
            return solver.InductorVoltageRms;

        return 0f;
    }
}