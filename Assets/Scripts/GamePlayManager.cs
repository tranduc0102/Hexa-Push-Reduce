using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct HexaSpawnData
{
    public ColorHexa color;
    public int number;
}

public class GamePlayManager : MonoBehaviour
{
    public static GamePlayManager Instance;
    public GridManager grid;
    public HexaSpawner spawnHexa;
    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        grid.GeneratorGrid();
        spawnHexa.SpawnInitialHexas(grid);
    }
    [Header("Config")]
    [SerializeField] private List<Color> _colors; // Bảng màu tương ứng ColorHexa enum
    public List<Color> Colors => _colors;

    [Header("Spawn Pattern")]
    [SerializeField] private List<HexaSpawnData> spawnPattern = new List<HexaSpawnData>();

    private int _spawnIndex = 0;

    /// <summary>
    /// Trả về viên Hexa tiếp theo trong chuỗi spawn (lặp lại)
    /// </summary>
    public HexaSpawnData GetNextHexaData()
    {
        if (spawnPattern.Count == 0)
        {
            Debug.LogWarning("⚠️ Spawn pattern is empty!");
            return default;
        }

        HexaSpawnData data = spawnPattern[_spawnIndex];

        _spawnIndex++;
        if (_spawnIndex >= spawnPattern.Count)
            _spawnIndex = 0;

        return data;
    }

    /// <summary>
    /// Sinh ra 1 HexaItem prefab sẵn sàng dùng
    /// </summary>
    public HexaItem SpawnNextHexa(HexaItem prefab, Transform parent)
    {
        var data = GetNextHexaData();

        HexaItem hexa = Instantiate(prefab, parent);
        hexa.Init(data.color, data.number);
        return hexa;
    }
}
