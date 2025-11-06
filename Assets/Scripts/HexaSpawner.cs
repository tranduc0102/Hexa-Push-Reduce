using UnityEngine;

public class HexaSpawner : MonoBehaviour
{
    [SerializeField] private HexaItem hexaPrefab;
    public HexaDragController dragController;

    // Spawn 2 viên: 1 ở hàng dưới cùng, 1 ở hàng trên cùng
    private void Start()
    {
        dragController.SetCurrentHexa(GamePlayManager.Instance.SpawnNextHexa(hexaPrefab, transform));
    }
    public void SpawnInitialHexas(GridManager grid)
    {
        if (grid == null)
        {
            Debug.LogError("❌ GridManager is null!");
            return;
        }

        var totalColumns = grid._gridSize * 2 + 1;

        int midColumn = totalColumns / 2;
        var bottomCells = grid.GetCellsInColumn(midColumn);
        if (bottomCells != null && bottomCells.Count > 0)
        {
            var bottomCell = bottomCells[0]; 
            Vector3 spawnPos = bottomCell.transform.position + Vector3.up * 0.1f;
            var hexa1 = Instantiate(hexaPrefab, spawnPos, Quaternion.identity, bottomCell.transform);
            hexa1.Init((ColorHexa)Random.Range(0, 3), 1);
            hexa1.transform.localEulerAngles = Vector3.zero;
            bottomCell.SetItemHexa(hexa1);
            hexa1.SetCellHexa(bottomCell);
        }

        int otherColumn = Mathf.Clamp(midColumn - 2, 0, totalColumns - 1);
        var topCells = grid.GetCellsInColumn(otherColumn);
        if (topCells != null && topCells.Count > 0)
        {
            var topCell = topCells[topCells.Count - 1]; 
            Vector3 spawnPos = topCell.transform.position + Vector3.up * 0.1f;
            var hexa2 = Instantiate(hexaPrefab, spawnPos, Quaternion.identity, topCell.transform);
            hexa2.Init((ColorHexa)Random.Range(0, 3), 2);
            hexa2.transform.localEulerAngles = Vector3.zero;
            topCell.SetItemHexa(hexa2);
            hexa2.SetCellHexa(topCell);
        }

        Debug.Log("✅ Spawned 2 Hexas: one bottom, one top.");
    }
}
