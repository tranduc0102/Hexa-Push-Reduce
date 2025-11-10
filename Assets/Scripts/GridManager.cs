using DG.Tweening;
using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Grid _grid;
    [SerializeField] private CellHexa _cellPrefab;
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private BoxCollider _collider;

    public List<CellHexa> AllCells = new List<CellHexa>();

    private readonly Dictionary<(int q, int r), CellHexa> cellLookup = new();
    private readonly Dictionary<int, List<CellHexa>> columnCache = new();
    private readonly List<float> columnXs = new();
    private readonly HashSet<CellHexa> pendingCollectChecks = new();

    private readonly Queue<CellHexa> collectQueue = new();
    private bool isCollecting = false;

    #region GRID GENERATION

    [ContextMenu("Generate Grid")]
    public void GeneratorGrid(int width, int height, List<CellData> cells, float offSetZ)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        AllCells.Clear();
        cellLookup.Clear();
        transform.localEulerAngles = Vector3.zero;

        float cellWidth = 0.85f;
        float cellHeight = 0.75f;

        for (int r = -height; r < height; r++)
        {
            float offsetX = (r % 2 == 0) ? -cellWidth / 2f : 0f;

            for (int q = -width; q < width; q++)
            {
                var cellData = cells.Find(c => c.q == q && c.r == r);
                float x = q * cellWidth + offsetX;
                float z = r * cellHeight;
                Vector3 pos = new Vector3(x, 0f, z);

                var cell = Instantiate(_cellPrefab, pos, Quaternion.identity, transform);
                cell.InitCoords(q, r);
                cell.name = $"Cell_{q}_{r}";

                if (cellData != null && cellData.isHidden)
                {
                    cell.HideVisual();
                    continue;
                }

                AllCells.Add(cell);
                cellLookup[(q, r)] = cell;

                if (cellData?.spawnHexaData > 0)
                {
                    GamePlayManager.Instance.HexaSpawner.SpawnHexa(cell, cellData.spawnHexaData);
                }
            }
        }

        Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, 0, float.MinValue);

        foreach (var c in AllCells)
        {
            if (c.IsHidden) continue;
            Vector3 p = c.transform.localPosition;
            if (p.x < min.x) min.x = p.x;
            if (p.z < min.z) min.z = p.z;
            if (p.x > max.x) max.x = p.x;
            if (p.z > max.z) max.z = p.z;
        }

        Vector3 center = (min + max) / 2f;
        foreach (var c in AllCells)
            c.transform.localPosition -= center;

        transform.localEulerAngles = new Vector3(0f, 90f, 0f);
        transform.position = new Vector3(0, 0, offSetZ);
        _collider.size = new Vector3(width * 2 * 0.8f, 1, height * 5);

        BuildColumnCache();
    }

    private void BuildColumnCache()
    {
        columnXs.Clear();
        columnCache.Clear();

        foreach (var cell in AllCells)
        {
            if (cell.IsHidden) continue;
            float x = Mathf.Round(cell.transform.position.x * 100f) / 100f;
            if (!columnXs.Contains(x))
                columnXs.Add(x);
        }

        columnXs.Sort();

        for (int i = 0; i < columnXs.Count; i++)
        {
            float targetX = columnXs[i];
            var col = AllCells
                .Where(c => !c.IsHidden && Mathf.Abs(c.transform.position.x - targetX) < 0.01f)
                .OrderBy(c => c.transform.position.z)
                .ToList();
            columnCache[i] = col;
        }
    }

    #endregion

    #region COLUMN HANDLING

    public int GetNearestColumnX(float worldX)
    {
        if (columnXs.Count == 0)
            BuildColumnCache();

        float bestDist = float.MaxValue;
        int bestIndex = 0;

        for (int i = 0; i < columnXs.Count; i++)
        {
            float dist = Mathf.Abs(columnXs[i] - worldX);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public List<CellHexa> GetCellsInColumn(int columnIndex)
    {
        if (columnCache.Count == 0)
            BuildColumnCache();
        return columnCache[Mathf.Clamp(columnIndex, 0, columnXs.Count - 1)];
    }

    public bool InsertHexaInColumn(HexaItem hexa, int columnIndex, CellHexa targetCell)
    {
        if (targetCell == null) return false;

        var column = GetCellsInColumn(columnIndex);

        if (!targetCell.IsEmpty)
        {
            return ShiftColumnUp(hexa, column, targetCell);
        }

        hexa.MoveToCell(targetCell, moveDuration);
        CheckAndCollect(targetCell, moveDuration + 0.5f);
        return true;
    }

    private bool ShiftColumnUp(HexaItem hexa, List<CellHexa> column, CellHexa targetCell)
    {
        bool result = false;
        column.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

        int index = column.IndexOf(targetCell);
        List<CellHexa> movedCells = new();

        for (int i = column.Count - 1; i > index; i--)
        {
            var below = column[i - 1];
            var current = column[i];
            var top = targetCell;
            if(i - 2 > 0)
            {
                top = column[i - 2];
            }
            if (!below.IsEmpty && current.IsEmpty && (top == below || !top.IsEmpty))
            {
                var item = below.Item;
                item.MoveToCell(current, moveDuration);
                below.ClearItem();
                movedCells.Add(current);
                result = true;
            }
        }

        hexa.MoveToCell(targetCell, moveDuration);
        movedCells.Add(targetCell);

        DOVirtual.DelayedCall(moveDuration + 0.1f, () =>
        {
            foreach (var movedCell in movedCells)
            {
                CheckAndCollect(movedCell, 0.05f);
            }
        });

        return result;
    }
  


    #endregion

    #region COLLECT SYSTEM

    private void CheckAndCollect(CellHexa cell, float delay = 0f)
    {
        if (cell == null || cell.IsEmpty || cell.Item == null) return;
        pendingCollectChecks.Add(cell);
        DOVirtual.DelayedCall(delay, TryEnqueueCollect);
    }

    private void TryEnqueueCollect()
    {
        if (pendingCollectChecks.Count == 0)
            return;

        var groups = new List<(CellHexa cell, int number)>();
        var visited = new HashSet<CellHexa>();

        foreach (var cell in pendingCollectChecks)
        {
            if (cell == null || cell.IsEmpty || visited.Contains(cell))
                continue;

            var group = FloodFillSameType(cell);
            if (group.Count >= 3)
            {
                groups.Add((cell, cell.Item.Number));
                foreach (var g in group)
                    visited.Add(g);
            }
        }

        pendingCollectChecks.Clear();

        groups = groups.OrderByDescending(g => g.number).ToList();

        if (groups.Count > 0)
        {
            var topGroup = groups.First();
            if (!collectQueue.Contains(topGroup.cell))
                collectQueue.Enqueue(topGroup.cell);
        }

        if (!isCollecting)
            StartCoroutine(ProcessCollectQueue());
    }


    private IEnumerator ProcessCollectQueue()
    {
        isCollecting = true;

        while (collectQueue.Count > 0)
        {
            var cell = collectQueue.Dequeue();
            if (cell == null || cell.IsEmpty) continue;
            yield return CollectCascade(cell);
        }
        bool _lose = true;
        foreach(var i in AllCells)
        {
            if (i.IsEmpty)
            {
                _lose = false;
                break;
            }
        }
        isCollecting = false;
        if (_lose)
        {
            if (GamePlayManager.Instance.State == GamePlayManager.GameState.Playing)
            {
                GamePlayManager.Instance.State = GamePlayManager.GameState.Lose;
                AudioManager.Instance.PlayLose();
                UIManager.Instance.ShowResult(false, true);
            }
        }
        else
        {
            GamePlayManager.Instance.SpawnNextHexa(true);
        }
    }

    private IEnumerator CollectCascade(CellHexa startCell)
    {
        if (startCell == null || startCell.IsEmpty || startCell.Item == null)
            yield break;

        HashSet<CellHexa> processed = new();
        Queue<CellHexa> queue = new();

        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == null || current.IsEmpty || processed.Contains(current))
                continue;

            var group = FloodFillSameType(current);
            if (group.Count < 3) continue;

            foreach (var g in group)
                processed.Add(g);

            yield return WaveCollectEffect(current, group);
            yield return new WaitForSeconds(Mathf.Clamp(group.Count * 0.05f, 0.25f, 0.6f));

            foreach (var g in group)
            {
                if (g.Item == null)
                {
                    break;
                }
                
                foreach (var n in GetHexNeighbors(g))
                {
                    if (n.Item == null)
                    {
                        continue;
                    }
                    if (n != null && !n.IsEmpty && !processed.Contains(n) &&
                        n.Item.Number == g.Item.Number)
                        queue.Enqueue(n);
                }
            }
        }
    }

    private IEnumerator WaveCollectEffect(CellHexa startCell, List<CellHexa> group)
    {
        if (group == null || group.Count == 0) yield break;
        float waveSpeed = 16f;

        foreach (var cell in group.OrderBy(c => Vector3.Distance(c.transform.position, startCell.transform.position)))
        {
            if (cell.Item == null) continue;

            float dist = Vector3.Distance(cell.transform.position, startCell.transform.position);
            float delay = dist / waveSpeed;
            DOVirtual.DelayedCall(delay, () => {
            if (cell.Item != null)
                {
                    cell.Item.Collect();
                }
            });
        }

        yield return new WaitForSeconds(0.25f);
    }

    #endregion

    #region GRID UTILS

    public List<CellHexa> FloodFillSameType(CellHexa startCell)
    {
        List<CellHexa> result = new();
        if (startCell == null || startCell.IsEmpty) return result;

        int targetNumber = startCell.Item.Number;
        Queue<CellHexa> q = new();
        HashSet<CellHexa> visited = new();

        q.Enqueue(startCell);
        visited.Add(startCell);

        while (q.Count > 0)
        {
            var cell = q.Dequeue();
            result.Add(cell);

            foreach (var n in GetHexNeighbors(cell))
            {
                if (n == null || n.IsEmpty || visited.Contains(n))
                    continue;
                if (n.Item.Number == targetNumber)
                {
                    q.Enqueue(n);
                    visited.Add(n);
                }
            }
        }
        return result;
    }

    private List<CellHexa> GetHexNeighbors(CellHexa cell)
    {
        int[][] dirsEven = new int[][]
        {
            new[] { +1, 0 }, new[] { 0, +1 }, new[] { -1, +1 },
            new[] { -1, 0 }, new[] { -1, -1 }, new[] { 0, -1 }
        };

        int[][] dirsOdd = new int[][]
        {
            new[] { +1, 0 }, new[] { +1, +1 }, new[] { 0, +1 },
            new[] { -1, 0 }, new[] { 0, -1 }, new[] { +1, -1 }
        };

        var list = new List<CellHexa>();
        var dirs = (cell.r % 2 == 0) ? dirsEven : dirsOdd;

        foreach (var dir in dirs)
        {
            int nq = cell.q + dir[0];
            int nr = cell.r + dir[1];
            if (cellLookup.TryGetValue((nq, nr), out var neighbor))
                list.Add(neighbor);
        }
        return list;
    }

    #endregion
    public CellHexa GetNearestCellUnder(Vector3 worldPos)
    {
        int columnIndex = GetNearestColumnX(worldPos.x);
        var column = GetCellsInColumn(columnIndex);
        column.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
        CellHexa lastBlockedCell = null;
        for (int i = column.Count - 1; i >= 0; i--)
        {
            var cell = column[i];

            if (cell.IsEmpty)
            {
                bool blocked = false;

                for (int j = i - 1; j >= 0; j--)
                {
                    if (!column[j].IsEmpty)
                    {
                        blocked = true;
                        lastBlockedCell = column[j];
                    }
                }

                if (!blocked)
                    return cell;
            }
        }

        if (lastBlockedCell != null)
        {
            int index = column.IndexOf(lastBlockedCell);
            for (int j = index + 1; j < column.Count; j++)
            {
                if (column[j].IsEmpty)
                {
                    return lastBlockedCell;
                }
            }
        }

        return null;
    }
}
