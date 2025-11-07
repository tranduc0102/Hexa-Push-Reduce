using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
[CustomEditor(typeof(LevelData))]
[CanEditMultipleObjects]
public class LevelDataEditor : Editor
{
    private LevelData level;
    private Vector2 scrollPos;
    private bool showDefaultInspector = false;
    private SerializedProperty spawnPatternProp;

    private const float CELL_SIZE = 40f;
    private Dictionary<(int q, int r), CellData> cellLookup = new();

    private void OnEnable()
    {
        level = (LevelData)target;
        GenerateGrid();
    }

    public override void OnInspectorGUI()
    {
        showDefaultInspector = EditorGUILayout.Toggle("Show Default Inspector", showDefaultInspector);
        EditorGUILayout.Space(5);

        if (showDefaultInspector)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);
        level.width = EditorGUILayout.IntField("Width", level.width);
        level.height = EditorGUILayout.IntField("Height", level.height);
        level.target = EditorGUILayout.IntField("Target", level.target);
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Spawn Pattern", EditorStyles.boldLabel);
        spawnPatternProp = serializedObject.FindProperty("spawnPattern");
        EditorGUILayout.PropertyField(spawnPatternProp, new GUIContent("Spawn Pattern"), true);

        /*level.spaw = EditorGUILayout.TextField("Spaw (comma-separated)", level.spaw);

        if (GUILayout.Button("Load Pattern from Spaw"))
        {
            if (string.IsNullOrEmpty(level.spaw))
            {
                Debug.LogWarning("⚠️ Spaw string is empty!");
            }
            else
            {
                level.spawnPattern = new List<int>();
                string[] parts = level.spaw.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    if (int.TryParse(p.Trim(), out int num))
                        level.spawnPattern.Add(num);
                }

                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
            }
        }*/
        
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Regenerate Grid"))
        {
            ClearData();
            GenerateGrid();
        }

        if (GUILayout.Button("Clear Data", GUILayout.Width(120)))
        {
            ClearData();
        }

        EditorGUILayout.EndHorizontal();

        if (cellLookup.Count == 0)
            return;

        EditorGUILayout.Space();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(500));
        DrawSquareGrid();
        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
        
      
    }

    private void GenerateGrid()
    {
        cellLookup.Clear();

        if (level.CellDatas == null)
            level.CellDatas = new List<CellData>();

        foreach (var c in level.CellDatas)
            cellLookup[(c.q, c.r)] = c;

        for (int r = -level.height; r < level.height; r++)
        {
            for (int q = -level.width; q < level.width; q++)
            {
                if (!cellLookup.ContainsKey((q, r)))
                {
                    cellLookup[(q, r)] = new CellData
                    {
                        q = q,
                        r = r,
                        isHidden = false,
                        spawnHexaData = 0
                    };
                }
            }
        }

        SaveToLevelData(); 
        Repaint();
    }

    private void ClearData()
    {
        Undo.RecordObject(level, "Clear Level Data");
        level.CellDatas.Clear();
        cellLookup.Clear();
        SaveToLevelData();
        Debug.Log($" Cleared all cell data in {level.name}");
        Repaint();
    }

    private void DrawSquareGrid()
    {
        float gridWidth = level.width * 2 * CELL_SIZE;
        float gridHeight = level.height * 2 * CELL_SIZE;

        float startX = (EditorGUIUtility.currentViewWidth - gridWidth) / 2f;

        float startY = 250f - gridHeight / 2f; 

        foreach (var kv in cellLookup.Values)
        {
            var c = kv;

            float x = startX + ((level.width + (c.r + level.width)) * CELL_SIZE);
            float y = startY + ((c.q + level.width) * CELL_SIZE);

            Rect rect = new Rect(x, y, CELL_SIZE - 2, CELL_SIZE - 2);
            Color prev = GUI.color;

            if (c.isHidden)
                GUI.color = Color.gray;
            else if (c.spawnHexaData == 0)
                GUI.color = new Color(0.9f, 0.9f, 0.9f);
            else
                GUI.color = GetColorByNumber(c.spawnHexaData);

            string label = c.spawnHexaData == 0 ? "" : c.spawnHexaData.ToString();

            if (GUI.Button(rect, label))
            {
                ShowEditPopup(c);
            }

            GUI.color = prev;
        }
    }


    private void ShowEditPopup(CellData cell)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Toggle Hidden"), cell.isHidden, () =>
        {
            cell.isHidden = !cell.isHidden;
            SaveToLevelData();
            Repaint();
        });

        menu.AddSeparator("");

        for (int i = 0; i <= 8; i++)
        {
            int num = i;
            string label = (i == 0) ? "Clear (0)" : "Set Number/" + i;

            menu.AddItem(new GUIContent(label), cell.spawnHexaData == num, () =>
            {
                cell.spawnHexaData = num;
                cellLookup[(cell.q, cell.r)] = cell;

                SaveToLevelData();
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    private void SaveToLevelData()
    {
        level.CellDatas = new List<CellData>(cellLookup.Values);
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();
    }

    private Color GetColorByNumber(int number)
    {
        return number switch
        {
            1 => new Color(1f, 0.3f, 0.3f),
            2 => new Color(1f, 0.9f, 0.3f),
            3 => new Color(0.3f, 1f, 0.3f),
            4 => new Color(0.3f, 0.5f, 1f),
            5 => new Color(1f, 0.6f, 0.8f),
            6 => new Color(0.6f, 0.4f, 1f),
            7 => new Color(1f, 0.6f, 0.3f),
            8 => new Color(0.3f, 1f, 1f),
            _ => new Color(0.9f, 0.9f, 0.9f)
        };
    }

    private ColorHexa GetColorEnumByNumber(int number)
    {
        return number switch
        {
            1 => ColorHexa.Red,
            2 => ColorHexa.Yellow,
            3 => ColorHexa.Green,
            4 => ColorHexa.Blue,
            5 => ColorHexa.Pink,
            6 => ColorHexa.Purple,
            7 => ColorHexa.Orange,
            8 => ColorHexa.LightBlue,
            _ => ColorHexa.None
        };
    }
}
#endif
