using UnityEngine;

public class HexaSpawner : MonoBehaviour
{
    [SerializeField] public HexaItem hexaPrefab;
    public HexaDragController dragController;

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

        var totalColumns = grid._width;

        int midColumn = totalColumns / 2;
        var bottomCells = grid.GetCellsInColumn(midColumn);
        if (bottomCells != null && bottomCells.Count > 0)
        {
            var bottomCell = bottomCells[0]; 
            var bottomCell2 = bottomCells[1]; 
            Vector3 spawnPos = bottomCell.transform.position + Vector3.up * 0.1f;
            Vector3 spawnPos1 = bottomCell2.transform.position + Vector3.up * 0.1f;
            var hexa1 = Instantiate(hexaPrefab, spawnPos, Quaternion.identity, bottomCell.transform);
            var hexa2= Instantiate(hexaPrefab, spawnPos1, Quaternion.identity, bottomCell2.transform);
            hexa1.Init(ColorHexa.Red, 1);
            hexa2.Init(ColorHexa.Red, 1);
            hexa1.transform.eulerAngles = bottomCell.transform.eulerAngles;
            hexa2.transform.eulerAngles = bottomCell2.transform.eulerAngles;
            bottomCell.SetItemHexa(hexa1);
            bottomCell2.SetItemHexa(hexa2);
            hexa1.SetCellHexa(bottomCell);
            hexa2.SetCellHexa(bottomCell2);
        }

        int otherColumn = Mathf.Clamp(midColumn - 2, 0, totalColumns - 1);
        var topCells = grid.GetCellsInColumn(otherColumn);
        var topCells1 = grid.GetCellsInColumn(otherColumn + 2);
        if (topCells != null && topCells.Count > 0)
        {
            var topCell = topCells[topCells.Count - 1]; 
            Vector3 spawnPos = topCell.transform.position + Vector3.up * 0.1f;
            var hexa2 = Instantiate(hexaPrefab, spawnPos, Quaternion.identity, topCell.transform);
            hexa2.Init(ColorHexa.Red, 2);
            hexa2.transform.eulerAngles = topCell.transform.eulerAngles;
            topCell.SetItemHexa(hexa2);
            hexa2.SetCellHexa(topCell);
            hexa2.name = "Hexa";

            var topCell1 = topCells1[topCells1.Count - 1];
            Vector3 spawnPos1 = topCell1.transform.position + Vector3.up * 0.1f;
            var hexa21 = Instantiate(hexaPrefab, spawnPos1, Quaternion.identity, topCell1.transform);
            hexa21.Init(ColorHexa.Red, 2);
            hexa21.transform.eulerAngles = topCell1.transform.eulerAngles;
            topCell1.SetItemHexa(hexa2);
            hexa21.SetCellHexa(topCell1);
            hexa21.name = "Hexa";
        }
    }
}
