using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
#if UNITY_2021_2_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
#else
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
#endif

namespace Kamgam.UVEditor
{
    [EditorTool("UV Triangle Select Tool")]
    partial class UVTriangleSelectTool : EditorTool
    {
        public override GUIContent toolbarIcon
        {
            get
            {
                if(UtilsEditor.IsLightTheme())
                    return EditorGUIUtility.IconContent("PreMatCube@2x");
                else
                    return EditorGUIUtility.IconContent("d_PreMatCube@2x");
            }
        }

        public static UVTriangleSelectTool Instance;

        public static void StartTool()
        {
            if (ToolManager.activeToolType != typeof(UVTriangleSelectTool) || Instance == null)
                ToolManager.SetActiveTool<UVTriangleSelectTool>();
        }

        public static void ExitTool(bool notifyUVEditorWindow = true)
        {
            Instance?.exitToolInternal(notifyUVEditorWindow);
        }

        protected void exitToolInternal(bool notifyUVEditorWindow = true)
        {
            if (notifyUVEditorWindow)
                UVEditorWindow.Instance?.OnTriangleSelectToolExit();

            EditorApplication.delayCall += () => Tools.current = Tool.Move;
            SelectedObjects = new GameObject[] { };

            Selection.selectionChanged -= onSelectionChanged;
            SceneViewDrawer.Instance().OnRender -= onRenderMesh;

            Instance = null;
        }

        public GameObject[] SelectedObjects = new GameObject[] { };

        // flags & temp
        protected bool _mouseIsDown;
        protected bool _mouseIsInSceneView;
        protected bool _mouseEnteredSceneView;
        protected bool _leftMouseIsDown;
        protected bool _leftMouseWasPressed;
        protected bool _leftMouseWasReleased;
        protected bool _shiftPressed;
        protected bool _altPressed;
        protected bool _controlPressed;
        protected bool _scrollWheelTurned;
        protected double _lastMouseDragTime;
        protected double _lastScrollWheelTime;
        protected int _toolActiveFrameCount;
        protected bool _autoEnterPaintMode;

        public UVEditorWindow UVEditor
        {
            get => UVEditorWindow.Instance;
        }

        public bool HasValidObjectSelection => SelectedObjects.Length > 0 && SelectedObjects[0] != null;

        public override void OnActivated()
        {
            Instance = this;

            if (UVEditor == null)
            {
                ExitTool();
                bool result = EditorUtility.DisplayDialog(
                   "No UV Editor Window",
                   "You have to open the UV Editor Window and select an object before using the triangle select tool.\n\nWindow > UV Editor Window",
                   "OK", "Open UV Editor");
                if (!result)
                {
                    UVEditorWindow.ShowWindow();
                }
                return;
            }

            if (Selection.activeGameObject != null)
            {
                if (UVEditor != null && UVEditor.SelectedGameObject == null)
                {
                    UVEditor.SetSelectedObject(Selection.activeGameObject);
                }
            }

            if (UVEditor != null)
            {
                UVEditor.StartTriangleSelecting();
            }

            updateSelection();

            if (HasValidObjectSelection)
            {
                restartSelectionPainting();
            }

            _toolActiveFrameCount = 0;

            Selection.selectionChanged -= onSelectionChanged;
            Selection.selectionChanged += onSelectionChanged;

            var sceneViewDrawer = SceneViewDrawer.Instance();
            sceneViewDrawer.OnRender -= onRenderMesh;
            sceneViewDrawer.OnRender += onRenderMesh;

            EditorSceneManager.sceneOpened -= onSceneOpened;
            EditorSceneManager.sceneOpened += onSceneOpened;

            PrefabStage.prefabStageOpened -= onClearDueToPrefabStage;
            PrefabStage.prefabStageOpened += onClearDueToPrefabStage;

            PrefabStage.prefabStageClosing -= onClearDueToPrefabStage;
            PrefabStage.prefabStageClosing += onClearDueToPrefabStage;
        }

        void updateSelection()
        {
            if (SelectedObjects.Length == 0 || SelectedObjects[0] != UVEditor.SelectedGameObject)
                SelectedObjects = new GameObject[] { UVEditor.SelectedGameObject };
        }

        void onClearDueToPrefabStage(PrefabStage obj)
        {
            ClearSelection();
        }

        void onSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ClearSelection();
        }

        public override void OnWillBeDeactivated()
        {
        }

        /// <summary>
        /// A method to activate the tool. Though remember, this is not always called.
        /// The tool is actually a ScriptableObject which is instantiated and deserialized by Unity.
        /// </summary>
        // [MenuItem("Tools/UV Editor/Start", priority = 1)]
        public static void Activate()
        {
#if UNITY_2020_2_OR_NEWER
            ToolManager.SetActiveTool(typeof(UVTriangleSelectTool));
#else
            EditorTools.SetActiveTool(typeof(UVEditorTool));
#endif

            SceneView.lastActiveSceneView.Focus();
        }

        public void OnToolChanged()
        {
            if (UVTriangleSelectToolActiveState.IsActive)
            {
                SceneView.lastActiveSceneView.Focus();

                initWindowSize();

                _mouseIsDown = false;
                _leftMouseIsDown = false;

                _autoEnterPaintMode = true;
            }

            if (UVEditorWindow.Instance != null)
                UVEditorWindow.Instance.OnToolChanged();
        }

        public void Restart()
        {
            ClearSelection();
            TriangleCache.Clear();
            restartSelectionPainting();
        }

        // Will be called after all regular rendering is done
        public void onRenderMesh()
        {
            onSelectRenderMesh();
        }

        // Equivalent to Editor.OnSceneGUI.
        public override void OnToolGUI(EditorWindow window)
        {
            _toolActiveFrameCount++;

            if (UVEditor == null || UVEditor.SelectedGameObject == null)
            {
                DrawLabel("Not object selected in UV Editor Window");
                return;
            }

            // Update selection
            updateSelection();

            if (!(window is SceneView sceneView))
            {
                return;
            }

            var settings = UVEditorSettings.GetOrCreateSettings();
            var current = Event.current;

            // Detect if the mouse returns into the scene view
            if (current.type == EventType.Repaint)
            {
                bool mouseIsInSceneView = IsMouseInSceneView();
                if (!_mouseIsInSceneView && mouseIsInSceneView)
                {
                    _mouseEnteredSceneView = true;
                }
                _mouseIsInSceneView = mouseIsInSceneView;
            }

            // handle key presses & draw handles
            int passiveControlID = GUIUtility.GetControlID(FocusType.Passive);

            // Key events
            bool keyEvent = false;
            bool useKey = false;
            _shiftPressed = current.shift;
            _controlPressed = current.control;
            if (current.type == EventType.KeyDown)
            {
                keyEvent = true;

                // undo redo
                if ((SceneViewIsActive() || IsMouseInSceneView())
                    && current.isKey && (current.control || current.command)
                    )
                {
                    if (current.keyCode == settings.UndoKey)
                    {
                        Undo();
                        useKey = true;
                    }
                    else if (current.keyCode == settings.RedoKey)
                    {
                        Redo();
                        useKey = true;
                    }
                }

                if (current.keyCode == KeyCode.Escape)
                {
                    useKey = true;
                    ExitTool();
                }

                if (current.keyCode == KeyCode.LeftAlt)
                {
                    _altPressed = true;
                }
            }

            if (_saveLoadVisibility == 0
                && current.isKey && current.modifiers == EventModifiers.None && current.keyCode == UVEditorSettings.GetOrCreateSettings().TriggerSelectLinked
                && !Tools.viewToolActive)
            {
                keyEvent = true;
                useKey = true;
            }

            // Mouse events
            bool mouseEvent = false;
            if (current.type == EventType.MouseDown)
            {
                _mouseIsDown = true;
                if (current.button == 0)
                {
                    _leftMouseIsDown = true;
                    _leftMouseWasPressed = true;
                }
                mouseEvent = true;
            }
            else if (current.type == EventType.MouseUp)
            {
                _mouseIsDown = false;
                _leftMouseIsDown = false;
                _leftMouseWasReleased = true;
                mouseEvent = true;
            }
            else if (current.type == EventType.MouseDrag)
            {
                _mouseIsDown = true;
                mouseEvent = true;
                _lastMouseDragTime = EditorApplication.timeSinceStartup;
                if (current.button == 0)
                {
                    _leftMouseIsDown = true;
                }
            }
            else if (current.type == EventType.MouseMove)
            {
                // fixing mouse down
                bool timedOut = EditorApplication.timeSinceStartup - _lastMouseDragTime > 0.05f;
                if (_mouseIsDown && timedOut)
                {
                    _mouseIsDown = false;
                }
                if (_leftMouseIsDown && timedOut)
                {
                    _leftMouseIsDown = false;
                }
                mouseEvent = true;
            }

            bool scrollWheelEvent = false;
            if (current.type == EventType.ScrollWheel)
            {
                _scrollWheelTurned = true;
                _lastScrollWheelTime = EditorApplication.timeSinceStartup;
                scrollWheelEvent = true;
            }

            drawWindow(sceneView, passiveControlID + 1);


            onSelectGUI(sceneView);

            // Restore selection after the mouse entered the scene view again.
            // And also do not auto select if the settings have been opened recently.
            if (_mouseEnteredSceneView && SelectedObjects.Length > 0
                && EditorApplication.timeSinceStartup - _lastSettingsOpenTimestamp > 2)
            {
                restoreSelected();
            }

            if (mouseEvent
                // Don't consume mouse events if a view tool is active.
                && !Tools.viewToolActive && !current.alt
                // Consume only left mouse button down or drag events.
                && current.button == 0
                && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                )
            {
                if (IsMouseInSceneView()
                    // Do not consume if CTRL is pressed AND no triangles are hovered.
                    && !(current.control && _hoverTriangles.Count == 0)
                    )
                {
                    GUIUtility.hotControl = passiveControlID;
                    Event.current.Use();
                }
            }

            // Consume scroll wheel events.
            if (scrollWheelEvent)
            {
                if (selectConsumesScrollWheelEvent(current))
                {
                    // Do not set the hot control to inactive if teh scroll whell was used (otherwise it would prohibit key press detection afterwards).
                    Event.current.Use();
                }
            }
            else if (keyEvent && useKey)
            {
                Event.current.Use();
            }

            resetFlags();
        }

        void restoreSelected()
        {
            List<GameObject> selectedObjectsInScene = getValidSelection();

            if (selectedObjectsInScene.Count == 0)
            {
                var newSelectedObjects = new List<GameObject>(SelectedObjects);

                // Add the object from the selected triangles
                foreach (var tri in _selectedTriangles)
                {
                    if(tri.Component == null)
                    {
                        continue;
                    }

                    if (!selectedObjectsInScene.Contains(tri.Component.gameObject))
                    {
                        bool isChild = false;
                        foreach (var selectedObj in SelectedObjects)
                        {
                            if (tri.Component.transform.IsChildOf(selectedObj.transform))
                            {
                                isChild = true;
                                break;
                            }
                        }

                        if (!isChild && UtilsEditor.IsInScene(tri.Component.gameObject))
                        {
                            newSelectedObjects.Add(tri.Component.gameObject);
                        }
                    }
                }

                Selection.objects = newSelectedObjects.ToArray();
            }
        }

        List<GameObject> getValidSelection()
        {
            return new List<GameObject>() { UVEditor.SelectedGameObject };
        }

        private void resetFlags()
        {
            _leftMouseWasPressed = false;
            _leftMouseWasReleased = false;
            _scrollWheelTurned = false;
            _altPressed = false;
            _mouseEnteredSceneView = false;
        }

        public void restartSelectionPainting()
        {
            _autoEnterPaintMode = false;

            DefragTriangles();

            // Refresh cache
            if (_selectedTriangles.Count == 0)
            {
                TriangleCache.CacheTriangles(SceneView.lastActiveSceneView.camera, SelectedObjects);
                TriangleCache.RebuildBakedMeshesCache(SelectedObjects);
            }

            if (didCameraChange(SceneView.lastActiveSceneView.camera))
            {
                TriangleCache.CacheTriangles(SceneView.lastActiveSceneView.camera, SelectedObjects);
                // Make sure the cached world space is in sync.
                updateSelectedTrianglesAfterCacheChange();
            }
        }

        public static Camera GetValidSceneViewCamera()
        {
            var cam = SceneView.lastActiveSceneView.camera;
            if (cam != null && cam.transform.position != Vector3.zero)
            {
                return cam;
            }

            return null;
        }


        public static bool SceneViewIsActive()
        {
            return EditorWindow.focusedWindow == SceneView.lastActiveSceneView;
        }

        public static bool IsMouseInSceneView()
        {
            return EditorWindow.mouseOverWindow != null && SceneView.sceneViews.Contains(EditorWindow.mouseOverWindow);
        }

        void onSelectionChanged()
        {
            // We do this to trigger a cache renewal after the selection changed.
            resetCameraMemory();
        }
    }
}
