using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] public Grid _grid;
    [SerializeField] private CellHexa _cellPrefab;

    [Header("Settings")]
    public int _gridSize = 5;
    [SerializeField] private float moveDuration = 0.25f;

    private List<CellHexa> allCells = new List<CellHexa>();
    private List<float> columnXs = new List<float>();

    // ==========================================
    // GRID GENERATION
    // ==========================================
    [ContextMenu("Generate Grid")]
    public void GeneratorGrid()
    {
        foreach (Transform obj in transform)
            if (obj != transform) DestroyImmediate(obj.gameObject);

        allCells.Clear();

        for (int x = -_gridSize; x <= _gridSize; x++)
        {
            for (int y = -_gridSize; y <= _gridSize; y++)
            {
                Vector3 spawnPos = _grid.CellToWorld(new Vector3Int(x, y, 0));
                if (spawnPos.magnitude > _grid.CellToWorld(new Vector3Int(0, 1, 0)).magnitude * _gridSize)
                    continue;

                CellHexa cell = Instantiate(_cellPrefab, spawnPos, Quaternion.identity, transform);
                cell.transform.localEulerAngles = Vector3.zero;
                allCells.Add(cell);
            }
        }

        BuildColumnIndex();
    }

    // ==========================================
    // COLUMN / POSITION UTILS
    // ==========================================
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

    public void InsertHexaInColumn(HexaItem hexa, int columnIndex)
    {
        List<CellHexa> column = GetCellsInColumn(columnIndex);
        CellHexa targetCell = null;

        for (int i = column.Count - 1; i >= 0; i--)
        {
            if (column[i].IsEmpty)
            {
                targetCell = column[i];
                break;
            }
        }

        if (targetCell == null)
        {
            Debug.Log("❌ Cột đầy!");
            return;
        }

        PushAndCascade(hexa, targetCell);
    }

    private void PushAndCascade(HexaItem hexa, CellHexa targetCell)
    {
        hexa.MoveToCell(targetCell, moveDuration);
    }


    // ==========================================
    // HIGHLIGHT CELL (Ô TRỐNG DƯỚI CÙNG CỦA CỘT)
    // ==========================================
    public CellHexa GetNearestCellUnder(Vector3 worldPos)
    {
        int columnIndex = GetNearestColumnX(worldPos.x);
        var column = GetCellsInColumn(columnIndex);

        // Sắp xếp theo Z tăng dần (xa dần ra sau)
        column.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

        for (int i = column.Count - 1; i >= 0; i--)
        {
            // Nếu cell này trống, nhưng cell phía trên nó (Oz nhỏ hơn) bị block thì đây là vị trí hợp lệ nhất
            if (column[i].IsEmpty)
            {
                bool blocked = false;

                // Kiểm tra các cell phía trước (Oz nhỏ hơn)
                for (int j = i - 1; j >= 0; j--)
                {
                    if (!column[j].IsEmpty)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                    return column[i];
            }
        }

        return null; // Không có chỗ trống hợp lệ
    }
}
