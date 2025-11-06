using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
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

        float gridWidthWorld = (_width * 2 - 1) * cellWidth;
        float gridHeightWorld = (_heigh * 2 - 1) * cellHeight;
        Vector3 offsetCenter = new Vector3(gridWidthWorld / 2f, 0f, gridHeightWorld / 2f);

        for (int row = -_heigh; row < _heigh; row++)
        {
            float offsetX = (row % 2 == 0) ? -cellWidth / 2f : 0f;

            for (int col = -_width; col < _width; col++)
            {
                float x = col * cellWidth + offsetX;
                float z = row * cellHeight;

                Vector3 spawnPos = new Vector3(x, 0f, z) - offsetCenter;

                CellHexa cell = Instantiate(_cellPrefab, spawnPos, Quaternion.identity, transform);
                cell.name = $"Cell_{col}_{row}";
                allCells.Add(cell);
            }
        }
        transform.localEulerAngles = new Vector3(0f, 90f, 0f);
        transform.position = new Vector3(transform.position.x + _heigh/2f + 0.65f, transform.position.y, transform.position.z);
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

    public void InsertHexaInColumn(HexaItem hexa, int columnIndex, CellHexa targetCell)
    {
        if(targetCell == null) return;
        List<CellHexa> column = GetCellsInColumn(columnIndex);
        if (!targetCell.IsEmpty)
        {
            ShiftColumnUp(hexa, column, targetCell);
            return;
        }
        hexa.MoveToCell(targetCell, moveDuration);
        Debug.LogError(FloodFillSameType(targetCell).Count);
    }
    private void ShiftColumnUp(HexaItem hexa, List<CellHexa> column, CellHexa targetCell)
    {
        column.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

        int index = column.IndexOf(targetCell);
        for (int i = column.Count - 1; i > index; i--)
        {
            var below = column[i - 1];  
            var current = column[i];   

            if (!below.IsEmpty)
            {
                var item = below.Item;
                item.MoveToCell(current, moveDuration);
                Debug.LogError(FloodFillSameType(current).Count);
                below.ClearItem();
            }
        }
        hexa.MoveToCell(targetCell, moveDuration);
        Debug.LogError(FloodFillSameType(targetCell).Count);
        /*var collect = FloodFillSameType(targetCell);
        if (collect.Count >= 3)
        {
            foreach (var VARIABLE in collect)
            {
                VARIABLE.Item.Collect();
            }
        }*/
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
    public List<CellHexa> FloodFillSameType(CellHexa startCell)
    {
        List<CellHexa> result = new List<CellHexa>();
        if (startCell == null || startCell.IsEmpty)
            return result;

        HexaItem startItem = startCell.Item;
        int targetNumber = startItem.Number;
        Color targetColor = startItem.Color; 

        Queue<CellHexa> queue = new Queue<CellHexa>();
        HashSet<CellHexa> visited = new HashSet<CellHexa>();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            result.Add(cell);

            foreach (var neighbor in GetNeighbors(cell))
            {
                if (neighbor == null || neighbor.IsEmpty || visited.Contains(neighbor))
                    continue;

                var item = neighbor.Item;
                if (item.Number == targetNumber && item.Color == targetColor)
                {
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }
        }
        return result;
    }
    private List<CellHexa> GetNeighbors(CellHexa cell)
    {
        List<CellHexa> neighbors = new List<CellHexa>();

        foreach (var other in allCells)
        {
            if (other == cell)
                continue;
            if (Vector3.Distance(cell.transform.position, other.transform.position) < 0.9f)
            {
                neighbors.Add(other);
            }
        }

        return neighbors;
    }

}
