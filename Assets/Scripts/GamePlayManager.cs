using System.Collections.Generic;
using UnityEngine;
public class GamePlayManager : MonoBehaviour
{
    public static GamePlayManager Instance;
    [SerializeField] private GridManager grid;
    [SerializeField] private HexaSpawner _hexaSpawner;
    [SerializeField] private HexaDragController _hexaDragController;
    public GridManager Grid => grid;
    public HexaSpawner HexaSpawner => _hexaSpawner;
    public int Total = 0;

    [Header("Config")]
    [SerializeField] private List<Color> _colors;
    public List<Color> Colors => _colors;
    private int _spawnIndex = 0;
    [SerializeField] private LevelData _currentLevel;
    private void Awake()
    {
        Instance = this;
    }

    public int level;
    private void Start()
    {
        LoadLevvel(level);
    }

    public void LoadLevvel(int level)
    {
        _currentLevel = Resources.Load<LevelData>("Levels/Level " + level);
        if (_currentLevel == null)
        {
            Debug.LogError("XXXX"); return;
        }
        grid.GeneratorGrid(_currentLevel.width, _currentLevel.height, _currentLevel.CellDatas);
        _hexaDragController.Init();
        _hexaDragController.SetCurrentHexa(SpawnNextHexa());
    }
    private int GetNextHexaData()
    {
        if (_currentLevel.spawnPattern.Count == 0)
        {
            return default;
        }

        int data = _currentLevel.spawnPattern[_spawnIndex];

        _spawnIndex++;
        if (_spawnIndex >= _currentLevel.spawnPattern.Count)
            _spawnIndex = 0;

        return data;
    }
    public HexaItem SpawnNextHexa()
    {
        var data = GetNextHexaData();

        HexaItem hexa = _hexaSpawner.GetFromPool();
        hexa.Init(data);
        return hexa;
    }
}
