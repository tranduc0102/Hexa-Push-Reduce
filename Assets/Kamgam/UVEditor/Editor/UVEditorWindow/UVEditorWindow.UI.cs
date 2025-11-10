using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Kamgam.UVEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.EditorTools;


namespace Kamgam.UVEditor
{
    public partial class UVEditorWindow : EditorWindow
    {
        public bool IsEditingTexture => _editTextureEnabled;

        [System.NonSerialized] protected Button _editTextureButton;
        [System.NonSerialized] protected Button _editCropTextureButton;
        [System.NonSerialized] protected Button _triangleSelectionButton;
        [System.NonSerialized] protected Button _trianglesSelectLinkedButton;
        [System.NonSerialized] protected DropdownField _trianglesUVFilterDropDown;
        [System.NonSerialized] protected Button _verticesSelectionButton;
        [System.NonSerialized] protected Button _verticesSelectLinkedButton;
        [System.NonSerialized] protected Button _applyButton;
        
        [System.NonSerialized] protected Toggle _liveUVsToggle;
        [System.NonSerialized] protected List<VisualElement> _cropBottomTools; // All elements with class crop-bottom-toolbar
        [System.NonSerialized] protected TextField _cropWidthInput;
        [System.NonSerialized] protected TextField _cropHeightInput;

        [System.NonSerialized] protected bool _focusedOnSelection;
        [System.NonSerialized] protected bool _uvsAreDirty = true;
        [System.NonSerialized] protected bool _selectionChangedSinceLastFocus = true;
        

        [System.NonSerialized] protected UnityEngine.Object _selectedObject;
        public GameObject SelectedGameObject
        {
            get
            {
                if (_selectedMeshFilter != null)
                    return _selectedMeshFilter.gameObject;
                else if (_selectedSkinnedRenderer != null)
                    return _selectedSkinnedRenderer.gameObject;
                else
                    return null;
            }
        }

        [System.NonSerialized] protected MeshFilter _selectedMeshFilter;
        [System.NonSerialized] protected SkinnedMeshRenderer _selectedSkinnedRenderer;
        [System.NonSerialized] protected Mesh _selectedMesh;
        [System.NonSerialized] protected int _selectedSubMeshIndex = 0;

        protected ObjectField _selectedObjectField;
        protected ObjectField selectedObjectField
        {
            get
            {
                if (_selectedObjectField == null)
                {
                    _selectedObjectField = _root.Q<ObjectField>(name: "SelectedObject");
                }
                return _selectedObjectField;
            }
        }

        public void CreateGUI()
        {
            Instance = this;

            _gridTexture = findAndLoadAsset<Texture2D>("Texture", "UVEditorGrid");

            _root = Layout.Instantiate();
            _root.style.flexGrow = 1;
            rootVisualElement.Add(_root);

            // dynamic skin support
            if (EditorGUIUtility.isProSkin)
            {
                _root.AddToClassList("dark-skin");
                _root.RemoveFromClassList("light-skin");
            }
            else
            {
                _root.RemoveFromClassList("dark-skin");
                _root.AddToClassList("light-skin");
            }

            var selectedObjectFieldBig = _root.Q<ObjectField>(name: "SelectedObjectBig");
            selectedObjectFieldBig.SetValueWithoutNotify(_selectedObject);
            selectedObjectFieldBig.RegisterValueChangedCallback((e) =>
            {
                selectedObjectField.value = e.newValue;
            });

            selectedObjectField.SetValueWithoutNotify(_selectedObject);
            selectedObjectField.RegisterValueChangedCallback((e) =>
            {
                SetSelectedObject(e.newValue);
                selectedObjectFieldBig.SetValueWithoutNotify(e.newValue);
                ClearCacheAndSelection();
                UndoStack.Instance.Clear();
            });

            var subMeshIndexDropDown = _root.Q<DropdownField>(name: "SelectedSubMeshIndex");
            subMeshIndexDropDown.RegisterValueChangedCallback((e) =>
            {
                if (int.TryParse(e.newValue, out int subMeshIndex))
                {
                    _selectedSubMeshIndex = subMeshIndex;
                    _uvsAreDirty = true;
                }
            });

            var uvDropDown = _root.Q<DropdownField>(name: "UVDropDown");
            uvDropDown.value = "UV" + _uvChannel.ToString();
            _root.Q<DropdownField>(name: "UVDropDown").RegisterValueChangedCallback((e) =>
            {
                _uvChannel = int.Parse(e.newValue.Replace("UV", ""));

                _uvsAreDirty = true;
            });


            _root.Q(name: "RecenterButton").RegisterCallback<ClickEvent>((e) =>
            {
                Recenter();
                _uvsAreDirty = true;
            });

            _root.Q(name: "FocusButton").RegisterCallback<ClickEvent>((e) =>
            {
                FocusSelection();
                _uvsAreDirty = true;
            });

            _root.Q(name: "UVGridColorToggle").RegisterCallback<ClickEvent>((e) =>
            {
                if (UVLineColor == Color.white)
                    UVLineColor = Color.black;
                else
                    UVLineColor = Color.white;

                _uvsAreDirty = true;
            });

            _triangleSelectionButton = _root.Q<Button>(name: "TrianglesSelectButton");
            _triangleSelectionButton.RegisterCallback<ClickEvent>((e) =>
             {
                 if (!_triangleSelectEnabled)
                 {
                     StartTriangleSelecting();
                 }
             });

            _trianglesSelectLinkedButton = _root.Q<Button>(name: "TrianglesSelectLinkedButton");
            _trianglesSelectLinkedButton.RegisterCallback<ClickEvent>((e) =>
            {
                if (!_triangleSelectEnabled)
                    return;

                trianglesSelectLinked(remove: e.shiftKey);
            });

            _trianglesUVFilterDropDown = _root.Q<DropdownField>(name: "TrianglesUVFilterDropDown");
            _trianglesUVFilterDropDown.value = "All";
            _root.Q<DropdownField>(name: "UVFilterDropDown").RegisterValueChangedCallback((e) =>
            {
                if (e.newValue == UVFilter.All.ToString())
                    _uvFilter = UVFilter.All;
                else if (e.newValue == UVFilter.Selected.ToString())
                    _uvFilter = UVFilter.Selected;
                else
                    _uvFilter = UVFilter.All;

                _uvsAreDirty = true;
            });
            // Initial state
            _trianglesSelectLinkedButton.SetEnabled(false);
            _trianglesUVFilterDropDown.SetEnabled(false);

            _verticesSelectionButton = _root.Q<Button>(name: "VerticesSelectButton");
            _verticesSelectionButton.RegisterCallback<ClickEvent>((e) =>
            {
                if (_triangleSelectEnabled)
                {
                    StopTriangleSelecting();
                }
            });
            _verticesSelectionButton.style.backgroundColor = new Color(0f, 1f, 1f, 0.2f);

            _verticesSelectLinkedButton = _root.Q<Button>(name: "VerticesSelectLinkedButton");
            _verticesSelectLinkedButton.RegisterCallback<ClickEvent>((e) =>
            {
                if (_triangleSelectEnabled)
                    return;

                verticesSelectLinked(remove: e.shiftKey);
            });

            _editTextureButton = _root.Q<Button>(name: "EditTextureButton");
            _editCropTextureButton = _root.Q<Button>(name: "EditCropTextureButton");

            _editTextureButton.RegisterCallback<ClickEvent>((e) =>
            {
                if (_editTextureEnabled)
                {
                    StopTextureEditing();
                }
                else
                {
                    StartTextureEditing();
                }

                _uvsAreDirty = true;
            });

            _editCropTextureButton.RegisterCallback<ClickEvent>((e) =>
            {
                if (_editCropEnabled)
                {
                    StopCropping();
                }
                else
                {
                    Recenter();
                    StartCropping();
                }
            });

            _root.Q(name: "ClearSelectionButton").RegisterCallback<ClickEvent>((e) =>
            {
                if (_editCropEnabled)
                    StopCropping();

                if (_editTextureEnabled)
                    StopTextureEditing();

                _selectedVertices.Clear();
                onSelectedVerticesChanged();

                ClearCacheAndSelection();
                UndoStack.Instance.Clear();
            });

            _applyButton = _root.Q<Button>(name: "ApplyButton");
            _applyButton.RegisterCallback<ClickEvent>((e) =>
            {
                if (_editCropEnabled)
                {
                    // Crop
                    applyCrop();
                    internalStopCroppingCycle();
                    internalStartCroppingCycle();
                }
                else if(_editTextureEnabled)
                {
                    // Edit Texture
                    UndoStack.Instance.StartGroup();
                    applyUVChangesToSelectedObjectMesh(recordUndo: true, _uvChannel);
                    applyChangesToTexture(recordUndo: true);
                    UndoStack.Instance.EndGroup();
                }
                else
                {
                    applyUVChangesToSelectedObjectMesh(recordUndo: true, _uvChannel);
                }
                _uvsAreDirty = true;
            });

            _cropWidthInput = _root.Q<TextField>("CropWidth");
            _cropWidthInput.RegisterValueChangedCallback(onCropWidthInputChanged);
            _cropHeightInput = _root.Q<TextField>("CropHeight");
            _cropHeightInput.RegisterValueChangedCallback(onCropHeightInputChanged);
            _cropBottomTools = _root.Query(className: "crop-bottom-toolbar").ToList();
            foreach (var ve in _cropBottomTools)
            {
                ve.style.display = DisplayStyle.None;
            }

            _liveUVsToggle = _root.Q<Toggle>(name: "LiveUVsToggle");
            _liveUVsToggle.value = _applyChangesImmediately;
            _liveUVsToggle.RegisterValueChangedCallback((evt) => _applyChangesImmediately = evt.newValue);

            _canvas = _root.Q(name: "Canvas");

            _textureContainer = new VisualElement();
            _textureContainer.name = "TextureContainer";
            _textureContainer.style.position = Position.Absolute;
            _canvas.Add(_textureContainer);

            _textureSliceContainer = new VisualElement();
            _textureSliceContainer.name = "TextureSliceContainer";
            _textureSliceContainer.style.position = Position.Absolute;
            _textureSliceContainer.style.display = DisplayStyle.None;
            _canvas.Add(_textureSliceContainer);

            _gridContainer = new VisualElement();
            _gridContainer.name = "GridContainer";
            _gridContainer.style.position = Position.Absolute;
            _gridContainer.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _canvas.Add(_gridContainer);

            _selectedTriContainer = new TriangleDrawer();
            _selectedTriContainer.name = "SelectedTriContainer";
            _selectedTriContainer.style.position = Position.Absolute;
            _canvas.Add(_selectedTriContainer);

            _highlightedTriContainer = new TriangleDrawer();
            _highlightedTriContainer.name = "HighlightedTriContainer";
            _highlightedTriContainer.style.position = Position.Absolute;
            _canvas.Add(_highlightedTriContainer);

            createNewUVLinesContainer();

            _rectUVGizmoContainer = new LineDrawer();
            _rectUVGizmoContainer.name = "RectUVGizmoContainer";
            _rectUVGizmoContainer.style.position = Position.Absolute;
            _rectUVGizmoContainer.style.display = !_editTextureEnabled && !_editCropEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            _canvas.Add(_rectUVGizmoContainer);

            createGizmoElement(_rectUVGizmoContainer, ref _blScaleGizmo, UnityDefaultCursor.CursorType.ResizeUpRight);
            _blScaleGizmo.AddToClassList("scale-corner-gizmo");
            createGizmoElement(_rectUVGizmoContainer, ref _tlScaleGizmo, UnityDefaultCursor.CursorType.ResizeUpLeft);
            _tlScaleGizmo.AddToClassList("scale-corner-gizmo");
            createGizmoElement(_rectUVGizmoContainer, ref _trScaleGizmo, UnityDefaultCursor.CursorType.ResizeUpRight);
            _trScaleGizmo.AddToClassList("scale-corner-gizmo");
            createGizmoElement(_rectUVGizmoContainer, ref _brScaleGizmo, UnityDefaultCursor.CursorType.ResizeUpLeft);
            _brScaleGizmo.AddToClassList("scale-corner-gizmo");
            createGizmoElement(_rectUVGizmoContainer, ref _blRotateGizmo, UnityDefaultCursor.CursorType.RotateArrow);
            createGizmoElement(_rectUVGizmoContainer, ref _tlRotateGizmo, UnityDefaultCursor.CursorType.RotateArrow);
            createGizmoElement(_rectUVGizmoContainer, ref _trRotateGizmo, UnityDefaultCursor.CursorType.RotateArrow);
            createGizmoElement(_rectUVGizmoContainer, ref _brRotateGizmo, UnityDefaultCursor.CursorType.RotateArrow);
            createGizmoElement(_rectUVGizmoContainer, ref _centerMoveGizmo, UnityDefaultCursor.CursorType.MoveArrow);

            _vertexContainer = new LineDrawer();
            _vertexContainer.pickingMode = PickingMode.Ignore;
            _vertexContainer.name = "VertexContainer";
            _vertexContainer.style.position = Position.Absolute;
            _vertexContainer.style.display = !_editTextureEnabled && !_editCropEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            _canvas.Add(_vertexContainer);

            _selectionRectContainer = new LineDrawer();
            _selectionRectContainer.pickingMode = PickingMode.Ignore;
            _selectionRectContainer.name = "SelectionRectContainer";
            _selectionRectContainer.style.position = Position.Absolute;
            _selectionRectContainer.style.display = !_editCropEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            _canvas.Add(_selectionRectContainer);

            _rectCropGizmoContainer = new LineDrawer();
            _rectCropGizmoContainer.name = "RectCropGizmoContainer";
            _rectCropGizmoContainer.style.position = Position.Absolute;
            _rectCropGizmoContainer.style.display = _editCropEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            _canvas.Add(_rectCropGizmoContainer);

            createGizmoElement(_rectCropGizmoContainer, ref _blCropGizmo, UnityDefaultCursor.CursorType.ResizeUpRight);
            _blCropGizmo.AddToClassList("crop-corner-gizmo");
            createGizmoElement(_rectCropGizmoContainer, ref _tlCropGizmo, UnityDefaultCursor.CursorType.ResizeUpLeft);
            _tlCropGizmo.AddToClassList("crop-corner-gizmo");
            createGizmoElement(_rectCropGizmoContainer, ref _trCropGizmo, UnityDefaultCursor.CursorType.ResizeUpRight);
            _trCropGizmo.AddToClassList("crop-corner-gizmo");
            createGizmoElement(_rectCropGizmoContainer, ref _brCropGizmo, UnityDefaultCursor.CursorType.ResizeUpLeft);
            _brCropGizmo.AddToClassList("crop-corner-gizmo");


            // Initially display the texture of the material of the last selected tri sub mesh.
            _checkerTexture = findAndLoadAsset<Texture2D>("Texture", "UVEditorCheckerTexture");
            _texture = null;
            _textureMaterial = null;

            // get last element and make this the cursor target.
            // We would be able to void this if style inheritance was a thing within Unity.
            //var canvasChildren = canvas.Children();
            //foreach (var child in canvasChildren)
            //{
            //    cursorTargetElement = child;
            //}
            _cursorTargetElement = _rectUVGizmoContainer;

            // Register for events
            _canvas.RegisterCallback<WheelEvent>(OnScroll);
            _canvas.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _canvas.RegisterCallback<MouseUpEvent>(OnMouseUp);
            _canvas.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _canvas.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            // Info: We can't use UI Toolkit key events because they suck (they only trigger on focused elements).
            //       Instead we piggyback on the mouse move event (see OnMouseMove).

            UpdateWindowContent();
            _root.schedule.Execute(UpdateWindowContent).Every(16);
        }

        // Container classes are used to show/hide certain UI elements, see USS file (show-if-...) for more infos.

        public void AddContainerClass(string className)
        {
            _root.Q("Container").AddToClassList(className);
        }

        public void RemoveContainerClass(string className)
        {
            _root.Q("Container").RemoveFromClassList(className);
        }

        public void SetSelectedObject(UnityEngine.Object obj)
        {
            var selectedObjectFieldBig = _root.Q<ObjectField>(name: "SelectedObjectBig");

            if (obj == null)
            {
                StopTriangleSelecting();
                StopCropping();
                StopTextureEditing();

                _selectedObject = null;
                selectedObjectField.SetValueWithoutNotify(_selectedObject);
                selectedObjectFieldBig.SetValueWithoutNotify(_selectedObject);
                _selectedMeshFilter = null;
                _selectedSkinnedRenderer = null;
                _selectedMesh = null;
                RemoveContainerClass("object-selected");
                return;
            }

            // Validate new selected object
            var mesh = obj as Mesh;
            var go = obj as GameObject;
            var comp = obj as Component;
            if (mesh == null && go == null && comp == null)
            {
                Logger.LogError("Sorry, the selected object is no Mesh or GameObject or a Component.");
            }

            if (mesh != null)
            {
                _selectedObject = obj;
                _selectedMeshFilter = null;
                _selectedSkinnedRenderer = null;
                _selectedMesh = mesh;
            }
            else if (comp != null)
            {
                var meshFilter = comp as MeshFilter;
                var meshRenderer = comp as SkinnedMeshRenderer;

                // Because users often drop in the renderer instead of the filter we fetch the filter from the renderer if possible.
                var meshFilterRenderer = comp as MeshRenderer;
                if (meshFilter == null && meshFilterRenderer != null)
                {
                    meshFilter = meshFilterRenderer.gameObject.GetComponent<MeshFilter>();
                }

                if (meshFilter != null)
                {
                    if (meshFilter.sharedMesh == null)
                    {
                        EditorUtility.DisplayDialog("Invalid Object", "Sorry, the selected MeshFilter does not have a mesh assigned or the mesh asset is missing.", "OK");
                        _selectedMeshFilter = null;
                        _selectedSkinnedRenderer = null;
                        _selectedMesh = null;
                        _selectedObject = null;
                        selectedObjectField.SetValueWithoutNotify(_selectedObject);
                        selectedObjectFieldBig.SetValueWithoutNotify(_selectedObject); 
                    }
                    else
                    {
                        _selectedObject = obj;
                        _selectedMeshFilter = meshFilter;
                        _selectedSkinnedRenderer = null;
                        _selectedMesh = meshFilter.sharedMesh;
                    }
                }
                else if (meshRenderer != null)
                {
                    if (meshRenderer.sharedMesh == null)
                    {
                        EditorUtility.DisplayDialog("Invalid Object", "Sorry, the selected SkinnedMeshRenderer does not have a mesh assigned or the mesh asset is missing.", "OK");
                        _selectedMeshFilter = null;
                        _selectedSkinnedRenderer = null;
                        _selectedMesh = null;
                        _selectedObject = null;
                        selectedObjectField.SetValueWithoutNotify(_selectedObject);
                        selectedObjectFieldBig.SetValueWithoutNotify(_selectedObject);
                    }
                    else
                    {
                        _selectedObject = obj;
                        _selectedMeshFilter = null;
                        _selectedSkinnedRenderer = meshRenderer;
                        _selectedMesh = meshRenderer.sharedMesh;
                    }
                }
            }
            else if (go != null)
            {
                var meshFilter = go.GetComponentInChildren<MeshFilter>(includeInactive: true);
                var meshRenderer = go.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);

                if (meshFilter != null && meshRenderer != null)
                {
                    EditorUtility.DisplayDialog("Ambiguous Selection", "There is more than one mesh in the selected object. Please select an object with only one mesh or select the mesh component or mesh asset directly.", "OK");
                }
                else
                {
                    if (meshFilter != null)
                    {
                        if (meshFilter.sharedMesh == null)
                        {
                            EditorUtility.DisplayDialog("Invalid Object", "Sorry, the selected MeshFilter does not have a mesh assigned or the mesh asset is missing.", "OK");
                            _selectedMeshFilter = null;
                            _selectedSkinnedRenderer = null;
                            _selectedMesh = null;
                            _selectedObject = null;
                            selectedObjectField.SetValueWithoutNotify(_selectedObject);
                            selectedObjectFieldBig.SetValueWithoutNotify(_selectedObject);
                            return;
                        }
                        else
                        {
                            _selectedObject = obj;
                            _selectedMeshFilter = meshFilter;
                            _selectedSkinnedRenderer = null;
                            _selectedMesh = meshFilter.sharedMesh;
                        }
                    }
                    else if (meshRenderer != null)
                    {
                        if (meshRenderer.sharedMesh == null)
                        {
                            EditorUtility.DisplayDialog("Invalid Object", "Sorry, the selected SkinnedMeshRenderer does not have a mesh assigned or the mesh asset is missing.", "OK");
                            _selectedMeshFilter = null;
                            _selectedSkinnedRenderer = null;
                            _selectedMesh = null;
                            _selectedObject = null;
                            selectedObjectField.SetValueWithoutNotify(_selectedObject);
                            selectedObjectFieldBig.SetValueWithoutNotify(_selectedObject);
                            return;
                        }
                        else
                        {
                            _selectedObject = obj;
                            _selectedMeshFilter = null;
                            _selectedSkinnedRenderer = meshRenderer;
                            _selectedMesh = meshRenderer.sharedMesh;
                        }
                    }
                }
            }

            if (_selectedObject == null)
                RemoveContainerClass("object-selected");
            else
                AddContainerClass("object-selected");

            selectedObjectField.SetValueWithoutNotify(_selectedObject);
            selectedObjectFieldBig.SetValueWithoutNotify(_selectedObject);

            // Sub Mesh Index
            var subMeshIndexDropDown = _root.Q<DropdownField>(name: "SelectedSubMeshIndex");
            if (_selectedMesh == null)
            {
                subMeshIndexDropDown.choices = new List<string>() { "0" };
                _selectedSubMeshIndex = 0;
                subMeshIndexDropDown.SetValueWithoutNotify(_selectedSubMeshIndex.ToString());
            }
            else
            {
                var choices = new List<string>();
                for (int i = 0; i < _selectedMesh.subMeshCount; i++)
                {
                    choices.Add(i.ToString());
                }
                subMeshIndexDropDown.choices = choices;
                _selectedSubMeshIndex = 0;
                subMeshIndexDropDown.SetValueWithoutNotify(_selectedSubMeshIndex.ToString());
            }

            // Limit undo history for big models.
            if (_selectedMesh != null && _selectedMesh.vertexCount > 10_000)
                UndoStack.Instance.MaxEntries = 4;
            else
                UndoStack.Instance.MaxEntries = 40;

            clearMeshCache();
        }

        private LineDrawer createNewUVLinesContainer(VisualElement sibling = null)
        {
            var uvLinesContainer = new LineDrawer();
            uvLinesContainer.name = "UVLinesContainer";
            uvLinesContainer.style.position = Position.Absolute;
            uvLinesContainer.InvertVertical = true;
            uvLinesContainer.LineColor = UVLineColor;
            uvLinesContainer.LineWidth = 1f;
            _uvLinesContainers.Add(uvLinesContainer);

            _currentUVLinesContainer = uvLinesContainer;

            _canvas.Add(uvLinesContainer);
            if (sibling != null)
            {
                uvLinesContainer.PlaceInFront(sibling);
            }

            return uvLinesContainer;
        }

        public void Recenter()
        {
            _focusedOnSelection = false;

            _pan = Vector2.zero;
            _zoom = 1f;
            UpdateWindowContent();
        }

        public void ToggleFocusSelection()
        {
            // If the selection changed since the last focus then we force focusing on the selection.
            if (_focusedOnSelection && !_selectionChangedSinceLastFocus)
            {
                Recenter();
            }
            else
            {
                FocusSelection();
                _selectionChangedSinceLastFocus = false;
            }
        }

        public void FocusSelection()
        {
            _focusedOnSelection = true;

            // Skip if no uvs have been selected.
            if (_selectedVertices.IsNullOrEmpty())
                return;

            _zoom = Mathf.Min(1 / (_selRectInUVs.min - _selRectInUVs.max).magnitude * 0.9f, 100f);

            calcCanvasSize(out float canvasWidth, out float canvasHeight, out float size, out float margin);

            var offsetInUV = _selRectInUVs.center - new Vector2(0.5f, 0.5f);
            offsetInUV.x = -offsetInUV.x * size;
            offsetInUV.y = offsetInUV.y * size;
            _pan = offsetInUV;

            UpdateWindowContent();
        }

        public void StartTriangleSelecting()
        {
            if (_triangleSelectEnabled)
                return;

            if (SceneView.lastActiveSceneView == null)
            {
                EditorUtility.DisplayDialog(
                    "No Scene View",
                    "Triangle selection works in the scene view but the tool has not found any scene views. Please open one and focus it.",
                    "OK");
                return;
            }

            if (SelectedGameObject == null)
            {
                EditorUtility.DisplayDialog(
                    "No Object Selected",
                    "Please select an object for UV Editing before starting the triangle selection mode.",
                    "OK");
                return;
            }

            if (!SceneView.lastActiveSceneView.hasFocus)
            {
                foreach (var sceneView in SceneView.sceneViews)
                {
                    if (sceneView is SceneView)
                    {
                        SceneView scene = (SceneView)sceneView;
                        scene.Focus();
                        scene.Repaint();
                        break;
                    }
                }

                SceneView.lastActiveSceneView?.FrameSelected();
            }

            // Force selection of the object selected in the UV Editor
            Selection.objects = new GameObject[] { SelectedGameObject };

            _triangleSelectEnabled = true;
            
            // ui
            _triangleSelectionButton.style.backgroundColor = new Color(0f, 1f, 1f, 0.2f);
            _verticesSelectionButton.style.backgroundColor = StyleKeyword.Null;
            _trianglesSelectLinkedButton.SetEnabled(true);
            _trianglesUVFilterDropDown.SetEnabled(true);
            _verticesSelectLinkedButton.SetEnabled(false);

            UndoStack.Instance.Clear();
            _uvsAreDirty = true;

            if (_editCropEnabled)
                StopCropping();

            if (_editTextureEnabled)
                StopTextureEditing();

            UVTriangleSelectTool.StartTool();
        }

        public void OnToolChanged()
        {
            if (_triangleSelectEnabled && ToolManager.activeToolType != typeof(UVTriangleSelectTool))
                StopTriangleSelecting();
        }

        public void StopTriangleSelecting()
        {
            if (!_triangleSelectEnabled) 
                return;

            _triangleSelectEnabled = false;
            
            // ui
            _triangleSelectionButton.style.backgroundColor = StyleKeyword.Null;
            _verticesSelectionButton.style.backgroundColor = new Color(0f, 1f, 1f, 0.2f);
            _trianglesSelectLinkedButton.SetEnabled(false);
            _trianglesUVFilterDropDown.SetEnabled(false);
            _verticesSelectLinkedButton.SetEnabled(true);

            UndoStack.Instance.Clear();
            _uvsAreDirty = true;

            UVTriangleSelectTool.ExitTool(notifyUVEditorWindow: false);
        }

        public void OnTriangleSelectToolExit()
        {
            if (_triangleSelectEnabled)
                StopTriangleSelecting();
        }

        public void StartTextureEditing()
        {
            if (_selectedVertices.Count == 0)
                return;

            UndoStack.Instance.Clear();

            // Check if a texture is available.
            if (_texture != null)
            {
                if (_editCropEnabled)
                    StopCropping();

                if (_triangleSelectEnabled)
                    StopTriangleSelecting();

                // Check if only one sub mesh is selected.
                if (isTextureEditingPossible())
                {
                    _editTextureEnabled = true;
                    _editTextureButton.style.backgroundColor = new Color(0f, 1f, 1f, 0.2f);

                    // Make sure apply immediately is OFF for texture editing.
                    _applyChangesImmediately = false;
                    _liveUVsToggle.value = _applyChangesImmediately;

                    internalStartTextureEditing();
                }
            }
            else
            {
                EditorUtility.DisplayDialog("No Texture", "Can not switch to texture editing mode because there is no texture to be edited. Please select a triangle with a valid texture 2D first.", "OK");
            }
        }

        private bool isTextureEditingPossible()
        {
            // Check if the texture is not null AND is not the default checker texture
            if (_texture == null)
            {
                EditorUtility.DisplayDialog(
                    "No Texture!",
                    "The currently selected submesh (" + _selectedSubMeshIndex + ") does not have a texture or material assigned. We can not edit the texture if there is none." +
                    "\n\nHINT: The texture you see is the default 'checker' texture for reference only.",
                    "OK");
                return false;
            }

            return true;
        }

        protected List<int> _tmpSubMeshTris = new List<int>();

        private int getSubMeshForVertex(int v)
        {
            // TODO: Cache sub mesh infos for vetices.
            int subMeshCount = _selectedMesh.subMeshCount;
            for (int s = 0; s < subMeshCount; s++)
            {
                _selectedMesh.GetTriangles(_tmpSubMeshTris, s);
                if (_tmpSubMeshTris.Contains(v))
                    return s;
            }

            return -1;
        }

        public void StopTextureEditing()
        {
            _editTextureButton.style.backgroundColor = StyleKeyword.Null;
            
            if (!_editTextureEnabled)
                return;

            _editTextureEnabled = false;

            internalStopTextureEditing();

            UndoStack.Instance.Clear();
        }

        public void StartCropping()
        {
            if (_selectedVertices.Count == 0)
                return;

            if (_editCropEnabled)
                return;

            UndoStack.Instance.Clear();

            if (_editTextureEnabled)
                StopTextureEditing();

            if (_triangleSelectEnabled)
                StopTriangleSelecting();

            // Check if a texture is available.
            if (_texture != null)
            {
                // Check if only one sub mesh is selected.
                if (isTextureEditingPossible())
                {
                    _editCropEnabled = true;
                    _editCropTextureButton.style.backgroundColor = new Color(0f, 1f, 1f, 0.2f);
                    foreach (var ve in _cropBottomTools)
                    {
                        ve.style.display = DisplayStyle.Flex;
                    }

                    internalStartCroppingCycle();
                }
            }
            else
            {
                EditorUtility.DisplayDialog("No Texture", "Can not switch to texture cropping mode because there is no texture to be edited. Please select a triangle with a valid texture 2D first.", "OK");
            }
        }

        public void StopCropping()
        {
            if (!_editCropEnabled)
                return;

            _editCropEnabled = false;
            _editCropTextureButton.style.backgroundColor = StyleKeyword.Null;
            foreach (var ve in _cropBottomTools)
            {
                ve.style.display = DisplayStyle.None;
            }

            if (_editTextureEnabled)
                StopTextureEditing();

            internalStopCroppingCycle();

            UndoStack.Instance.Clear();
        }

        private void trianglesSelectLinked(bool remove)
        {
            var tool = UVTriangleSelectTool.Instance;
            if (tool != null)
            {
                tool.SelectLinked(remove, compareConnectedUVs: false);
            }
        }

        private void verticesSelectLinked(bool remove)
        {
            if (_selectedVertices.Count > 0)
            {
                UndoStack.Instance.StartEntry();
                UndoStack.Instance.AddUndoAction(undoSetSelectedVerticesFunc());

                var settings = UVEditorSettings.GetOrCreateSettings();

                AddLinked(
                    _selectedVertices,
                    GetSharedMeshFromGameObject(SelectedGameObject),
                    limitToSubMesh: false,
                    _selectedSubMeshIndex,
                    maxDuration: settings.MaxSelectLinkedDuration,
                    showFailedPopup: true,
                    compareConnectedUVs: true,
                    _uvChannel);


                _uvsAreDirty = true;
                removeUVsWorkingCopy(SelectedGameObject, _uvChannel);
                onSelectedVerticesChanged();

                UndoStack.Instance.AddRedoAction(undoSetSelectedVerticesFunc());
                UndoStack.Instance.EndEntry();
            }
            else
            {
                EditorUtility.DisplayDialog("No Selection to Start", "Please select at least one vertex to start a linked selection.", "Ok");
            }
        }

        private void createGizmoElement(VisualElement container, ref VisualElement gizmo, UnityDefaultCursor.CursorType cursorType)
        {
            gizmo = new VisualElement();
            gizmo.style.display = DisplayStyle.None;
            gizmo.style.position = Position.Absolute;
            gizmo.style.cursor = UnityDefaultCursor.DefaultCursor(cursorType);

            container.Add(gizmo);
        }

        private void updateGizmo(ref VisualElement gizmo, bool show, Vector2 uvPosition, float widthInPx, float heightInPx)
        {
            gizmo.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            // Notice: we position them with % because:
            // a) That matches the UVs nicely
            // b) If the texture container is resized then % values will update automatically,
            //    px values would need to be updated (= flickering).
            gizmo.style.left = new Length(uvPosition.x * 100f, LengthUnit.Percent);
            gizmo.style.bottom = new Length(uvPosition.y * 100f, LengthUnit.Percent);
            gizmo.style.width = widthInPx;
            gizmo.style.height = heightInPx;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateWindowContent();
        }

        private void checkMeshMaterialOrTextureChange()
        {

        }

        private void UpdateWindowContent()
        {
            // If the object went missing then update selection to null (happens due to scene change, object deletion, ...)
            if (_selectedObject == null && !ReferenceEquals(_selectedObject, null))
            {
                SetSelectedObject(null);
                return;
            }

            // If the mesh reference became invalid then abort and set selection to null
            if (_selectedMesh == null || _selectedMesh != GetSharedMeshFromGameObject(SelectedGameObject))
            {
                SetSelectedObject(_selectedObject);
            }

            if (_previousZoom != _zoom)
            {
                _previousZoom = _zoom;
                _uvsAreDirty = true;
            }

            var renderer = getSelectedRenderer();
            var settings = UVEditorSettings.GetOrCreateSettings();
            var uvs = getUVWorkingCopyForSelection(_uvChannel);
            bool hasUVs = uvs != null && uvs.Count > 0;

            bool showApplyButton = !_selectedVertices.IsNullOrEmpty() || _editCropEnabled;
            bool showLivePreview = !_selectedVertices.IsNullOrEmpty() && !_editCropEnabled && !_editTextureEnabled;
            _applyButton.style.display = showApplyButton ? DisplayStyle.Flex : DisplayStyle.None;
            _liveUVsToggle.style.display = showLivePreview ? DisplayStyle.Flex : DisplayStyle.None;

            // bool zoomChanged = !Mathf.Approximately(_zoom, _previousZoom);
            // _previousZoom = _zoom;
            // if (zoomChanged)
            // {
            //     UVsAreDirty = true;
            // }

            bool textureFound = false;
            Material[] rendererMaterials;
            if (renderer != null)
            {
                rendererMaterials = renderer.sharedMaterials;
                if (rendererMaterials.Length > _selectedSubMeshIndex && rendererMaterials[_selectedSubMeshIndex] != null)
                {
                    var tex = rendererMaterials[_selectedSubMeshIndex].mainTexture as Texture2D;
                    if (tex != null)
                    {
                        textureFound = true;
                        _texture = tex;
                        _textureMaterial = rendererMaterials[_selectedSubMeshIndex];
                    }
                }
            }
            if (!textureFound)
            {
                _texture = null;
                _textureMaterial = null;
            }

            // Get the size of the parent container (the ScrollView's content container)
            float canvasWidth, canvasHeight, size;
            calcCanvasSize(out canvasWidth, out canvasHeight, out size, out _);

            // n2h: Check if redraw is necessary (zoom or pan changed?)
            //      though, we also have to redraw with any selection or hover change, hm ...

            // Update the texture container's size and position
            _textureContainer.style.width = size;
            _textureContainer.style.height = size;

            // Center the texture in the parent container
            _textureContainer.style.left = (canvasWidth - size) / 2f + _pan.x;
            _textureContainer.style.bottom = (canvasHeight - size) / 2f - _pan.y;

            // Set the texture background
            var offset = _textureMaterial != null ? _textureMaterial.mainTextureOffset : Vector2.zero;
            var scale = _textureMaterial != null ? _textureMaterial.mainTextureScale : Vector2.one;
            fillTextureContainerWithTiledTexture(_texture, _textureMaterial, offset, scale);

            // Match grid to texture
            if (_gridTexture != null)
                _gridContainer.style.backgroundImage = _gridTexture;
            float gridMarginWidth = _textureContainer.style.width.value.value * 0.12f;
            float gridMarginHeight = _textureContainer.style.height.value.value * 0.12f;
            _gridContainer.style.left = _textureContainer.style.left.value.value - gridMarginWidth;
            _gridContainer.style.bottom = _textureContainer.style.bottom.value.value - gridMarginHeight;
            _gridContainer.style.width = _textureContainer.style.width.value.value * 1.2f;
            _gridContainer.style.height = _textureContainer.style.height.value.value * 1.2f;

            // draw selected tris
            var tool = UVTriangleSelectTool.Instance;
            if (_triangleSelectEnabled && tool != null && tool.SelectedTriangles.Count > 0)
            {
                _selectedTriContainer.style.display = DisplayStyle.Flex;
                _selectedTriContainer.style.width = _textureContainer.style.width;
                _selectedTriContainer.style.height = _textureContainer.style.height;
                _selectedTriContainer.style.left = _textureContainer.style.left;
                _selectedTriContainer.style.bottom = _textureContainer.style.bottom;
                _selectedTriContainer.InvertVertical = true;
                _selectedTriContainer.ClearVertices();
                _selectedTriContainer.Tint = new Color(0f, 1f, 0f, settings.SelectionColorAlpha);
                if (hasUVs)
                {
                    foreach (var tri in tool.SelectedTriangles)
                    {
                        var color = _selectedTriContainer.Tint;
                        if (tri.SubMeshIndex < settings.SelectionColorSubMeshes.Length)
                        {
                            color = settings.SelectionColorSubMeshes[tri.SubMeshIndex];
                            color.a = settings.SelectionColorAlpha;
                        }
            
                        var a = uvs[tri.TriangleIndices[0]];
                        var b = uvs[tri.TriangleIndices[1]];
                        var c = uvs[tri.TriangleIndices[2]];
                        if (isClockwise(a, b, c))
                        {
                            _selectedTriContainer.AddVertex(a * size, color);
                            _selectedTriContainer.AddVertex(b * size, color);
                            _selectedTriContainer.AddVertex(c * size, color);
                        }
                        else
                        {
                            _selectedTriContainer.AddVertex(c * size, color);
                            _selectedTriContainer.AddVertex(b * size, color);
                            _selectedTriContainer.AddVertex(a * size, color);
                        }
                    }
                }
                _selectedTriContainer.MarkDirtyRepaint();
            }
            else
            {
                _selectedTriContainer.style.display = DisplayStyle.None;
            }

            // draw hovered tri
            if (_triangleSelectEnabled && tool.HoverTriangles != null && tool.HoverTriangles.Count > 0)
            {
                _highlightedTriContainer.style.display = DisplayStyle.Flex;
                _highlightedTriContainer.style.width = _textureContainer.style.width;
                _highlightedTriContainer.style.height = _textureContainer.style.height;
                _highlightedTriContainer.style.left = _textureContainer.style.left;
                _highlightedTriContainer.style.bottom = _textureContainer.style.bottom;
                _highlightedTriContainer.InvertVertical = true;
                _highlightedTriContainer.ClearVertices();

                if (tool.RemoveFromSelection || _ctrlIsPressed)
                    _highlightedTriContainer.Tint = new Color(1f, 0f, 0f, 0.7f);
                else
                    _highlightedTriContainer.Tint = new Color(1f, 1f, 0f, 0.7f);

                foreach (var hoverTri in tool.HoverTriangles)
                {
                    var a = uvs[hoverTri.TriangleIndices[0]];
                    var b = uvs[hoverTri.TriangleIndices[1]];
                    var c = uvs[hoverTri.TriangleIndices[2]];

                    _highlightedTriContainer.AddVertex(a * size);
                    _highlightedTriContainer.AddVertex(b * size);
                    _highlightedTriContainer.AddVertex(c * size);
                }
                _highlightedTriContainer.MarkDirtyRepaint();
            }
            else
            {
                _highlightedTriContainer.style.display = DisplayStyle.None;
            }

            _textureSliceContainer.style.display = (_editTextureEnabled && _texture != null) ? DisplayStyle.Flex : DisplayStyle.None;
            _rectUVGizmoContainer.style.display = !_selectedVertices.IsNullOrEmpty() ? DisplayStyle.Flex : DisplayStyle.None;
            _rectCropGizmoContainer.style.display = _editCropEnabled ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasUVs)
            {
                // Update UV container positions
                foreach (var uvLinesContainer in _uvLinesContainers)
                {
                    uvLinesContainer.style.display = DisplayStyle.Flex;
                    uvLinesContainer.style.width = _textureContainer.style.width;
                    uvLinesContainer.style.height = _textureContainer.style.height;
                    uvLinesContainer.style.left = _textureContainer.style.left;
                    uvLinesContainer.style.bottom = _textureContainer.style.bottom;
                    uvLinesContainer.InvertVertical = true;
                    uvLinesContainer.LineColor = UVLineColor;
                    uvLinesContainer.LineWidth = 1f;
                }
            }
            
            // If the selection rect is very small (most likely if only once vertex position was selected) then
            // only show the move gizmo and increase the rect size a bit outwards (so there actually is a move gizmo area).
            bool showScaleAndRotateGizmos = _selRectInUVs.width * size > 2 && _selRectInUVs.height * size > 2;

            if (hasUVs)
            {
                if (_uvsAreDirty)
                {
                    _uvsAreDirty = false;
                    // draw UV lines
                    foreach (var uvLinesContainer in _uvLinesContainers)
                    {
                        uvLinesContainer.ClearVertices();
                    }

                    // Start caching things for performance. The profiler told me to do this ;)
                    var tmpSharedMaterials = renderer.sharedMaterials;
                    var tmpSmMainTextures = new List<Texture>(tmpSharedMaterials.Length);
                    var tmpTextureMaterialMainTexture = _textureMaterial != null ? _textureMaterial.mainTexture : null;
                    for (int smIndex = 0; smIndex < tmpSharedMaterials.Length; smIndex++)
                    {
                        if (tmpSharedMaterials[smIndex] != null)
                            tmpSmMainTextures.Add(tmpSharedMaterials[smIndex].mainTexture);
                        else
                            tmpSmMainTextures.Add(null);
                    }
                    // End cache

                    // Check if front UV filtering is feasible performance wise
                    // if (_selectedMesh.vertexCount > 10000)
                    // {
                    //     if (_uvFilter == UVFilter.Front)
                    //     {
                    //         _uvFilter = UVFilter.Selected;
                    //         EditorUtility.DisplayDialog("'Front' UV Filter not possible", "Sorry, but front filtering UVs is a performance heavy task, thus it is limited to meshes with 10.000 vertices or less. My apologies.", "Understood (that sucks)");
                    //     }
                    // }

                    int uvLinesContainerIndex = 0;
                    _currentUVLinesContainer = _uvLinesContainers[uvLinesContainerIndex];

                    List<int> subMeshTris = new List<int>();
                    for (int subMeshIndex = 0; subMeshIndex < _selectedMesh.subMeshCount; subMeshIndex++)
                    {
                        _selectedMesh.GetTriangles(subMeshTris, subMeshIndex);

                        int numOfVertices = subMeshTris.Count;
                        for (int v = 0; v < numOfVertices; v += 3)
                        {
                            bool sameMaterial = tmpSharedMaterials[subMeshIndex] == _textureMaterial;
                            bool sameTexture = false;
                            if (tmpSharedMaterials[subMeshIndex] != null && _textureMaterial != null)
                            {
                                sameTexture = tmpSmMainTextures[subMeshIndex] == tmpTextureMaterialMainTexture;
                            }
                            else
                            {
                                sameTexture = sameMaterial; // fallback
                            }

                            var vIndex0 = subMeshTris[v];
                            var vIndex1 = subMeshTris[v + 1];
                            var vIndex2 = subMeshTris[v + 2];

                            // Filter based on sel
                            if (_triangleSelectEnabled && filterUV(vIndex0, vIndex1, vIndex2))
                            {
                                continue;
                            }

                            var uvA = uvs[vIndex0];
                            var uvB = uvs[vIndex1];
                            var uvC = uvs[vIndex2];

                            // Skip triangles of zero area
                            if (uvA == uvB || uvB == uvC || uvA == uvC)
                                continue;

                            // Count drawn vertice and limit at 65k since that is the maximum UI Toolkit can draw.
                            // Once we have reached the limit we create a new linedrawer and continue drawing in there.
                            if (!_currentUVLinesContainer.NextLinesCanBeDrawn(3))
                            {
                                uvLinesContainerIndex++;
                                if (uvLinesContainerIndex >= _uvLinesContainers.Count)
                                {
                                    var newContainer = createNewUVLinesContainer(sibling: _currentUVLinesContainer);
                                    newContainer.style.display = DisplayStyle.Flex;
                                    newContainer.style.width = _textureContainer.style.width;
                                    newContainer.style.height = _textureContainer.style.height;
                                    newContainer.style.left = _textureContainer.style.left;
                                    newContainer.style.bottom = _textureContainer.style.bottom;
                                    newContainer.InvertVertical = true;
                                    newContainer.LineColor = UVLineColor;
                                    newContainer.LineWidth = 1f;
                                    newContainer.ClearVertices();
                                }
                                _currentUVLinesContainer = _uvLinesContainers[uvLinesContainerIndex];
                            }

                            var ignoredUVsColor = _currentUVLinesContainer.LineColor;
                            ignoredUVsColor.a = _editTextureEnabled ? 0.1f : 0.3f;
                            _currentUVLinesContainer.StartNewLine();
                            _currentUVLinesContainer.AddVertex(uvA * size, !sameTexture ? ignoredUVsColor : _currentUVLinesContainer.LineColor);
                            _currentUVLinesContainer.AddVertex(uvB * size, !sameTexture ? ignoredUVsColor : _currentUVLinesContainer.LineColor);
                            _currentUVLinesContainer.AddVertex(uvC * size, !sameTexture ? ignoredUVsColor : _currentUVLinesContainer.LineColor, closeLoop: true);
                        }
                    }

                    foreach (var uvLinesContainer in _uvLinesContainers)
                    {
                        uvLinesContainer.MarkDirtyRepaint();
                    }
                }

                // draw UV rect gizmo if we have vertices selected.
                if (!_selectedVertices.IsNullOrEmpty() && !_editCropEnabled)
                {
                    // Interactive Edit Texture quad gizmo
                    {
                        _rectUVGizmoContainer.style.width = _textureContainer.style.width;
                        _rectUVGizmoContainer.style.height = _textureContainer.style.height;
                        _rectUVGizmoContainer.style.left = _textureContainer.style.left;
                        _rectUVGizmoContainer.style.bottom = _textureContainer.style.bottom;
                        _rectUVGizmoContainer.InvertVertical = true;
                        _rectUVGizmoContainer.LineWidth = 2f;
                        _rectUVGizmoContainer.LineColor = _editTextureEnabled ? TextureGimzoColor : UVGimzoColor;
                        _rectUVGizmoContainer.ClearVertices();
                        // quad
                        _rectUVGizmoContainer.StartNewLine();
                        _rectUVGizmoContainer.AddVertex(new Vector2(_selRectInUVs.xMin, _selRectInUVs.yMin) * size);
                        _rectUVGizmoContainer.AddVertex(new Vector2(_selRectInUVs.xMin, _selRectInUVs.yMax) * size);
                        _rectUVGizmoContainer.AddVertex(new Vector2(_selRectInUVs.xMax, _selRectInUVs.yMax) * size);
                        _rectUVGizmoContainer.AddVertex(new Vector2(_selRectInUVs.xMax, _selRectInUVs.yMin) * size, closeLoop: true);

                        _rectUVGizmoContainer.MarkDirtyRepaint();

                        // Texture Gizmos (interactive areas)
                        var lineWidth = _rectUVGizmoContainer.LineWidth;
                        var scaleGizmoSize = lineWidth * 4f;
                        var scaleGizmoSizeHalf = lineWidth * 2f;
                        var cornerGizmoOutwardMargin = !showScaleAndRotateGizmos ? 10f : 4f;

                        // Scale Corner BottomLeft
                        _gizmoScaleRectBL = new Rect();
                        _gizmoScaleRectBL.min = new Vector2(_selRectInUVs.xMin - scaleGizmoSizeHalf / size, _selRectInUVs.yMin - scaleGizmoSizeHalf / size);
                        _gizmoScaleRectBL.max = new Vector2(_selRectInUVs.xMin + scaleGizmoSizeHalf / size, _selRectInUVs.yMin + scaleGizmoSizeHalf / size);
                        _gizmoScaleRectBL.position = _gizmoScaleRectBL.position + new Vector2(-cornerGizmoOutwardMargin / size, -cornerGizmoOutwardMargin / size);
                        updateGizmo(ref _blScaleGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectBL.min, scaleGizmoSize, scaleGizmoSize);

                        // Scale Corner TopLeft
                        _gizmoScaleRectTL = new Rect();
                        _gizmoScaleRectTL.min = new Vector2(_selRectInUVs.xMin - scaleGizmoSizeHalf / size, _selRectInUVs.yMax - scaleGizmoSizeHalf / size);
                        _gizmoScaleRectTL.max = new Vector2(_selRectInUVs.xMin + scaleGizmoSizeHalf / size, _selRectInUVs.yMax + scaleGizmoSizeHalf / size);
                        _gizmoScaleRectTL.position = _gizmoScaleRectTL.position + new Vector2(-cornerGizmoOutwardMargin / size, cornerGizmoOutwardMargin / size);
                        updateGizmo(ref _tlScaleGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectTL.min, scaleGizmoSize, scaleGizmoSize);

                        // Scale Corner TopRight
                        _gizmoScaleRectTR = new Rect();
                        _gizmoScaleRectTR.min = new Vector2(_selRectInUVs.xMax - scaleGizmoSizeHalf / size, _selRectInUVs.yMax - scaleGizmoSizeHalf / size);
                        _gizmoScaleRectTR.max = new Vector2(_selRectInUVs.xMax + scaleGizmoSizeHalf / size, _selRectInUVs.yMax + scaleGizmoSizeHalf / size);
                        _gizmoScaleRectTR.position = _gizmoScaleRectTR.position + new Vector2(cornerGizmoOutwardMargin / size, cornerGizmoOutwardMargin / size);
                        updateGizmo(ref _trScaleGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectTR.min, scaleGizmoSize, scaleGizmoSize);

                        // Scale Corner BottomRight
                        _gizmoScaleRectBR = new Rect();
                        _gizmoScaleRectBR.min = new Vector2(_selRectInUVs.xMax - scaleGizmoSizeHalf / size, _selRectInUVs.yMin - scaleGizmoSizeHalf / size);
                        _gizmoScaleRectBR.max = new Vector2(_selRectInUVs.xMax + scaleGizmoSizeHalf / size, _selRectInUVs.yMin + scaleGizmoSizeHalf / size);
                        _gizmoScaleRectBR.position = _gizmoScaleRectBR.position + new Vector2(cornerGizmoOutwardMargin / size, -cornerGizmoOutwardMargin / size);
                        updateGizmo(ref _brScaleGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectBR.min, scaleGizmoSize, scaleGizmoSize);

                        var rSize = new Vector2(lineWidth * 20f, lineWidth * 20f);
                        var pw = _textureContainer.resolvedStyle.width;
                        var ph = _textureContainer.resolvedStyle.height;
                        // Rotate Corner BottomLeft
                        updateGizmo(ref _blRotateGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectBL.min + new Vector2(-rSize.x / pw, -rSize.y / ph), rSize.x, rSize.y);
                        // Rotate Corner TopLeft
                        updateGizmo(ref _tlRotateGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectTL.min + new Vector2(-rSize.x / pw, scaleGizmoSize / ph), rSize.x, rSize.y);
                        // Rotate Corner TopRight
                        updateGizmo(ref _trRotateGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectTR.min + new Vector2(scaleGizmoSize / pw, scaleGizmoSize / ph), rSize.x, rSize.y);
                        // Rotate Corner BottomRight
                        updateGizmo(ref _brRotateGizmo, show: showScaleAndRotateGizmos && !_editTextureEnabled, _gizmoScaleRectBR.min + new Vector2(scaleGizmoSize / pw, -rSize.y / ph), rSize.x, rSize.y);

                        // Move Gizmo
                        float moveGizmoAreaMargin = 5f;
                        var mgWidth = (_gizmoScaleRectTR.min.x - _gizmoScaleRectBL.max.x) * pw - moveGizmoAreaMargin;
                        var mgHeight = (_gizmoScaleRectTR.min.y - _gizmoScaleRectBL.max.y) * ph - moveGizmoAreaMargin;
                        updateGizmo(ref _centerMoveGizmo, show: true,
                            uvPosition: _gizmoScaleRectBL.max + new Vector2(moveGizmoAreaMargin * 0.5f / pw, moveGizmoAreaMargin * 0.5f / ph),
                            widthInPx: mgWidth,
                            heightInPx: mgHeight);

                        // texture slice for texture editing
                        if (_editTextureEnabled && _texture != null)
                        {
                            float marginInLayoutPxX = _textureEditMargin / (float)_texture.width * size;
                            float marginInLayoutPxY = _textureEditMargin / (float)_texture.height * size;
                            var sliceRect = new Rect(_selRectInUVs);
                            sliceRect.min *= size;
                            sliceRect.max *= size;
                            sliceRect = addMarignToRect(sliceRect, marginInLayoutPxX, marginInLayoutPxY);
                            _textureSliceContainer.style.width = sliceRect.width;
                            _textureSliceContainer.style.height = sliceRect.height;
                            _textureSliceContainer.style.left = _textureContainer.style.left.value.value + sliceRect.min.x;
                            _textureSliceContainer.style.bottom = _textureContainer.style.bottom.value.value + sliceRect.min.y;
                            _textureSliceContainer.style.backgroundImage = _textureEditSliceForUI;
#if UNITY_2022_1_OR_NEWER
                            _textureSliceContainer.style.backgroundSize = new BackgroundSize(new Length(100, LengthUnit.Percent), new Length(100, LengthUnit.Percent));
#else
                            _textureSliceContainer.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
#endif
                        }
                    }
                }
                else
                {
                    _rectUVGizmoContainer.style.display = DisplayStyle.None;
                }// end has vertices selected


                // Interactive Crop gizmo
                {
                    _rectCropGizmoContainer.style.width = _textureContainer.style.width;
                    _rectCropGizmoContainer.style.height = _textureContainer.style.height;
                    _rectCropGizmoContainer.style.left = _textureContainer.style.left;
                    _rectCropGizmoContainer.style.bottom = _textureContainer.style.bottom;
                    _rectCropGizmoContainer.InvertVertical = true;
                    _rectCropGizmoContainer.LineWidth = 2f;
                    _rectCropGizmoContainer.LineColor = CropGimzoColor;
                    _rectCropGizmoContainer.ClearVertices();
                    // quad
                    _rectCropGizmoContainer.StartNewLine();
                    _rectCropGizmoContainer.AddVertex(new Vector2(_cropRectInUVs.xMin, _cropRectInUVs.yMin) * size);
                    _rectCropGizmoContainer.AddVertex(new Vector2(_cropRectInUVs.xMin, _cropRectInUVs.yMax) * size);
                    _rectCropGizmoContainer.AddVertex(new Vector2(_cropRectInUVs.xMax, _cropRectInUVs.yMax) * size);
                    _rectCropGizmoContainer.AddVertex(new Vector2(_cropRectInUVs.xMax, _cropRectInUVs.yMin) * size, closeLoop: true);

                    _rectCropGizmoContainer.MarkDirtyRepaint();

                    // Texture Gizmos (interactive areas)
                    var lineWidth = _rectCropGizmoContainer.LineWidth;
                    var cropGizmoSize = lineWidth * 4f;
                    var cropGizmoSizeHalf = lineWidth * 2f;

                    // Scale Corner BottomLeft
                    _gizmoCropRectBL = new Rect();
                    _gizmoCropRectBL.min = new Vector2(_cropRectInUVs.xMin - cropGizmoSizeHalf / size, _cropRectInUVs.yMin - cropGizmoSizeHalf / size);
                    _gizmoCropRectBL.max = new Vector2(_cropRectInUVs.xMin + cropGizmoSizeHalf / size, _cropRectInUVs.yMin + cropGizmoSizeHalf / size);
                    updateGizmo(ref _blCropGizmo, show: _editCropEnabled, _gizmoCropRectBL.min, cropGizmoSize, cropGizmoSize);

                    // Crop Corner TopLeft
                    _gizmoCropRectTL = new Rect();
                    _gizmoCropRectTL.min = new Vector2(_cropRectInUVs.xMin - cropGizmoSizeHalf / size, _cropRectInUVs.yMax - cropGizmoSizeHalf / size);
                    _gizmoCropRectTL.max = new Vector2(_cropRectInUVs.xMin + cropGizmoSizeHalf / size, _cropRectInUVs.yMax + cropGizmoSizeHalf / size);
                    updateGizmo(ref _tlCropGizmo, show: _editCropEnabled, _gizmoCropRectTL.min, cropGizmoSize, cropGizmoSize);

                    // Crop Corner TopRight
                    _gizmoCropRectTR = new Rect();
                    _gizmoCropRectTR.min = new Vector2(_cropRectInUVs.xMax - cropGizmoSizeHalf / size, _cropRectInUVs.yMax - cropGizmoSizeHalf / size);
                    _gizmoCropRectTR.max = new Vector2(_cropRectInUVs.xMax + cropGizmoSizeHalf / size, _cropRectInUVs.yMax + cropGizmoSizeHalf / size);
                    updateGizmo(ref _trCropGizmo, show: _editCropEnabled, _gizmoCropRectTR.min, cropGizmoSize, cropGizmoSize);

                    // Crop Corner BottomRight
                    _gizmoCropRectBR = new Rect();
                    _gizmoCropRectBR.min = new Vector2(_cropRectInUVs.xMax - cropGizmoSizeHalf / size, _cropRectInUVs.yMin - cropGizmoSizeHalf / size);
                    _gizmoCropRectBR.max = new Vector2(_cropRectInUVs.xMax + cropGizmoSizeHalf / size, _cropRectInUVs.yMin + cropGizmoSizeHalf / size);
                    updateGizmo(ref _brCropGizmo, show: _editCropEnabled, _gizmoCropRectBR.min, cropGizmoSize, cropGizmoSize);
                }

            } // end has UVs

            if (!hasUVs)
            {
                foreach (var uvLinesContainer in _uvLinesContainers)
                {
                    uvLinesContainer.style.display = DisplayStyle.None;
                }
                _rectUVGizmoContainer.style.display = DisplayStyle.None;
            }

            // Draw vertices
            _vertexContainer.style.display = hasUVs ? DisplayStyle.Flex : DisplayStyle.None;
            // Draw vertices
            // Don't draw in crop or _editTexture mode while dragging.
            if (hasUVs && !_editCropEnabled && (!_editTextureEnabled || (_editTextureEnabled && !showScaleAndRotateGizmos)))
            {
                _vertexContainer.style.width = _textureContainer.style.width;
                _vertexContainer.style.height = _textureContainer.style.height;
                _vertexContainer.style.left = _textureContainer.style.left;
                _vertexContainer.style.bottom = _textureContainer.style.bottom;
                _vertexContainer.InvertVertical = true;
                _vertexContainer.LineWidth = (0.015f * size) / _zoom; // vertex square size is 1.5% of total texture corrected for zoom.
                _vertexContainer.LineColor = new Color(1f, 1f, 1f, 1f);

                _vertexContainer.ClearVertices();

                // Draw the vertices that are inside the selection rect or that are already selected.
                Rect rect = getSelectionRectUV();

                // Material infos for edit texture mode (select only those vertices that have the same material).
                var tmpMaterials = GetRendererFromGameObject(SelectedGameObject).sharedMaterials;

                List<int> subMeshTris = new List<int>();
                for (int subMeshIndex = 0; subMeshIndex < _selectedMesh.subMeshCount; subMeshIndex++)
                {
                    _selectedMesh.GetTriangles(subMeshTris, subMeshIndex);

                    int numOfVertices = subMeshTris.Count;
                    int maxVerticesToDraw = 65500;
                    int numOfVerticesDrawn = 0;
                    bool reachedMaxVerticesLimit = false;
                    for (int v = 0; v < numOfVertices; v++)
                    {
                        // Skip vertices of other sub meshes if edit-texture mode is enabled.
                        if (_editTextureEnabled && tmpMaterials[subMeshIndex] != _textureMaterial)
                            continue;

                        int index = subMeshTris[v];
                        var x = uvs[index].x;
                        var y = uvs[index].y;

                        bool contains = rect.Contains(uvs[index]);
                        bool isSelected = _selectedVertices.Contains(index);

                        // Draw selected vertices (limit to 65k, 6 vertices in UI per UV vertex)
                        if ((contains || isSelected))
                        {
                            if (numOfVerticesDrawn * 6 < maxVerticesToDraw)
                            {
                                // Hide contained if ctrl is pressed to preview deselect, otherwise show vertices that are contained or already selected.
                                if (!_ctrlIsPressed || !contains)
                                {
                                    var lwh = _vertexContainer.LineWidth * 0.5f;
                                    _vertexContainer.StartNewLine();
                                    _vertexContainer.AddVertex(x * size - lwh, y * size);
                                    _vertexContainer.AddVertex(x * size + lwh, y * size);
                                    numOfVerticesDrawn++;
                                }
                            }
                            else
                            {
                                reachedMaxVerticesLimit = true;
                            }
                        }
                    }

                    if (reachedMaxVerticesLimit)
                    {
                        Logger.LogMessage("Can not draw more than 10.000 selected vertices. However, all the vertices are selected (just not shown).");
                    }
                }
            }
            else
            {
                _vertexContainer.ClearVertices();
            }
            _vertexContainer.MarkDirtyRepaint();

            bool showSelectionRect = !_editCropEnabled;
            if (showSelectionRect && Vector2.Distance(_selectionRectStart, _selectionRectEnd) > 0f)
            {
                _selectionRectContainer.style.display = DisplayStyle.Flex;
                _selectionRectContainer.style.width = _textureContainer.style.width;
                _selectionRectContainer.style.height = _textureContainer.style.height;
                _selectionRectContainer.style.left = _textureContainer.style.left;
                _selectionRectContainer.style.bottom = _textureContainer.style.bottom;
                _selectionRectContainer.InvertVertical = true;
                _selectionRectContainer.LineWidth = (0.003f * size) / _zoom; // vertex square size is 1.5% of total texture corrected for zoom.
                _selectionRectContainer.LineColor = new Color(0.6f, 0.7f, 1f, 0.7f);

                _selectionRectContainer.ClearVertices();

                // Rect Border
                _selectionRectContainer.StartNewLine();
                _selectionRectContainer.AddVertex(_selectionRectStart * size);
                _selectionRectContainer.AddVertex(new Vector2(_selectionRectEnd.x, _selectionRectStart.y) * size);
                _selectionRectContainer.AddVertex(_selectionRectEnd * size);
                _selectionRectContainer.AddVertex(new Vector2(_selectionRectStart.x, _selectionRectEnd.y) * size, closeLoop: true);

                // Rect Fill
                _selectionRectContainer.StartNewLine();
                _selectionRectContainer.LineWidth = Mathf.Abs(_selectionRectEnd.y - _selectionRectStart.y) * size;
                _selectionRectContainer.AddVertex(new Vector2(_selectionRectStart.x, _selectionRectStart.y + (_selectionRectEnd.y - _selectionRectStart.y) * 0.5f) * size, new Color(0.7f, 0.8f, 1f, 0.3f));
                _selectionRectContainer.AddVertex(new Vector2(_selectionRectEnd.x,   _selectionRectStart.y + (_selectionRectEnd.y - _selectionRectStart.y) * 0.5f) * size, new Color(0.7f, 0.8f, 1f, 0.3f));

                _selectionRectContainer.MarkDirtyRepaint();
            }
            else
            {
                _selectionRectContainer.style.display = DisplayStyle.None;
            }
        }

        private void updateSelectionRect()
        {
            var uvs = getUVWorkingCopyForSelection(_uvChannel);

            if (uvs == null) // Happens in undp/redo. TODO: Investigate.
                return;

            _selRectInUVs = new Rect();
            _selRectInUVs.xMin = 1f;
            _selRectInUVs.yMin = 1f;
            _selRectInUVs.xMax = 0f;
            _selRectInUVs.yMax = 0f;

            if (_selectedVertices.Count > 0)
            {
                foreach (var v in _selectedVertices)
                {
                    var uv = uvs[v];

                    if (uv.x < _selRectInUVs.xMin) _selRectInUVs.xMin = uv.x;
                    if (uv.y < _selRectInUVs.yMin) _selRectInUVs.yMin = uv.y;
                    if (uv.x > _selRectInUVs.xMax) _selRectInUVs.xMax = uv.x;
                    if (uv.y > _selRectInUVs.yMax) _selRectInUVs.yMax = uv.y;
                }

                if (_editTextureEnabled && _rectGizmoDraggedMoveHandle == 0 && _selRectInUVs.width > 0f && _selRectInUVs.height > 0f)
                {
                    updateTextureEditSelection();
                }
            }
            else
            {
                _selRectInUVs.xMin = 0f;
                _selRectInUVs.yMin = 0f;
                _selRectInUVs.xMax = 0f;
                _selRectInUVs.yMax = 0f;
            }
        }

        private Rect getSelectionRectUV()
        {
            float width = _selectionRectEnd.x - _selectionRectStart.x;
            float height = _selectionRectEnd.y - _selectionRectStart.y;
            Rect rect = new Rect(width > 0 ? _selectionRectStart.x : _selectionRectEnd.x, height > 0 ? _selectionRectStart.y : _selectionRectEnd.y, Mathf.Abs(width), Mathf.Abs(height));
            return rect;
        }

        /// <summary>
        /// Returns true if the given triangle should be filtered out based on the UVs.
        /// </summary>
        /// <param name="vIndex0"></param>
        /// <param name="vIndex1"></param>
        /// <param name="vIndex2"></param>
        /// <returns></returns>
        private bool filterUV( int vIndex0, int vIndex1, int vIndex2)
        {
            /*
            if (_uvFilter == UVFilter.Selected)
            {
                bool isSelected = false;
                for (int i = 0; i < selectedTrisCount; i++)
                {
                    var tri = selectedTriangles[i];
                    if (tri.TriangleIndices.x == vIndex0
                        && tri.TriangleIndices.y == vIndex1
                        && tri.TriangleIndices.z == vIndex2)
                    {
                        isSelected = true;
                        break;
                    }
                }

                if (!isSelected)
                {
                    return true;
                }
            }
            */

            return false;
        }

        protected bool isClockwise(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) < 0f;
        }

        [System.NonSerialized] private Texture2D _lastUsedFillTexture;
        [System.NonSerialized] private Vector2 _lastUsedFillTextureOffset;
        [System.NonSerialized] private Vector2 _lastUsedFillTextureScale;

        private void fillTextureContainerWithTiledTexture(Texture2D texture, Material material, Vector2 offset, Vector2 scale)
        {
            // Update only if necessary
            if (_lastUsedFillTexture != texture || _lastUsedFillTextureOffset != offset || _lastUsedFillTextureScale != scale)
            {
                _lastUsedFillTexture = texture;
                _lastUsedFillTextureOffset = offset;
                _lastUsedFillTextureScale = scale;

                // Remove all children from textureContainer
                _textureContainer.Clear();

                if (scale == Vector2.one && offset == Vector2.zero)
                {
                    _textureContainer.style.flexWrap = Wrap.NoWrap;
                    _textureContainer.style.flexDirection = FlexDirection.Row;
                    _textureContainer.style.backgroundImage = texture == null ? _checkerTexture : texture;
                    // If the sub mesh has no texture then teh texture is _checkerTexture
                    if (texture == null && material != null)
                    {
                        var col = material.color;
                        col.a = 0.7f;
                        _textureContainer.style.unityBackgroundImageTintColor = col;
                    }
                    else
                    {
                        _textureContainer.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, 1f);
                    }

#if UNITY_2022_1_OR_NEWER
                    _textureContainer.style.backgroundSize = new BackgroundSize(new Length(100, LengthUnit.Percent), new Length(100, LengthUnit.Percent));
#else
                    _textureContainer.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
#endif
                }
                else
                {
                    // n2h: In Unity 2022+ we finally can tile background textures. > Update once the time has come to drop 2021 support
                    _textureContainer.style.flexWrap = Wrap.NoWrap;
                    _textureContainer.style.flexDirection = FlexDirection.ColumnReverse;
                    _textureContainer.style.backgroundImage = StyleKeyword.None;

                    // Add one if offset exists.
                    var tiles = new Vector2Int(
                        Mathf.CeilToInt(Mathf.Abs(scale.x)) + (offset != Vector2.zero ? 1 : 0),
                        Mathf.CeilToInt(Mathf.Abs(scale.y)) + (offset != Vector2.zero ? 1 : 0)
                        );

                    // Ensure positive offsets to keep the tiles in view
                    // (because we only add on additional tiles for the positive direction).
                    // It will still fail for ranges > +-1f but we don't care.
                    if (offset.x < 0f)
                        offset.x += 1f;
                    if (offset.y < 0f)
                        offset.y += 1f;

                    // Create children with flex wrap and % base on scale and offset.
                    float tileWithInPercent = 100f / Mathf.Abs(scale.x);
                    float tileHeightInPercent = 100f / Mathf.Abs(scale.y);
                    for (int y = 0; y < tiles.y; y++)
                    {
                        var row = new VisualElement();
                        row.style.flexWrap = Wrap.NoWrap;
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.flexGrow = 0f;
                        row.style.flexShrink = 0f;
                        row.style.width = new Length(tileWithInPercent * tiles.x, LengthUnit.Percent);
                        row.style.height = new Length(tileHeightInPercent, LengthUnit.Percent);
                        row.style.position = Position.Relative;
                        row.style.left = new Length((-offset.x * 100f) / Mathf.Abs(scale.x), LengthUnit.Percent);
                        row.style.bottom = new Length((-offset.y * 100f) / Mathf.Abs(scale.y), LengthUnit.Percent);

                        for (int x = 0; x < tiles.x; x++)
                        {
                            var tile = new VisualElement();
                            tile.style.backgroundImage = texture == null ? _checkerTexture : texture;
                            // If the sub mesh has no texture then teh texture is _checkerTexture
                            // If the sub mesh has no texture then teh texture is _checkerTexture
                            if (texture == null && material != null)
                            {
                                var col = material.color;
                                col.a = 0.7f;
                                tile.style.unityBackgroundImageTintColor = col;
                            }
                            else
                            {
                                tile.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, 1f);
                            }
#if UNITY_2022_1_OR_NEWER
                            tile.style.backgroundSize = new BackgroundSize(new Length(100, LengthUnit.Percent), new Length(100, LengthUnit.Percent));
#else
                            tile.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
#endif
                            tile.style.width = new Length(tileWithInPercent, LengthUnit.Percent);
                            tile.style.height = new Length(100, LengthUnit.Percent);
                            row.Add(tile);
                        }

                        _textureContainer.Add(row);
                    }
                }
            }
        }

        private void calcCanvasSize(out float canvasWidth, out float canvasHeight, out float size, out float margin)
        {
            canvasWidth = _canvas.resolvedStyle.width;
            canvasHeight = _canvas.resolvedStyle.height;
            margin = 25;

            // Calculate the maximum size the square can have within the parent
            float maxSize = Mathf.Min(canvasWidth - margin * 2f, canvasHeight - margin * 2f);

            // Apply zoom to the max size
            size = maxSize * _zoom;
        }
    }
}