using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Globalization;
using System;

namespace Kamgam.UVEditor
{
    public static class BackgroundTexture
    {
        private static Dictionary<Color, Texture2D> textures = new Dictionary<Color, Texture2D>();

        public static Texture2D Get(Color color)
        {
            if (textures.ContainsKey(color) && textures[color] != null) 
                return textures[color];

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();

            if (textures.ContainsKey(color))
                textures[color] = texture;
            else
                textures.Add(color, texture);

            return texture;
        }
    }

    partial class UVTriangleSelectTool
    {
        Rect windowRect;

        [System.NonSerialized]
        protected double _lastSettingsOpenTimestamp;

        /// <summary>
        /// 0 = hidden
        /// 1 = save
        /// 2 = load
        /// </summary>
        [System.NonSerialized]
        protected int _saveLoadVisibility;

        [System.NonSerialized]
        protected string _saveSelectionName = "Quicksave";

        [System.NonSerialized]
        protected Vector2 _loadSelectionScrollViewPos;

        void initWindowSize()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                var settings = UVEditorSettings.GetOrCreateSettings();
                windowRect.position = settings.WindowPosition;
                windowRect.width = 250;
                windowRect.height = 90;

                // If the window is not yet set or if it's outside the scene view then reset position.
                if (
                       windowRect.position.x > SceneView.lastActiveSceneView.position.width
                    || windowRect.position.x < 0
                    || windowRect.position.y > SceneView.lastActiveSceneView.position.height
                    || windowRect.position.y < 0
                    )
                {
                    // center
                    windowRect.position = new Vector2(
                        SceneView.lastActiveSceneView.position.width * 0.5f,
                        SceneView.lastActiveSceneView.position.height * 0.5f
                        );
                    settings.WindowPosition = windowRect.position;
                    EditorUtility.SetDirty(settings);
                }
            }
        }

        [MenuItem("Tools/UV Editor/Debug/Recenter Window", priority = 220)]
        static void RecenterWindowMenu()
        {
            if (Instance != null)
                Instance.RecenterWindow();
        }

        public void RecenterWindow()
        {
            var settings = UVEditorSettings.GetOrCreateSettings();
            
            // center
            windowRect.position = new Vector2(
                SceneView.lastActiveSceneView.position.width * 0.5f,
                SceneView.lastActiveSceneView.position.height * 0.5f
                );

            // dimensions
            windowRect.width = 250;
            windowRect.height = 90;

            settings.WindowPosition = windowRect.position;
            EditorUtility.SetDirty(settings);

            Logger.LogWarning("Please consider upgrading Unity. There is a bug in Unity 2021.0 to 2021.2.3f1 and 2022.0 - 2022.1.0a15, see: https://issuetracker.unity3d.com/issues/tool-handles-are-invisible-in-scene-view-when-certain-objects-are-selected");
        }

        void drawWindow(SceneView sceneView, int controlID)
        {
            Handles.BeginGUI();

            var oldRect = windowRect;
            windowRect = GUILayout.Window(controlID, windowRect, drawWindowContent, "UV Editor");

            // Auto save window position in settings if changed.
            if (Vector2.SqrMagnitude(oldRect.position - windowRect.position) > 0.01f)
            {
                var settings = UVEditorSettings.GetOrCreateSettings();
                settings.WindowPosition = windowRect.position;
                EditorUtility.SetDirty(settings);
            }

            GUI.enabled = true;
            Handles.EndGUI();
        }

        void drawWindowContent(int controlID)
        {
            var settings = UVEditorSettings.GetOrCreateSettings();

            var bgColor = UtilsEditor.IsLightTheme() ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.25f, 0.25f, 0.25f);
            var tex = BackgroundTexture.Get(bgColor);
            if (UtilsEditor.IsLightTheme())
            {
                GUI.skin.label.normal.textColor = Color.black;
                GUI.skin.label.hover.textColor = new Color(0.2f, 0.2f, 0.2f);

                GUI.skin.toggle.normal.textColor = Color.black;
                GUI.skin.toggle.hover.textColor = new Color(0.2f, 0.2f, 0.2f);

                // After switching to light theme the button bg textures are suddenly white but ONLY until Unity is restarted
                // then the buttons turn back to black .. WTH?!?
                //GUI.skin.button.normal.textColor = Color.black;
                //GUI.skin.button.active.textColor = Color.black;
                //GUI.skin.button.focused.textColor = Color.black;
                //GUI.skin.button.hover.textColor = new Color(0.2f, 0.2f, 0.2f);
                GUI.skin.button.normal.textColor = Color.white;
                GUI.skin.button.active.textColor = Color.white;
                GUI.skin.button.focused.textColor = Color.white;
                GUI.skin.button.hover.textColor = new Color(0.8f, 0.8f, 0.8f);
            }
            GUI.DrawTexture(new Rect(5, 22, windowRect.width - 10, windowRect.height - 26), tex);

            BeginHorizontalIndent(5, beginVerticalInside: true);

            GUILayout.Space(5);

            // settings button
            var settingsBtnStyle = GUIStyle.none;
            settingsBtnStyle.normal.background = BackgroundTexture.Get(UtilsEditor.IsLightTheme() ? new Color(0.45f, 0.45f, 0.45f) : bgColor);
            settingsBtnStyle.hover.background = BackgroundTexture.Get(new Color(0.5f, 0.5f, 0.5f));
            var settingsBtnContent = EditorGUIUtility.IconContent("d_Settings");
            settingsBtnContent.tooltip = "Close the tool (Esc).";
            if (GUI.Button(new Rect(windowRect.width - 40, 2, 16, 20), settingsBtnContent, settingsBtnStyle))
            {
                UVEditorSettings.OpenSettings();
                _lastSettingsOpenTimestamp = EditorApplication.timeSinceStartup;
            }

            // close button
            var closeBtnStyle = GUIStyle.none;
            closeBtnStyle.normal.background = BackgroundTexture.Get(UtilsEditor.IsLightTheme() ? new Color(0.45f, 0.45f, 0.45f) : bgColor);
            closeBtnStyle.hover.background = BackgroundTexture.Get(new Color(0.5f, 0.5f, 0.5f));
#if UNITY_2023_1_OR_NEWER
            var closeBtnContent = EditorGUIUtility.IconContent("d_clear@2x");
#else
            var closeBtnContent = EditorGUIUtility.IconContent("d_winbtn_win_close_a@2x");
#endif
            closeBtnContent.tooltip = "Close the tool (Esc).";
            if (GUI.Button(new Rect(windowRect.width - 21, 2, 16, 20), closeBtnContent, closeBtnStyle))
            {
                ExitTool(); 
            }

            // Content
            drawSelectWindowContentGUI(settings);

            GUILayout.Space(2);

            EndHorizontalIndent(bothSides: true);

            GUILayout.Space(4);

            GUI.SetNextControlName("BG");
            GUI.DragWindow();
        }

        void drawPickObjectsWindowContent()
        {
            DrawLabel("Select objects", bold: true);
            DrawLabel("Select one or more objects to extract meshes from.", "This is useful to avoid selecting background meshes by accident. You can return to this step at any time and add or remove objects.", wordwrap: true);
            
            if (DrawButton("Reset", "Clears the current selection, deselects any object and resets all configurations to default."))
            {
                ClearSelection();
                Selection.objects = new GameObject[] { };
                ResetSelect();
                UVEditorWindow.Instance?.ClearCacheAndSelection();
            }

            GUILayout.Space(5);

            GUI.enabled = SelectedObjects.Length > 0;
            var col = GUI.color;
            if (SelectedObjects.Length > 0)
                GUI.color = new Color(0.8f, 1f, 0.8f);
            else
                GUI.color = new Color(1f, 0.8f, 0.8f);
            GUI.color = col;
            GUI.enabled = true;
        }

        void drawSelectWindowContentGUI(UVEditorSettings settings)
        {
            GUILayout.BeginHorizontal();
            DrawLabel("Select polygons", "Paint on the objects to select polyons.", bold: true);

            // Tab menu for selection
            GUI.enabled = _saveLoadVisibility != 0;
            if (DrawButton("", "Show SELECT options.", "d_pick@2x", "ButtonLeft", GUILayout.Width(24), GUILayout.Height(24)))
            {
                _saveLoadVisibility = 0;
                GUI.FocusControl("BG");
            }
            GUI.enabled = _saveLoadVisibility != 1;
            if (DrawButton("", "Show SAVE options.", "d_CloudConnect@2x", "ButtonMid", GUILayout.Width(24), GUILayout.Height(24)))
            {
                _saveLoadVisibility = 1;
                GUI.FocusControl("BG");
            }
            GUI.enabled = _saveLoadVisibility != 2;
            if (DrawButton("", "Show LOAD options.", "d_scrolldown@2x", "ButtonRight", GUILayout.Width(24), GUILayout.Height(24)))
            {
                _saveLoadVisibility = 2;
                GUI.FocusControl("BG");
            }
            GUI.enabled = true;
            if (DrawButton("", "Quicksave: Saves the current selection into the last saved selection without asking for confirmation.", "d_FrameCapture@2x", null, GUILayout.Width(24), GUILayout.Height(24)))
            {
                saveSelectionFromUI(suppressReplacementConfirmation: true);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();


            if (_saveLoadVisibility == 0)
                drawSelectTrianglesGUI();
            else
                drawSelectSaveLoadGUI(settings);

            // footer buttons
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            GUI.enabled = UVEditorWindow.Instance == null;
            if (DrawButton("Open UV Window", "Editing the UVs is done in a separate window."))
            {
                UVEditorWindow.ShowWindow();
            }
            GUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        private void drawSelectTrianglesGUI()
        {
            GUI.enabled = SelectedObjects.Length > 0;

            _selectCullBack = !EditorGUILayout.ToggleLeft(new GUIContent("X-Ray", "X-Ray mode allows you to select front and back facing triangles at the same time."), !_selectCullBack);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Brush Size:", "Reduce the brush size to 0 to select only one triangle at a time.\n\nYou can also use SHIFT + MOUSE WHEEL to change the brush size."), GUILayout.MaxWidth(75));
            _selectBrushSize = GUILayout.HorizontalSlider(_selectBrushSize, 0f, 1f);
            GUILayout.Label((_selectBrushSize * 10).ToString("f1", CultureInfo.InvariantCulture), GUILayout.MaxWidth(22));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Brush Depth:", "Brush depth defines how far into the object the selection will go. This helps to avoid selecting background polygons by accident. If you want infinite depth then simply turn on X-Ray."), GUILayout.MaxWidth(75));
            _selectBrushDepth = GUILayout.HorizontalSlider(_selectBrushDepth, 0f, 2f);
            _selectBrushDepth = EditorGUILayout.FloatField(_selectBrushDepth, GUILayout.MaxWidth(32));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _lastSelectedTriangle != null;
            if (DrawButton("Select Linked", "Selects all triangles that are connected to the last selected triangle.\n\nHold SHIFT while clicking the button to deselect linked.\n\nHINT: You can press S or SHIFT + S while selecting to trigger this action."))
            {
                SelectLinked(remove: Event.current.shift);
            }
            if (DrawButton("Deselect", "Deselects all triangles that are connected to the last selected triangle."))
            {
                SelectLinked(remove: true);
            }
            _limitLinkedSearchToSubMesh = EditorGUILayout.ToggleLeft(
                new GUIContent("Limit", "Enable to limit selection to a single sub mesh.\nIt will use the sub mesh of the last selected triangle."),
                _limitLinkedSearchToSubMesh,
                GUILayout.Width(50)
                );
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            if (DrawButton("Clear", "Clears the current selection."))
            {
                ClearSelection();
            }
            if (DrawButton("Invert Selection", "Inverts the current selection. It will selected everything except the triangles you have currently selected.", null, "ButtonLeft"))
            {
                invertSelection(limitToObjectsWithSelections: true); // true because UV Editor only supports one object anyways.
            }

            // UV Editor only support one object anyways, no need for this.
            //if (DrawButton("Invert Object", "Inverts the current selection. It takes only those objects into account that have at least one triangle selected.", null, "ButtonRight"))
            //{
            //    invertSelection(limitToObjectsWithSelections: true);
            //}
            GUILayout.EndHorizontal();
        }

        private void drawSelectSaveLoadGUI(UVEditorSettings settings)
        {
            // Save Section
            if (_saveLoadVisibility == 1)
            {
                GUILayout.BeginHorizontal();
                DrawLabel("Save:", "Enter a name for your saved selection.", wordwrap: false);
                _saveSelectionName = EditorGUILayout.TextField(_saveSelectionName);
                GUILayout.EndHorizontal();
                if (DrawButton("Save", "Save the selection now."))
                {
                    saveSelectionFromUI(suppressReplacementConfirmation: false);
                }
            }
            // Load Section
            else if (_saveLoadVisibility == 2)
            {
                GUILayout.BeginHorizontal();
                DrawLabel("Snapshots:", "A list of all available snapshots", wordwrap: false);
                GUILayout.FlexibleSpace();
                if (DrawButton("Cear", "Clears the current selection.", null, null, GUILayout.Width(40), GUILayout.Height(18)))
                {
                    ClearSelection();
                }
                
                GUILayout.EndHorizontal();

                _loadSelectionScrollViewPos = GUILayout.BeginScrollView(_loadSelectionScrollViewPos, GUILayout.MinHeight(45), GUILayout.MaxHeight(100));
                foreach (var item in settings.SelectionSnapshots)
                {
                    GUILayout.BeginHorizontal();
                    if (DrawButton("X", null, null, null, GUILayout.Width(22)))
                    {
                        EditorApplication.delayCall += () =>
                        {
                            deleteSavedSelection(item.Name);
                            SceneView.RepaintAll();
                        };
                    }
                    if (DrawButton(item.Name, "Replaces the current selection with this selection."))
                    {
                        EditorUtility.DisplayProgressBar("Loading Selection", "...", 0.2f);
                        _saveLoadVisibility = 0;
                        try
                        {
                            LoadSelection(item, additive: false);
                        }
                        finally
                        {
                            EditorUtility.ClearProgressBar();
                        }
                    }
                    if (DrawButton("+", "Adds this selection to the current selection.", null, null, GUILayout.Width(22)))
                    {
                        EditorUtility.DisplayProgressBar("Loading Selection", "...", 0.2f);
                        try
                        {
                            LoadSelection(item, additive: true);
                        }
                        finally
                        {
                            EditorUtility.ClearProgressBar();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(5);
        }

        protected void saveSelectionFromUI(bool suppressReplacementConfirmation)
        {
            EditorUtility.DisplayProgressBar("Saving Selection", "...", 0.2f);
            try
            {
                bool didSave = saveSelection(_saveSelectionName, suppressReplacementConfirmation);
                if (didSave)
                {
                    _saveLoadVisibility = 0;
                    Logger.LogMessage("Selection saved as '" + _saveSelectionName + "'.");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected bool _firstExtractDraw = true;

        protected List<Component> _uniqueComponentsInSelectedTriangles = new List<Component>();
        protected List<BoneWeight> _tmpUniqueComponentsBoneWeights = new List<BoneWeight>();


#region GUI Helpers
        public static bool DrawButton(string text, string tooltip = null, string icon = null, GUIStyle style = null, params GUILayoutOption[] options)
        {
            GUIContent content;

            // After switching to light theme the button bg textures are suddenly white but ONLY until Unity is restarted
            // then the buttons turn back to black .. WTH?!?
            //if(UtilsEditor.IsLightTheme() && !string.IsNullOrEmpty(icon) && icon.StartsWith("d_"))
            //{
            //    icon = icon.Substring(2);
            //}

            // icon
            if (!string.IsNullOrEmpty(icon))
                content = EditorGUIUtility.IconContent(icon);
            else
                content = new GUIContent();

            // text
            content.text = text;

            // tooltip
            if (!string.IsNullOrEmpty(tooltip))
                content.tooltip = tooltip;

            if (style == null)
                style = new GUIStyle(GUI.skin.button);

            // After switching to light theme the button bg textures are suddenly white but ONLY until Unity is restarted
            // then the buttons turn back to black .. WTH?!?

            // if (UtilsEditor.IsLightTheme())
            // {
            //     if (GUI.enabled)
            //     {
            //         style.normal.textColor = Color.black;
            //         style.active.textColor = Color.black;
            //         style.hover.textColor = new Color(0.2f, 0.2f, 0.2f);
            //     }
            //     else
            //     {
            //         style.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            //         style.active.textColor = new Color(0.3f, 0.3f, 0.3f);
            //         style.hover.textColor = new Color(0.4f, 0.4f, 0.4f);
            //     }
            // }
            // var col = GUI.color;
            // if (UtilsEditor.IsLightTheme() && col.r == col.g && col.r == col.b)
            // {
            //     GUI.color = Color.white;
            // }

            // But then the styles for "ButtonLeft", "ButtonMid", "ButtonRight" have dark text color on dark ground.
            // What the hell is going on here.
            if (UtilsEditor.IsLightTheme())
            {
                if (style == (GUIStyle)"ButtonLeft" ||
                    style == (GUIStyle)"ButtonMid" ||
                    style == (GUIStyle)"ButtonRight")
                {
                    style.normal.textColor = Color.white;
                }
            }

            var btn = GUILayout.Button(content, style, options);
            // GUI.color = col;

            return btn;
        }

        public static void BeginHorizontalIndent(int indentAmount = 10, bool beginVerticalInside = true)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(indentAmount);

            if (beginVerticalInside)
            {
                GUILayout.BeginVertical();
            }
        }

        public static void EndHorizontalIndent(float indentAmount = 10, bool begunVerticalInside = true, bool bothSides = false)
        {
            if (begunVerticalInside)
            {
                GUILayout.EndVertical();
            }

            if (bothSides)
                GUILayout.Space(indentAmount);

            GUILayout.EndHorizontal();
        }

        public static void DrawLabel(string text, string tooltip = null, Color? color = null, bool bold = false, bool wordwrap = true, bool richText = true, Texture icon = null, GUIStyle style = null, params GUILayoutOption[] options)
        {
            if (!color.HasValue)
                color = GUI.skin.label.normal.textColor;

            if (style == null)
                style = new GUIStyle(GUI.skin.label);
            if (bold)
                style.fontStyle = FontStyle.Bold;
            else
                style.fontStyle = FontStyle.Normal;

            style.normal.textColor = color.Value;
            style.hover.textColor = color.Value;
            style.wordWrap = wordwrap;
            style.richText = richText;
            style.imagePosition = ImagePosition.ImageLeft;

            var content = new GUIContent(text);
            if (tooltip != null)
                content.tooltip = tooltip;
            if (icon != null)
            {
                GUILayout.Space(16);
                var position = GUILayoutUtility.GetRect(content, style, options);
                GUI.DrawTexture(new Rect(position.x - 16, position.y, 16, 16), icon);
                GUI.Label(position, content, style);
            }
            else
            {
                GUILayout.Label(content, style, options);
            }
        }
#endregion
    }
}
