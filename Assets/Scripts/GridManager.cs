using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] public Grid _grid;
    [SerializeField] private CellHexa _cellPrefab;

    [Header("Settings")]
    public int _width = 10;
    public int _heigh = 10;
    [SerializeField] private float moveDuration = 0.25f;

    public List<CellHexa> allCells = new List<CellHexa>();
    private List<float> columnXs = new List<float>();

    [ContextMenu("Generate Grid")]
    public void GeneratorGrid()
    {
        foreach (Transform child in transform)
            DestroyImmediate(child.gameObject);

        allCells.Clear();
        transform.localEulerAngles = Vector3.zero;

        float cellWidth = 0.85f;
        float cellHeight = 0.75f;

        for (int r = -_heigh; r < _heigh; r++)
        {
            float offsetX = (r % 2 == 0) ? -cellWidth / 2f : 0f;

            for (int q = -_width; q < _width; q++)
            {
                float x = q * cellWidth + offsetX;
                float z = r * cellHeight;
                Vector3 pos = new Vector3(x, 0f, z);

                var cell = Instantiate(_cellPrefab, pos, Quaternion.identity, transform);
                cell.InitCoords(q, r);
                cell.name = $"Cell_{q}_{r}";
                allCells.Add(cell);
            }
        }

        Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, 0, float.MinValue);

        foreach (var c in allCells)
        {
            Vector3 p = c.transform.localPosition;
            if (p.x < min.x) min.x = p.x;
            if (p.z < min.z) min.z = p.z;
            if (p.x > max.x) max.x = p.x;
            if (p.z > max.z) max.z = p.z;
        }

        Vector3 center = (min + max) / 2f;

        foreach (var c in allCells)
        {
            c.transform.localPosition -= center;
        }

        transform.localEulerAngles = new Vector3(0f, 90f, 0f);
        transform.position = new Vector3(0, 0, 3f);

        BuildColumnIndex();
    }


    private void BuildColumnIndex()
    {
        columnXs.Clear();

        foreach (var cell in allCells)
        {
            float x = Mathf.Round(cell.transform.position.x * 100f) / 100f;
            if (!columnXs.Contains(x))
                columnXs.Add(x);
        }

        columnXs.Sort();
    }
    public int GetNearestColumnX(float worldX)
    {
        if (columnXs.Count == 0)
            BuildColumnIndex();

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
        if (columnXs.Count == 0)
            BuildColumnIndex();

        List<CellHexa> column = new List<CellHexa>();
        float targetX = columnXs[Mathf.Clamp(columnIndex, 0, columnXs.Count - 1)];

        foreach (var cell in allCells)
        {
            if (Mathf.Abs(cell.transform.position.x - targetX) < 0.01f)
                column.Add(cell);
        }

        column.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
        return column;
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
        List<CellHexa> movedCells = new List<CellHexa>();

        for (int i = column.Count - 1; i > index; i--)
        {
            var below = column[i - 1];
            var current = column[i];

            if (!below.IsEmpty)
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

    private void CheckAndCollect(CellHexa startCell, float delay = 0f)
    {
        DOVirtual.DelayedCall(delay, () =>
        {
            StartCoroutine(CollectCascade(startCell));
        });
    }
    private IEnumerator CollectCascade(CellHexa startCell)
    {
        if (startCell.Item.IsChecking) yield break;
        startCell.Item.IsChecking = true;
        HashSet<CellHexa> processed = new HashSet<CellHexa>();
        Queue<CellHexa> toCheck = new Queue<CellHexa>();

        if (startCell == null || startCell.IsEmpty)
            yield break;

        toCheck.Enqueue(startCell);
        
        while (toCheck.Count > 0)
        {
            var current = toCheck.Dequeue();
            if (current == null || current.IsEmpty || processed.Contains(current))
                continue;

            var group = FloodFillSameType(current);
            foreach (var g in group)
            {
                g.Item.IsChecking = true;
                processed.Add(g);
            }

            if (group.Count < 3)
                continue;

            yield return StartCoroutine(WaveCollectEffect(current, group));


            yield return new WaitForSeconds(0.45f);

            var neighborCandidates = new HashSet<CellHexa>();
            foreach (var c in group)
            {
                foreach (var n in GetHexNeighbors(c))
                {
                    if (n != null && !n.IsEmpty && !processed.Contains(n))
                    {
                        if (n.Item.ColorType == group[0].Item.ColorType &&
                            n.Item.Number == group[0].Item.Number)
                        {
                            n.Item.IsChecking = true;
                            neighborCandidates.Add(n);
                        }
                    }
                }
            }

            foreach (var candidate in neighborCandidates)
            {
                var newGroup = FloodFillSameType(candidate);
                if (newGroup.Count >= 3 && !processed.Contains(candidate))
                {
                    yield return StartCoroutine(WaveCollectEffect(candidate, newGroup));

                    foreach (var c in newGroup)
                    {
                        c.Item.IsChecking = true;
                        processed.Add(c);
                    }

                    yield return new WaitForSeconds(0.45f);
                    foreach (var c in newGroup)
                    {
                        foreach (var n in GetHexNeighbors(c))
                        {
                            if (n != null && !n.IsEmpty && !processed.Contains(n))
                                toCheck.Enqueue(n);
                        }
                    }
                }
            }
        }
    }
    private IEnumerator WaveCollectEffect(CellHexa startCell, List<CellHexa> group)
    {
        if (group == null || group.Count == 0) yield break;

        float waveSpeed = 6f;

        foreach (var cell in group.OrderBy(c => Vector3.Distance(c.transform.position, startCell.transform.position)))
        {
            if (cell.Item == null) continue;

            float dist = Vector3.Distance(cell.transform.position, startCell.transform.position);
            float delay = dist / waveSpeed;

            DOVirtual.DelayedCall(delay, () =>
            {
                cell.Item.Collect();
            });
        }

        yield return new WaitForSeconds(0.25f);
    }

    public List<CellHexa> FloodFillSameType(CellHexa startCell)
    {
        List<CellHexa> result = new List<CellHexa>();
        if (startCell == null || startCell.IsEmpty)
            return result;

        var startItem = startCell.Item;
        int targetNumber = startItem.Number;
        var targetColor = startItem.ColorType;

        Queue<CellHexa> queue = new Queue<CellHexa>();
        HashSet<CellHexa> visited = new HashSet<CellHexa>();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            result.Add(cell);

            foreach (var n in GetHexNeighbors(cell))
            {
                if (n == null || n.IsEmpty || visited.Contains(n))
                    continue;

                var item = n.Item;
                if (item.Number == targetNumber && item.ColorType == targetColor)
                {
                    queue.Enqueue(n);
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
            new[]{+1, 0}, new[]{0, +1}, new[]{-1, +1},
            new[]{-1, 0}, new[]{-1, -1}, new[]{0, -1}
        };

        int[][] dirsOdd = new int[][]
        {
            new[]{+1, 0}, new[]{+1, +1}, new[]{0, +1},
            new[]{-1, 0}, new[]{0, -1}, new[]{+1, -1}
        };

        var list = new List<CellHexa>();
        var dirs = (cell.r % 2 == 0) ? dirsEven : dirsOdd;

        foreach (var dir in dirs)
        {
            int nq = cell.q + dir[0];
            int nr = cell.r + dir[1];
            var neighbor = allCells.FirstOrDefault(c => c.q == nq && c.r == nr);
            if (neighbor != null)
                list.Add(neighbor);
        }

        return list;
    }
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
