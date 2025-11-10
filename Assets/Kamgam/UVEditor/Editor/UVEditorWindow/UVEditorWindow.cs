using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Kamgam.UVEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace Kamgam.UVEditor
{
    public partial class UVEditorWindow : EditorWindow
    {
        public const string UVEditorCopyMarker = "_UVEditorCopy";

        [System.NonSerialized]
        public static UVEditorWindow Instance;

        public VisualTreeAsset Layout;

        [System.NonSerialized] protected VisualElement _root;
        [System.NonSerialized] protected VisualElement _canvas;
        [System.NonSerialized] protected VisualElement _gridContainer;
        [System.NonSerialized] protected VisualElement _textureContainer;
        [System.NonSerialized] protected VisualElement _textureSliceContainer;
        [System.NonSerialized] protected TriangleDrawer _selectedTriContainer;
        [System.NonSerialized] protected TriangleDrawer _highlightedTriContainer;
        [System.NonSerialized] protected List<LineDrawer> _uvLinesContainers = new List<LineDrawer>();
        [System.NonSerialized] protected LineDrawer _currentUVLinesContainer;
        [System.NonSerialized] protected LineDrawer _vertexContainer;
        [System.NonSerialized] protected LineDrawer _selectionRectContainer;

        [System.NonSerialized] protected LineDrawer _rectUVGizmoContainer;

        [System.NonSerialized] protected VisualElement _blScaleGizmo;
        [System.NonSerialized] protected VisualElement _tlScaleGizmo;
        [System.NonSerialized] protected VisualElement _trScaleGizmo;
        [System.NonSerialized] protected VisualElement _brScaleGizmo;

        [System.NonSerialized] protected VisualElement _blRotateGizmo;
        [System.NonSerialized] protected VisualElement _tlRotateGizmo;
        [System.NonSerialized] protected VisualElement _trRotateGizmo;
        [System.NonSerialized] protected VisualElement _brRotateGizmo;

        [System.NonSerialized] protected VisualElement _centerMoveGizmo;

        [System.NonSerialized] protected LineDrawer _rectCropGizmoContainer;
        [System.NonSerialized] protected VisualElement _blCropGizmo;
        [System.NonSerialized] protected VisualElement _tlCropGizmo;
        [System.NonSerialized] protected VisualElement _trCropGizmo;
        [System.NonSerialized] protected VisualElement _brCropGizmo;

        [System.NonSerialized] protected VisualElement _cursorTargetElement;

        /// <summary>
        /// Bounding rect of the selected UVs in UV space (bottom left = coordinate origin)
        /// </summary>
        [System.NonSerialized] protected Rect _selRectInUVs;

        [System.NonSerialized] protected Rect _cropRectInUVs;

        // The texture that is used or edited. This can be a reference to the original texture of the mesh or a copy (if texture editing or cropping was used).
        [System.NonSerialized] protected Texture2D _texture = null;
        // The texture material that is used or edited. This can be a reference to the original material of the mesh or a copy (if texture editing or cropping was used).
        [System.NonSerialized] protected Material _textureMaterial = null;
        [System.NonSerialized] protected Texture2D _gridTexture;
        [System.NonSerialized] protected float _zoom = 1.0f;
        [System.NonSerialized] protected float _previousZoom; 
        [System.NonSerialized] protected Vector2 _pan = Vector2.zero;
        [System.NonSerialized] protected Vector2 _canvasMousePos = Vector2.zero;
        [System.NonSerialized] protected Vector2 _textureMousePos = Vector2.zero;
        [System.NonSerialized] protected int _uvChannel = 0;
        public enum UVFilter { All = 0, Selected = 1 };
        [System.NonSerialized] protected UVFilter _uvFilter = UVFilter.All;
        [System.NonSerialized] protected Vector2 _uvMousePos = Vector2.zero;

        // Key events are triggered both by the onKeyEvent() and move/drap in UIToolkit (we use both to be extra sure we catch them).
        [System.NonSerialized] protected bool _ctrlIsPressed;
        [System.NonSerialized] protected bool _altIsPressed;

        [System.NonSerialized] public Color UVLineColor = Color.white;
        [System.NonSerialized] public Color UVGimzoColor = new Color(1f, 1f, 0f, 1f);
        [System.NonSerialized] public Color TextureGimzoColor = new Color(0f, 1f, 0f, 1f);
        [System.NonSerialized] public Color CropGimzoColor = new Color(1f, 1f, 1f, 1f);
        [System.NonSerialized] protected Texture2D _checkerTexture;
        [System.NonSerialized] protected bool _editTextureEnabled;
        [System.NonSerialized] protected bool _editCropEnabled;
        /// <summary>
        /// If TRUE = Triangles select, FALSE = Vertices select (default).
        /// </summary>
        [System.NonSerialized] protected bool _triangleSelectEnabled;
        public bool TriangleSelectEnabled => _triangleSelectEnabled;
        [System.NonSerialized] protected bool _applyChangesImmediately = false;

        [System.NonSerialized] protected Dictionary<Tuple<UnityEngine.Object, int>, List<Vector2>> _uvWorkingCopy = new Dictionary<Tuple<UnityEngine.Object, int>, List<Vector2>>();
        [System.NonSerialized] protected Rect _gizmoScaleRectBR;
        [System.NonSerialized] protected Rect _gizmoScaleRectTR;
        [System.NonSerialized] protected Rect _gizmoScaleRectTL;
        [System.NonSerialized] protected Rect _gizmoScaleRectBL;
        [System.NonSerialized] protected int _rectGizmoDraggedScaleHandle = 0; // 0 = no drag, 1 = BL, 2 = TL, 3 = TR, 4 = BR
        [System.NonSerialized] protected int _rectGizmoDraggedRotationHandle = 0; // 0 = no drag, 1 = BL, 2 = TL, 3 = TR, 4 = BR
        [System.NonSerialized] protected int _rectGizmoDraggedMoveHandle = 0; // 0 = no drag, 1 = move
        [System.NonSerialized] protected int _rectGizmoDraggedCropHandle = 0; // 0 = no drag, 1 = BL, 2 = TL, 3 = TR, 4 = BR
        [System.NonSerialized] protected UnityEngine.UIElements.Cursor? _nextCursorOnMouseUp = null;

        [System.NonSerialized] protected Rect _gizmoCropRectBR;
        [System.NonSerialized] protected Rect _gizmoCropRectTR;
        [System.NonSerialized] protected Rect _gizmoCropRectTL;
        [System.NonSerialized] protected Rect _gizmoCropRectBL;
        [System.NonSerialized] protected int _cropGizmoDraggedScaleHandle = 0; // 0 = no drag, 1 = BL, 2 = TL, 3 = TR, 4 = BR

        [System.NonSerialized] protected Dictionary<Tuple<UnityEngine.Object, int>, Texture2D> _newTextures = new Dictionary<Tuple<UnityEngine.Object, int>, Texture2D>();
        [System.NonSerialized] protected Dictionary<Tuple<UnityEngine.Object, int>, Material> _newMaterials = new Dictionary<Tuple<UnityEngine.Object, int>, Material>();
        [System.NonSerialized] protected Dictionary<UnityEngine.Object, Mesh> _newMeshes = new Dictionary<UnityEngine.Object, Mesh>();

        // Contains a unique list of vertex ids of all selected UV vertices. That means those of selected triangles and those manually selected.
        [System.NonSerialized] protected List<int> _selectedVertices = new List<int>();

        [System.NonSerialized] protected Vector2 _selectionRectStart;
        [System.NonSerialized] protected Vector2 _selectionRectEnd;

        /// <summary>
        /// The max duration in Milliseconds the mouse button hcan to be pressed to be counted as a click.<br />
        /// Mouse presses that are longer than this will not count as clicks.
        /// </summary>
        const long CLICK_DELAY_MS = 200;

        private List<int> _tmpVerticesUnderMouse = new List<int>();

        [MenuItem("Tools/UV Editor/Open Window", priority = 2)]
        [MenuItem("Window/UV Editor Window")]
        public static void ShowWindow()
        {
            UVEditorWindow wnd = GetWindow<UVEditorWindow>();
            wnd.titleContent = new GUIContent("UV Editor");

            Instance = wnd;

            GlobalKeyEventHandler.OnKeyEvent -= Instance.onKeyEvent;
            GlobalKeyEventHandler.OnKeyEvent += Instance.onKeyEvent;
        }

        public void OnEnable()
        {
            Instance = this;
            GlobalKeyEventHandler.OnKeyEvent -= onKeyEvent;
            GlobalKeyEventHandler.OnKeyEvent += onKeyEvent;
            _uvsAreDirty = true;
        }

        [System.NonSerialized] Vector2 _lastWindowSize;

        public void Update()
        {
            var size = new Vector2(position.width, position.height);
            if (_lastWindowSize != size)
            {
                _lastWindowSize = size;
                _uvsAreDirty = true; 
            }
        }

        public void OnDestroy()
        {
            Instance = null;
        }

        public void OnDisable()
        {
            Instance = null;
        }

        public static bool UVWindowIsActive()
        {
            return EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType() == typeof(UVEditorWindow);
        }

        public static bool IsMouseInUVWindow()
        {
            return EditorWindow.mouseOverWindow != null && EditorWindow.mouseOverWindow.GetType() == typeof(UVEditorWindow);
        }

        protected Renderer getSelectedRenderer()
        {
            if (_selectedMeshFilter != null)
                return _selectedMeshFilter.gameObject.GetComponent<MeshRenderer>();

            if (_selectedSkinnedRenderer != null)
                return _selectedSkinnedRenderer;

            return null;
        }

        /// <summary>
        /// Returns the working copy of the UVs of the current selection (creates a copy if none exists).
        /// </summary>
        /// <param name="uvChannel"></param>
        /// <returns></returns>
        protected List<Vector2> getUVWorkingCopyForSelection(int uvChannel)
        {
            if (_selectedMesh != null)
            {
                return getUVsWorkingCopy(_selectedObject, uvChannel, _selectedMesh);
            }

            return null;
        }

        public void ClearCacheAndSelection()
        {
            clearMeshCache();
            _selectedVertices.Clear();
            onSelectedVerticesChanged();
        }

        void clearMeshCache()
        {
            _uvWorkingCopy.Clear();
            _newMaterials.Clear();
            _newMeshes.Clear();
            _newTextures.Clear();
            _texture = null;
            _textureMaterial = null;

            _uvsAreDirty = true;
        }

        public void Restart()
        {
            if (_editTextureEnabled)
            {
                StopTextureEditing();
            }
        }

        /// <summary>
        /// Returns the working copy of the UVs of the current selection (creates a copy if none exists).
        /// </summary>
        /// <param name="comp"></param>
        /// <param name="uvChannel"></param>
        /// <returns></returns>
        protected List<Vector2> getUVsWorkingCopy(UnityEngine.Object obj, int uvChannel, Mesh mesh)
        {
            var key = new Tuple<UnityEngine.Object, int>(obj, uvChannel);
            if (_uvWorkingCopy.ContainsKey(key))
            {
                return _uvWorkingCopy[key];
            }
            else
            {
                // Create UV working copy if necessary.
                var uvs = new List<Vector2>();
                mesh.GetUVs(uvChannel, uvs); // TriangleCache.GetCachedUVs(comp, uvChannel);
                if (uvs == null)
                    return null;
                
                var copy = new List<Vector2>(uvs);
                _uvWorkingCopy.Add(key, copy);
                return copy;
            }
        }

        protected void setUVsWorkingCopy(UnityEngine.Object obj, int uvChannel, List<Vector2> uvs) 
        {
            var key = new Tuple<UnityEngine.Object, int>(obj, uvChannel);
            if (_uvWorkingCopy.ContainsKey(key))
            {
                _uvWorkingCopy[key] = new List<Vector2>(uvs);
            }
            else
            {
                _uvWorkingCopy.Add(key, new List<Vector2>(uvs));
            }
        }

        protected void removeUVsWorkingCopy(UnityEngine.Object obj, int uvChannel)
        {
            var key = new Tuple<UnityEngine.Object, int>(obj, uvChannel);
            if (_uvWorkingCopy.ContainsKey(key))
            {
                _uvWorkingCopy.Remove(key);
            }
        }

        private void onKeyEvent(Event evt)
        {
            var settings = UVEditorSettings.GetOrCreateSettings();
            bool stopKeyEvent = false;


            if (IsMouseInUVWindow() || UVWindowIsActive())
            {
   
                // Undo 
                if (evt.type == EventType.KeyDown && evt.keyCode == settings.UndoKey && evt.control)
                {
                    undo();
                    stopKeyEvent = true;
                }

                // Redo
                if (evt.type == EventType.KeyDown && evt.keyCode == settings.RedoKey && evt.control)
                {
                    redo();
                    stopKeyEvent = true;
                }

                if (evt.isKey && evt.keyCode == KeyCode.LeftAlt)
                {
                    _altIsPressed = evt.type == EventType.KeyDown;
                }

                // Vertices Selection Mode
                if (evt.isKey && evt.keyCode == KeyCode.V && !evt.control && !evt.alt)
                {
                    StopTriangleSelecting();
                }

                // Triangles Selection Mode
                if (evt.isKey && evt.keyCode == KeyCode.T && !evt.control && !evt.alt)
                {
                    StartTriangleSelecting();
                }

                if (evt.isKey && (evt.keyCode == KeyCode.LeftControl || evt.keyCode == KeyCode.LeftControl))
                {
                    _ctrlIsPressed = evt.type == EventType.KeyDown;
                }

                // Select linked (don't execute if control or alt is pressed)
                if (evt.type == EventType.KeyDown && evt.keyCode == settings.TriggerSelectLinked && !evt.control && !evt.alt)
                {
                    if (_triangleSelectEnabled)
                        trianglesSelectLinked(remove: evt.shift);
                    else
                        verticesSelectLinked(remove: evt.shift);
                }

                // Toggle Focus
                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.F && !evt.control && !evt.shift)
                {
                    ToggleFocusSelection();
                }

                if (evt.type == EventType.KeyDown && evt.control && evt.keyCode == KeyCode.C)
                {
                    CopyUVs(_uvChannel);
                }

                if (evt.type == EventType.KeyDown && evt.control && evt.keyCode == KeyCode.V)
                {
                    PasteUVs(_uvChannel);
                }

            }

            if (stopKeyEvent)
                evt.Use();
        }

        private long _leftMouseDownTime;
        private Vector2 _leftMouseDownPosition;

        private void OnMouseDown(MouseDownEvent evt)
        {
            // Start panning with ALT + LMB or MMB
            if (evt.altKey || evt.IsMiddlePressed())
            {
                // _cursorTargetElement.style.cursor = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.Pan);
                _canvas.CaptureMouse();
            }

            // Track mouse down time (for "Select vertices").
            if (evt.IsLeftPressed())
            {
                _leftMouseDownTime = evt.timestamp;
                _leftMouseDownPosition = evt.mousePosition;
            }

            if (!_editCropEnabled && evt.IsLeftPressed())
            {
                // bool rectHasArea = !Mathf.Approximately(_selRectInUVs.width, 0f) && !Mathf.Approximately(_selRectInUVs.height, 0f);

                // Scale
                if (!_editTextureEnabled && _rectGizmoDraggedScaleHandle == 0)
                {
                    if (_gizmoScaleRectBL.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedScaleHandle = 1;
                        startScalingUVs(_rectGizmoDraggedScaleHandle, evt);
                    }
                    else if (_gizmoScaleRectTL.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedScaleHandle = 2;
                        startScalingUVs(_rectGizmoDraggedScaleHandle, evt);
                    }
                    else if (_gizmoScaleRectTR.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedScaleHandle = 3;
                        startScalingUVs(_rectGizmoDraggedScaleHandle, evt);
                    }
                    else if (_gizmoScaleRectBR.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedScaleHandle = 4;
                        startScalingUVs(_rectGizmoDraggedScaleHandle, evt);
                    }
                }

                // Rotate
                if (!_editTextureEnabled && _rectGizmoDraggedRotationHandle == 0)
                {
                    if (_blRotateGizmo.worldBound.Contains(evt.mousePosition))
                    {
                        _rectGizmoDraggedRotationHandle = 1;
                        startRotatingUVs(_rectGizmoDraggedRotationHandle, evt);
                    }
                    else if (_tlRotateGizmo.worldBound.Contains(evt.mousePosition))
                    {
                        _rectGizmoDraggedRotationHandle = 2;
                        startRotatingUVs(_rectGizmoDraggedRotationHandle, evt);
                    }
                    else if (_trRotateGizmo.worldBound.Contains(evt.mousePosition))
                    {
                        _rectGizmoDraggedRotationHandle = 3;
                        startRotatingUVs(_rectGizmoDraggedRotationHandle, evt);
                    }
                    else if (_brRotateGizmo.worldBound.Contains(evt.mousePosition))
                    {
                        _rectGizmoDraggedRotationHandle = 4;
                        startRotatingUVs(_rectGizmoDraggedRotationHandle, evt);
                    }
                }

                // Move
                // Notice: The move area covers a lot of the area and if in vertex edit mode we have to make sure
                //         the user can still select vertices even if the mouse is inside the move area. Thus we
                //         do the move check in mouse UP once we are sure it is not a click action but a drag.
                //         Moving UVs is started here only if vertex editing is OFF, see OnMouseUp(..) otherwise.
                // Start moving UVs right here
                tryStartMovingUVs(evt, evt.mousePosition);

                // Start dragging a handle
                if (_rectGizmoDraggedScaleHandle > 0 || _rectGizmoDraggedRotationHandle > 0 || _rectGizmoDraggedMoveHandle > 0)
                    _canvas.CaptureMouse();
            }

            if (_editCropEnabled)
            {
                // Crop
                if (_rectGizmoDraggedCropHandle == 0)
                {
                    if (_gizmoCropRectBL.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedCropHandle = 1;
                        startCropDrag(_rectGizmoDraggedCropHandle, evt);
                    }
                    else if (_gizmoCropRectTL.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedCropHandle = 2;
                        startCropDrag(_rectGizmoDraggedCropHandle, evt);
                    }
                    else if (_gizmoCropRectTR.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedCropHandle = 3;
                        startCropDrag(_rectGizmoDraggedCropHandle, evt);
                    }
                    else if (_gizmoCropRectBR.Contains(_uvMousePos))
                    {
                        _rectGizmoDraggedCropHandle = 4;
                        startCropDrag(_rectGizmoDraggedCropHandle, evt);
                    }
                }

                // Start dragging a handle
                if (_rectGizmoDraggedCropHandle > 0)
                    _canvas.CaptureMouse();
            }

            if (evt.IsLeftPressed())
            {
                _selectionRectStart = _uvMousePos;
                _selectionRectEnd = _uvMousePos;

                // Modify selected vertices (only if no handle is used)
                if (!anyHandle() && !evt.shiftKey && !evt.ctrlKey)
                {
                    if (_selectedVertices.Count > 0)
                    {
                        UndoStack.Instance.StartEntry();
                        UndoStack.Instance.AddUndoAction(undoSetSelectedVerticesFunc());
                        _selectedVertices.Clear();
                        onSelectedVerticesChanged();
                        UndoStack.Instance.AddRedoAction(undoSetSelectedVerticesFunc());
                        UndoStack.Instance.EndEntry();
                    }
                }
            }
        }

        private bool anyHandle()
        {
            return _rectGizmoDraggedScaleHandle > 0
                || _rectGizmoDraggedMoveHandle > 0
                || _rectGizmoDraggedRotationHandle > 0
                || _rectGizmoDraggedCropHandle > 0;
        }


        private void tryStartMovingUVs(IMouseEvent evt, Vector2 mousePosition)
        {
            if (_rectGizmoDraggedMoveHandle == 0 && _centerMoveGizmo.worldBound.Contains(mousePosition) && !evt.altKey)
            {
                _rectGizmoDraggedMoveHandle = 1;
                startMovingUVs(evt);
                _canvas.CaptureMouse();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            // Sadly "evt.button == 0" does NOT work here, it's always 0, see bug report:
            // https://issuetracker.unity3d.com/issues/movemouseevent-dot-button-returns-0-both-when-no-button-is-pressed-and-when-the-left-mouse-button-is-pressed
            // We use our own extension methods instead, see:
            // See: https://discussions.unity.com/t/tutorial-resource-ui-toolkit-how-to-get-the-pressed-mouse-button-from-a-mousemoveevent/1554470
            bool isLeftButtonPressed = evt.IsLeftPressed();
            bool isRightButtonPressed = evt.IsRightPressed();
            bool isMiddleButtonPressed = evt.IsMiddlePressed();

            // We need the info whether or not the user is dragging.
            bool isDragging = false;
            if (isLeftButtonPressed)
            {
                long mouseDownDurationInMS = evt.timestamp - _leftMouseDownTime;
                var moveDistanceSinceDown = (evt.mousePosition - _leftMouseDownPosition).magnitude;
                if (mouseDownDurationInMS > CLICK_DELAY_MS + 10 || moveDistanceSinceDown > 10)
                {
                    isDragging = true;
                }
            }

            // Using mouse move to update key infos. This is pathetic but it works, kinda.
            // Could also use my own solution from the forums but I just don't want to becasue
            // god damn it there has to be an easier way of doing this. If you are interested:
            // https://discussions.unity.com/t/4-6-editorapplication-modifierkeyschanged-how-to-find-out-which-key-was-pressed/599061/10
            _ctrlIsPressed = evt.ctrlKey;
            _altIsPressed = evt.altKey;

            // Update Triangle Selection
            var pos = evt.mousePosition - new Vector2(rootVisualElement.resolvedStyle.left, rootVisualElement.resolvedStyle.top);
            _canvasMousePos = rootVisualElement.ChangeCoordinatesTo(_canvas, pos);
            _textureMousePos = rootVisualElement.ChangeCoordinatesTo(_textureContainer, pos);
            
            _uvMousePos = new Vector2(
                      _textureMousePos.x / _textureContainer.resolvedStyle.width,
                1f - (_textureMousePos.y / _textureContainer.resolvedStyle.height)
                );

            // Move UVs
            // Used only if vertex editing is ON and if it is NOT a click
            // See onMouseDown(..) for the implementation if vertex editing is off.
            if (!_editTextureEnabled && !_editCropEnabled && isDragging)
            {
                tryStartMovingUVs(evt, _leftMouseDownPosition);
            }

            // Dragging
            if (_canvas.HasMouseCapture())
            {
                if (_rectGizmoDraggedScaleHandle == 0
                    && _rectGizmoDraggedMoveHandle == 0
                    && _rectGizmoDraggedRotationHandle == 0
                    && _rectGizmoDraggedCropHandle == 0)
                {
                    // No gizmo handle drag? Then its a normal pan drag.
                    _pan += evt.mouseDelta;
                }
                else
                {
                    if (_rectGizmoDraggedScaleHandle > 0)
                        scaleUVs(_rectGizmoDraggedScaleHandle, evt);
                    else if (_rectGizmoDraggedRotationHandle > 0)
                        rotateUVs(_rectGizmoDraggedRotationHandle, evt);
                    else if (_rectGizmoDraggedMoveHandle > 0)
                        moveUVs(evt);
                    else if (_rectGizmoDraggedCropHandle > 0)
                        cropDrag(_rectGizmoDraggedCropHandle, evt);
                }
            }

            if (evt.IsLeftPressed() && !anyHandle())
            {
                _selectionRectEnd = _uvMousePos;
            }
        }

        public void OnSelectedTrianglesChanged(HashSet<SelectedTriangle> selectedTriangles)
        {
            if (selectedTriangles.Count == 0 && _selectedVertices.Count == 0)
                return;

            UndoStack.Instance.StartEntry();
            UndoStack.Instance.AddUndoAction(undoSetSelectedVerticesFunc());

            _selectedVertices.Clear();

            foreach (var tri in selectedTriangles)
            {
                _selectedVertices.AddIfNotContained(tri.TriangleIndices[0]);
                _selectedVertices.AddIfNotContained(tri.TriangleIndices[1]);
                _selectedVertices.AddIfNotContained(tri.TriangleIndices[2]);
            }

            onSelectedVerticesChanged();

            UndoStack.Instance.AddUndoAction(undoSetSelectedVerticesFunc());
            UndoStack.Instance.EndEntry();
        }

        private void refreshInEditor()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
        }

        private void OnScroll(WheelEvent evt)
        {
            // Zoom the texture
            float oldZoom = _zoom;
            _zoom -= (evt.delta.y * 0.01f) * _zoom;
            _zoom = Mathf.Clamp(_zoom, 0.1f, 100f);

            // Use zoom delta to update pan position to
            // give the illusion of zooming from the current
            // mouse position.
            float zoomDelta = _zoom / oldZoom;
            var textureCenter = new Vector2(
                _textureContainer.resolvedStyle.left + _textureContainer.resolvedStyle.width * 0.5f,
                _textureContainer.resolvedStyle.top + _textureContainer.resolvedStyle.height * 0.5f
                );
            var deltaOfMouseToTextureCenter = _canvasMousePos - textureCenter;
            _pan -= (deltaOfMouseToTextureCenter * zoomDelta - deltaOfMouseToTextureCenter); 

            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            long clickDurationMS = evt.timestamp - _leftMouseDownTime;
            bool isClick = (clickDurationMS < CLICK_DELAY_MS);

            if (_canvas.HasMouseCapture())
            {
                _canvas.ReleaseMouse();
            }

            bool didChangeSelectedVertices = false;

            // Record undo action.
            UndoStack.Instance.StartGroup();

            // Left mouse button released
            if (evt.button == 0)
            {
                // Select the vertices that are inside the selection rect. 
                if (!anyHandle() && !_editCropEnabled)
                {
                    var uvs = getUVWorkingCopyForSelection(_uvChannel);
                    if (uvs.Count > 0)
                    {
                        UndoStack.Instance.StartEntry();
                        UndoStack.Instance.AddUndoAction(undoSetSelectedVerticesFunc());

                        // Material infos for edit texture mode (select only those vertices that have the same material).
                        var tmpMaterials = GetRendererFromGameObject(SelectedGameObject).sharedMaterials;

                        Rect rect = getSelectionRectUV();
                        calcCanvasSize(out _, out _, out float size, out _);
                        List<int> subMeshTris = new List<int>();
                        for (int subMeshIndex = 0; subMeshIndex < _selectedMesh.subMeshCount; subMeshIndex++)
                        {
                            _selectedMesh.GetTriangles(subMeshTris, subMeshIndex);

                            int numOfVertices = subMeshTris.Count;
                            for (int v = 0; v < numOfVertices; v++)
                            {
                                // Skip vertices of other materials in edit-texture mode.
                                if (_editTextureEnabled && tmpMaterials[subMeshIndex] != _textureMaterial)
                                    continue;

                                int index = subMeshTris[v];
                                var x = uvs[index].x;
                                var y = uvs[index].y;

                                if (rect.Contains(uvs[index]))
                                {
                                    if (evt.ctrlKey)
                                    {
                                        // Remove
                                        _selectedVertices.Remove(index);
                                        didChangeSelectedVertices = true;
                                    }
                                    else
                                    {
                                        // Add
                                        _selectedVertices.AddIfNotContained(index);
                                        didChangeSelectedVertices = true;
                                    }
                                }
                            }
                        }

                        if (didChangeSelectedVertices)
                        {
                            didChangeSelectedVertices = false;
                            onSelectedVerticesChanged();

                            // Record redo action
                            UndoStack.Instance.AddRedoAction(undoSetSelectedVerticesFunc());
                            UndoStack.Instance.EndEntry();
                        }
                        else
                        {
                            UndoStack.Instance.CancelEntry();
                        }
                    }

                    // Reset selection rect
                    _selectionRectStart = _uvMousePos;
                    _selectionRectEnd = _uvMousePos;
                }

                if (_rectGizmoDraggedScaleHandle != 0)
                {
                    stopScalingUVs(_rectGizmoDraggedScaleHandle);
                    _rectGizmoDraggedScaleHandle = 0;
                }

                if (_rectGizmoDraggedMoveHandle != 0)
                {
                    stopMovingUVs();
                    _rectGizmoDraggedMoveHandle = 0;
                }

                if (_rectGizmoDraggedRotationHandle != 0)
                {
                    stopRotatingUVs(_rectGizmoDraggedRotationHandle);
                    _rectGizmoDraggedRotationHandle = 0;
                }

                if (_rectGizmoDraggedCropHandle != 0)
                {
                    stopCropDrag(_rectGizmoDraggedCropHandle);
                    _rectGizmoDraggedCropHandle = 0;
                }
            }

            // Record undo state
            UndoStack.Instance.EndGroup();

            // Reset cursor if needed
            if (_nextCursorOnMouseUp.HasValue && _cursorTargetElement != null)
            {
                _cursorTargetElement.style.cursor = _nextCursorOnMouseUp.Value;
            }

            // Ensure mouse down time is in the future if mouse UP was triggered.
            if (evt.button == 0)
            {
                _leftMouseDownTime = 999_000_000;
            }
        }

        private void onSelectedVerticesChanged()
        {
            updateSelectionRect();
            _selectionChangedSinceLastFocus = true;
        }
    }
}