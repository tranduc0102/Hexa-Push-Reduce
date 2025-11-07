using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VInspector;

[CreateAssetMenu(fileName = "Level", menuName = "Game/Level Data")]
public class LevelData : ScriptableObject
{
    public int width;
    public int height;
    public int target;
    public List<int> spawnPattern;
    public List<CellData> CellDatas;
    public string spaw;
}

[System.Serializable]
public class CellData
{
    public int q, r;
    public bool isHidden;
    public int spawnHexaData;
}
public enum ColorHexa
{
    None = 0,
    Red = 1,
    Blue = 2,
    Yellow = 3,
    Green = 4,
    Pink = 5,
    Purple = 6,
    Orange = 7,
    LightBlue = 8
}