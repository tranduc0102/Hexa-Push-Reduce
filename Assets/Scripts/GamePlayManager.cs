using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
public class GamePlayManager : MonoBehaviour
{
    public static GamePlayManager Instance;
    [SerializeField] private GridManager grid;
    [SerializeField] private HexaSpawner _hexaSpawner;
    [SerializeField] private HexaDragController _hexaDragController;
    [SerializeField] private HexaItem _hexaView;
    [SerializeField] private Camera camera;
    public GridManager Grid => grid;
    public HexaSpawner HexaSpawner => _hexaSpawner;
    public int Total => _currentLevel.target;

    [Header("Config")]
    [SerializeField] private List<Color> _colors;
    public List<Color> Colors => _colors;
    private int _spawnIndex = 0;
    [SerializeField] private LevelData _currentLevel;
    private int _nextHexaData;
    private GameState _state;
    public GameState State
    {
        get { return _state; }
        set { _state = value; }
    }
    public int Level
    {
        get {
            return PlayerPrefs.GetInt("Level", 1);
        }
        set
        {
            if(value > PlayerPrefs.GetInt("Level", 1))
            {
                PlayerPrefs.SetInt("Level", value);
            }
        }
    }
    private void Awake()
    {
        Instance = this;
        DOTween.SetTweensCapacity(1000, 200);
        DOTween.useSmoothDeltaTime = true;
        Application.targetFrameRate = 60;
    }
    private void Start()
    {
        LoadLevvel(Level);
    }
  /*  int level = 0;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            level += 1;
            LoadLevvel(level);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            level -= 1;
            LoadLevvel(level);
        }
    }*/
    public void LoadLevvel(int level)
    {
        _currentLevel = Resources.Load<LevelData>("Levels/Level " + level);
        if (_currentLevel == null)
        {
            _currentLevel = Resources.Load<LevelData>("Levels/Level " + UnityEngine.Random.Range(10, 21));
        }
        _spawnIndex = 0;
        grid.GeneratorGrid(_currentLevel.width, _currentLevel.height, _currentLevel.CellDatas, _currentLevel.offSetY);
        camera.transform.localPosition = new Vector3(camera.transform.localPosition.x, camera.transform.localPosition.y, _currentLevel.offsetZCam);
        float targetAspect = 1080f/1920f;
        float currentAspect = (float)Screen.width / Screen.height;

        float scaleFactor = targetAspect / currentAspect; 
        camera.orthographicSize = _currentLevel.camSize * scaleFactor;
        _hexaDragController.Init();
        _state = GameState.Playing;
        SpawnNextHexa(false);
        UIManager.Instance.UpdatePointInUI(true);
        UIManager.Instance.UpdateViewLevel(Level);
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
        _nextHexaData = _currentLevel.spawnPattern[_spawnIndex];
        return data;
    }
    public void SpawnNextHexa(bool canPlayAnim)
    {
        var data = GetNextHexaData();
        UpdateNextHexaView();
        HexaItem hexa = _hexaSpawner.GetFromPool();
        hexa.Init(data);
        if (canPlayAnim)
        {
            hexa.transform.localPosition = Vector3.forward * 10;
            hexa.transform.DOLocalMoveZ(0, 0.5f).OnComplete(delegate
            {
                _hexaDragController.SetCurrentHexa(hexa);
            });
        }
        else
        {
            _hexaDragController.SetCurrentHexa(hexa);
        }
    }
    private void UpdateNextHexaView()
    {
        if (_hexaView == null) return;

        _hexaView.Init(_nextHexaData, true);
    }

    public void ResetLevel()
    {
        LoadLevvel(Level);
    }

    public void NextLevel()
    {
        GamePlayManager.Instance.Level += 1;
        LoadLevvel(Level);
    }
    public enum GameState
    {
        Waiting,
        Playing,
        Win,
        Lose
    }
    [ContextMenu("Clear Data")]
    public void ClearData()
    {
        PlayerPrefs.DeleteAll();
    }
}
