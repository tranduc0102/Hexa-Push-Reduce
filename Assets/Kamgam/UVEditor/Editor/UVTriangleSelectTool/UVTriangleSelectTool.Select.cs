using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Kamgam.UVEditor
{
    partial class UVTriangleSelectTool
    {
        public enum SelectionUpdateCause { Unknown, UndoPerformed, Add, Remove, ModeChanged }

        protected HashSet<SelectedTriangle> _selectedTriangles = new HashSet<SelectedTriangle>();
        public HashSet<SelectedTriangle> SelectedTriangles => _selectedTriangles;

        protected SelectedTriangle _lastSelectedTriangle = null;
        public SelectedTriangle LastSelectedTriangle => _lastSelectedTriangle;

        public SelectedTriangle LastOrAnySelectedTriangle
        {
            get
            {
                if (_lastSelectedTriangle != null && _selectedTriangles.Contains(_lastSelectedTriangle))
                {
                    return _lastSelectedTriangle;
                }
                else if(_selectedTriangles.Count > 0)
                {
                    var iter = _selectedTriangles.GetEnumerator();
                    iter.MoveNext();
                    return iter.Current;
                }

                return null;
            }
        }

        public SelectedTriangle FirstSelectedTriangle
        {
            get
            {
                if (_selectedTriangles.Count > 0)
                {
                    var iter = _selectedTriangles.GetEnumerator();
                    iter.MoveNext();
                    return iter.Current;
                }

                return null;
            }
        }

        protected SelectedTriangle _lastClickedTriangle = null;

        // tmp list for raycast results
        protected List<RayCastTriangleResult> _hoverTriangles = new List<RayCastTriangleResult>();
        public List<RayCastTriangleResult> HoverTriangles => _hoverTriangles;

        protected List<RayCastTriangleResult> _currentDragSelectionTriangles = new List<RayCastTriangleResult>();

        // tmp list for triangles selected while
        protected SelectedTriangle _tmpLastSelectedTriangle;
        protected SelectedTriangle _tmpLastDeselectedTriangle;

        protected float _selectBrushSize = 0f;
        protected float _selectBrushDepth = 0.5f;
        protected bool _selectCullBack = true;
        protected bool _limitLinkedSearchToSubMesh = false;

        protected bool _removeFromSelection = false;
        public bool RemoveFromSelection => _removeFromSelection;

        [System.NonSerialized]
        protected Vector3 _camPos = new Vector3(0f, 0f, -0.001f); // arbitrary start pos not 0/0/0 to trigger an update at first check.
        [System.NonSerialized]
        protected Quaternion _camRot = Quaternion.identity;
        [System.NonSerialized]
        protected float _camAspect = 0f;
        [System.NonSerialized]
        protected double _cameraLastMoveTime = -1;

        protected bool _scheduledMeshCacheUpdateDueToScrollWheel = false;

        [System.NonSerialized]
        protected bool _selectedTrianglesChangedSinceLastUndoRecording = false;

        static Material _selectionMaterialCulled;
        static Material _selectionMaterialNoCull;
        static Material createSelectionMaterial(Color color, bool cullBack)
        {
            if (cullBack)
                return createSelectionMaterial(ref _selectionMaterialCulled, color, cullBack: true);
            else
                return createSelectionMaterial(ref _selectionMaterialNoCull, color, cullBack: false);
        }

        static Material createSelectionMaterial(ref Material material, Color color, bool cullBack)
        {
            if (material == null)
            {
                // Unity has a built-in shader that is useful for drawing
                // simple colored things.
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
                // Turn on alpha blending
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // Turn backface culling off
                material.SetInt("_Cull", (int)(cullBack ? UnityEngine.Rendering.CullMode.Back : UnityEngine.Rendering.CullMode.Off));
                // Turn off depth writes
                material.SetInt("_ZWrite", 1);
                // ZTest = 8 = "Always", 2 = "Less", according to https://docs.unity3d.com/ScriptReference/Rendering.CompareFunction.html
                material.SetInt("_ZTest", cullBack ? 2 : 8);
                material.SetColor("_Color", color);
            }
            else
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        protected void updateSelectedTrianglesAfterCacheChange()
        {
            foreach (var tri in _selectedTriangles)
            {
                tri.Mesh = TriangleCache.GetCachedMesh(tri.Component);
                tri.UpdateWorldPos();
            }
        }

        public void ResetSelect()
        {
            _selectBrushSize = 0f;
            _selectBrushDepth = 0.5f;
            _selectCullBack = true;
            _limitLinkedSearchToSubMesh = false;

            _removeFromSelection = false;
        }

        List<SelectedTriangle> _tmpTrisToDelete = new List<SelectedTriangle>();

        public void DefragTriangles()
        {
            _tmpTrisToDelete.Clear();
            foreach (var tri in _selectedTriangles)
            {
                if (tri.Transform == null || tri.Component == null)
                {
                    _tmpTrisToDelete.Add(tri);
                }
            }

            foreach (var tri in _tmpTrisToDelete)
            {
                _selectedTriangles.Remove(tri);
            }

            UVEditor?.OnSelectedTrianglesChanged(_selectedTriangles);
        }

        public bool ContainsTri(Component comp, int vertexIndex0, int vertexIndex1, int vertexIndex2)
        {
            foreach (var tri in _selectedTriangles)
            {
                if (tri.Transform == null || tri.Component == null)
                    continue;

                if (tri.Component == comp
                    && tri.TriangleIndices[0] == vertexIndex0
                    && tri.TriangleIndices[1] == vertexIndex1
                    && tri.TriangleIndices[2] == vertexIndex2
                    )
                {
                    return true;
                }
            }

            return false;
        }

        protected void resetCameraMemory()
        {
            _camPos = new Vector3(0f, 0f, -0.001f);
            _camRot = Quaternion.identity;
            _camAspect = 0f;
            _cameraLastMoveTime = -1;
        }

        protected bool didCameraChange(Camera cam)
        {
            if (cam == null)
                return false;

            bool moved = cam.transform.position != _camPos || cam.transform.rotation != _camRot || _camAspect != cam.aspect;

            _camPos = cam.transform.position;
            _camRot = cam.transform.rotation;
            _camAspect = cam.aspect;

            return moved;
        }

        protected bool isMouseInSceneView()
        {
            return EditorWindow.mouseOverWindow != null && SceneView.sceneViews.Contains(EditorWindow.mouseOverWindow);
        }

        protected bool selectConsumesScrollWheelEvent(Event evt)
        {
            return evt.isScrollWheel && evt.shift;
        }

        public void ClearSelection()
        {
            UndoStack.Instance.StartEntry();
            UndoStack.Instance.AddUndoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));

            _selectedTriangles.Clear();
            _lastSelectedTriangle = null;
            TriangleCache.RebuildBakedMeshesCache(SelectedObjects);

            UndoStack.Instance.AddRedoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));
            UndoStack.Instance.EndEntry();

            UVEditor?.OnSelectedTrianglesChanged(_selectedTriangles);
        }

        protected List<SelectedTriangle> _tmpSelectedTrianglesPreInversion = new List<SelectedTriangle>();
        protected List<Component> _tmpInversionComponentsWithSelectedTris = new List<Component>();

        protected void invertSelection(bool limitToObjectsWithSelections)
        {
            UndoStack.Instance.StartEntry();
            UndoStack.Instance.AddUndoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));

            _tmpInversionComponentsWithSelectedTris.Clear();

            // copy selected tris
            foreach (var tri in _selectedTriangles)
            {
                _tmpSelectedTrianglesPreInversion.Add(tri);
                if (!_tmpInversionComponentsWithSelectedTris.Contains(tri.Component))
                {
                    _tmpInversionComponentsWithSelectedTris.Add(tri.Component);
                }
            }

            // clear
            _selectedTriangles.Clear();
            _lastSelectedTriangle = null;

            // fill with inverted selection
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
            List<SkinnedMeshRenderer> skinnedRenderers = new List<SkinnedMeshRenderer>();
            TriangleCache.GetRenderers(SelectedObjects, meshRenderers, skinnedRenderers);

            // Renderers
            foreach (var renderer in meshRenderers)
            {
                var meshFilter = renderer.gameObject.GetComponent<MeshFilter>();

                if (limitToObjectsWithSelections && !_tmpInversionComponentsWithSelectedTris.Contains(meshFilter))
                {
                    continue;
                }

                var mesh = meshFilter.sharedMesh;
                var vertices = mesh.vertices;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var subMeshTriangles = mesh.GetTriangles(i);
                    for (int t = 0; t < subMeshTriangles.Length; t+=3)
                    {
                        int v0 = subMeshTriangles[t];
                        int v1 = subMeshTriangles[t+1];
                        int v2 = subMeshTriangles[t+2];

                        // Search for triangle
                        bool found = false;
                        foreach (var tri in _tmpSelectedTrianglesPreInversion)
                        {
                            if (tri.SubMeshIndex == i && tri.TriangleIndices.x == v0 && tri.TriangleIndices.y == v1 && tri.TriangleIndices.z == v2)
                            {
                                found = true;
                                break;
                            }
                        }
                        // Add as new triangle if NOT found in selected
                        if (!found)
                        {
                            var tri = new SelectedTriangle();
                            tri.TriangleIndices = new Vector3Int(v0, v1, v2);
                            tri.SubMeshIndex = i;
                            tri.Component = meshFilter;
                            tri.Transform = meshFilter.transform;
                            tri.Success = true;
                            tri.VertexLocal0 = vertices[v0];
                            tri.VertexLocal1 = vertices[v1];
                            tri.VertexLocal2 = vertices[v2];
                            tri.UpdateWorldPos();
                            tri.Mesh = mesh;

                            _selectedTriangles.Add(tri);
                        }
                    }
                }
            }

            // Skinned Renderers
            foreach (var renderer in skinnedRenderers)
            {
                if (limitToObjectsWithSelections && !_tmpInversionComponentsWithSelectedTris.Contains(renderer))
                {
                    continue;
                }

                var mesh = TriangleCache.GetBakedMeshForComponent(renderer);
                if (mesh == null)
                    continue;

                var vertices = mesh.vertices;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var subMeshTriangles = mesh.GetTriangles(i);
                    for (int t = 0; t < subMeshTriangles.Length; t += 3)
                    {
                        int v0 = subMeshTriangles[t];
                        int v1 = subMeshTriangles[t + 1];
                        int v2 = subMeshTriangles[t + 2];

                        // Search for triangle
                        bool found = false;
                        foreach (var tri in _tmpSelectedTrianglesPreInversion)
                        {
                            if (tri.SubMeshIndex == i && tri.TriangleIndices.x == v0 && tri.TriangleIndices.y == v1 && tri.TriangleIndices.z == v2)
                            {
                                found = true;
                                break;
                            }
                        }
                        // Add as new triangle if NOT found in selected
                        if (!found)
                        {
                            var tri = new SelectedTriangle();
                            tri.TriangleIndices = new Vector3Int(v0, v1, v2);
                            tri.SubMeshIndex = i;
                            tri.Component = renderer;
                            tri.Transform = renderer.transform;
                            tri.Success = true;
                            tri.VertexLocal0 = vertices[v0];
                            tri.VertexLocal1 = vertices[v1];
                            tri.VertexLocal2 = vertices[v2];
                            tri.UpdateWorldPos();
                            tri.Mesh = mesh;

                            _selectedTriangles.Add(tri);
                        }
                    }
                }
            }

            UndoStack.Instance.AddRedoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));
            UndoStack.Instance.EndEntry();

            UVEditor?.OnSelectedTrianglesChanged(_selectedTriangles);

            _tmpSelectedTrianglesPreInversion.Clear();
            _tmpInversionComponentsWithSelectedTris.Clear();
        }

        public bool HasValidTriangleSelection()
        {
            return _selectedTriangles.Count > 0;
        }

        // Called from within the tools OnToolGUI method.
        protected void onSelectGUI(SceneView sceneView)
        {
            // Update cache if triggerd by scroll wheel.
            if (_scheduledMeshCacheUpdateDueToScrollWheel && EditorApplication.timeSinceStartup - _cameraLastMoveTime > 0.05f)
            {
                _scheduledMeshCacheUpdateDueToScrollWheel = false;
                TriangleCache.CacheTriangles(sceneView.camera, SelectedObjects);
                // Make sure the cached world space is in sync.
                updateSelectedTrianglesAfterCacheChange();
            }

            var settings = UVEditorSettings.GetOrCreateSettings();

            // Clear if in view mode
            if (Tools.viewToolActive)
            {
                _hoverTriangles.Clear();
            }

            bool isEditingTexture = (UVEditorWindow.Instance != null && UVEditorWindow.Instance.IsEditingTexture);

            var evt = Event.current;
            if ((evt.isMouse || evt.isScrollWheel || evt.isKey || evt.type == EventType.Repaint) && !evt.alt && !isEditingTexture)
            {
                // update brush size
                if (selectConsumesScrollWheelEvent(evt))
                {
                    float newSize = _selectBrushSize + evt.delta.y * -0.0035f * settings.ScrollWheelSensitivity;
                    _selectBrushSize = Mathf.Clamp(newSize, 0f, 1f);
                }

                // Do raycasting only if the mouse is in the scene view
                if (isMouseInSceneView())
                {
                    if (!_mouseIsDown)
                    {
                        _tmpLastSelectedTriangle = null;
                        _tmpLastDeselectedTriangle = null;
                    }

                    if (_leftMouseWasPressed)
                    {
                        UndoStack.Instance.StartEntry();
                        UndoStack.Instance.AddUndoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));
                    }

                    // Raycast into scene.
                    _hoverTriangles.Clear();
                    if (!Tools.viewToolActive)
                    {
                        // Schedule cache update?
                        bool cameraChanged = sceneView != null && didCameraChange(sceneView.camera);
                        if (cameraChanged)
                        {
                            _cameraLastMoveTime = EditorApplication.timeSinceStartup;

                            // Update cache instantly after camera movement (this works because we do NOT execute this while Tools.viewToolActive is active).
                            if (EditorApplication.timeSinceStartup - _lastScrollWheelTime > 0.05f)
                            {
                                TriangleCache.CacheTriangles(sceneView.camera, SelectedObjects);
                                // Make sure the cached world space is in sync.
                                updateSelectedTrianglesAfterCacheChange();
                            }
                            else
                            {
                                // Schedule change due to scroll wheel.
                                // The scroll wheel does not trigger Tools.viewToolActive to be true,
                                // thus we need to keep track of it manually.
                                _scheduledMeshCacheUpdateDueToScrollWheel = true;
                            }
                        }

                        mouseDown = _mouseIsDown;

                        TriangleCache.GetTrianglesUnderMouse(
                            evt,
                            cullBack: _selectCullBack,
                            rayThickness: _selectBrushSize,
                            rayDepth: _selectCullBack ? _selectBrushDepth : float.MaxValue,
                            objects: SelectedObjects,
                            allowMultipleResults: _selectBrushSize > 0.001f || !_selectCullBack,
                            results: _hoverTriangles
                        );
                    }

                    if (_hoverTriangles.Count > 0)
                    {
                        _removeFromSelection = _controlPressed;

                        if (_leftMouseIsDown && !Tools.viewToolActive)
                        {
                            foreach (var result in _hoverTriangles)
                            {
                                var tri = new SelectedTriangle(result);
                                if (_selectedTriangles.Contains(tri))
                                {
                                    if (_removeFromSelection && !tri.Equals(_tmpLastSelectedTriangle))
                                    {
                                        _selectedTriangles.Remove(tri);
                                        _tmpLastDeselectedTriangle = tri;
                                        _selectedTrianglesChangedSinceLastUndoRecording = true;
                                    }
                                }
                                else
                                {
                                    if (!_removeFromSelection && !tri.Equals(_tmpLastDeselectedTriangle))
                                    {
                                        _selectedTriangles.Add(tri);
                                        _tmpLastSelectedTriangle = tri;
                                        _lastSelectedTriangle = tri;
                                        _selectedTrianglesChangedSinceLastUndoRecording = true;
                                    }
                                }
                                _lastClickedTriangle = tri;
                            }
                        }
                    }

                    // Register undo on mouse up
                    if (_leftMouseWasReleased)
                    {
                        if (SelectedObjects.Length > 0 && IsMouseInSceneView() && _selectedTrianglesChangedSinceLastUndoRecording)
                        {
                            _selectedTrianglesChangedSinceLastUndoRecording = false;

                            UndoStack.Instance.AddRedoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));
                        }

                        UndoStack.Instance.EndEntry();

                        UVEditor?.OnSelectedTrianglesChanged(_selectedTriangles);
                    }
                }

                if(_saveLoadVisibility == 0 
                    && evt.isKey && evt.modifiers == EventModifiers.None && evt.keyCode == UVEditorSettings.GetOrCreateSettings().TriggerSelectLinked
                    && evt.type == EventType.KeyUp
                    && !Tools.viewToolActive)
                {
                    SelectLinked( remove: evt.shift);
                }

                if (evt.isMouse || evt.isKey)
                {
                    SceneView.currentDrawingSceneView?.Repaint();
                }
            }
        }

        public static bool mouseDown;

        public void SelectLinked(bool remove = false, bool compareConnectedUVs = false, int uvLayoutIndex = 0)
        {
            UndoStack.Instance.StartEntry();
            UndoStack.Instance.AddUndoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));

            List<SelectedTriangle> trisToRemove = null;
            if (remove)
            {
                trisToRemove = new List<SelectedTriangle>();
            }

            if (_lastSelectedTriangle == null)
                return;

            var settings = UVEditorSettings.GetOrCreateSettings();
            var linkedTris = TriangleCache.AddLinked(
                SelectedObjects, LastSelectedTriangle, 
                _limitLinkedSearchToSubMesh, settings.MaxSelectLinkedDuration, settings.ShowSelectLinkedFailedPopup,
                compareConnectedUVs, uvLayoutIndex);
            foreach (var tri in linkedTris)
            {
                if (remove)
                {
                    if (_selectedTriangles.Contains(tri))
                    {
                        trisToRemove.Add(tri);
                    }
                }
                else
                {
                    if (!_selectedTriangles.Contains(tri))
                    {
                        _selectedTriangles.Add(tri);
                    }
                }
            }

            if (remove)
            {
                foreach (var tri in trisToRemove)
                {
                    _selectedTriangles.Remove(tri);
                }
            }

            UndoStack.Instance.AddRedoAction(undoSetSelectedTrianglesFunc(_selectedTriangles));
            UndoStack.Instance.EndEntry();

            UVEditor?.OnSelectedTrianglesChanged(_selectedTriangles);
        }

        // Will be called after all regular rendering is done
        public void onSelectRenderMesh()
        {
            if (Camera.current.cameraType != CameraType.SceneView)
                return;

            var camPos = Camera.current.transform.position;

            // Paint tris under mouse
            if (_hoverTriangles.Count > 0 && !_mouseIsDown)
            {
                var color = new Color(1f, 1f, 0f);
                if (_removeFromSelection)
                {
                    color = new Color(1f, 0f, 0f);
                }
                color.a = Mathf.Clamp01(UVEditorSettings.GetOrCreateSettings().SelectionColorAlpha + 0.1f);

                // Apply the line material
                var material = createSelectionMaterial(color, _selectCullBack);
                material.SetPass(0);

                GL.PushMatrix();
                GL.MultMatrix(Matrix4x4.identity);
                GL.Begin(GL.TRIANGLES);
                try
                {

                    foreach (var result in _hoverTriangles)
                    {
                        if (result.Transform == null)
                            continue;

                        // Draw triangles
                        Vector3 v0 = result.Transform.TransformPoint(result.VertexLocal0);
                        Vector3 v1 = result.Transform.TransformPoint(result.VertexLocal1);
                        Vector3 v2 = result.Transform.TransformPoint(result.VertexLocal2);

                        // displace tri slightly towards camera:
                        var d = (camPos - v0).normalized * 0.001f;

                        GL.Vertex3(v0.x + d.x, v0.y + d.y, v0.z + d.z);
                        GL.Vertex3(v1.x + d.x, v1.y + d.y, v1.z + d.z);
                        GL.Vertex3(v2.x + d.x, v2.y + d.y, v2.z + d.z);
                    }
                }
                finally
                {
                    GL.End();
                    GL.PopMatrix();
                }
            }

            // Paint selected tris
            var uvWindow = UVEditorWindow.Instance;
            bool skipDrawingDueToUVs = uvWindow != null && !uvWindow.TriangleSelectEnabled;
            if (_selectedTriangles.Count > 0 && !skipDrawingDueToUVs)
            {
                var color = new Color(0f, 1f, 0f);
                color.a = UVEditorSettings.GetOrCreateSettings().SelectionColorAlpha;

                // Apply the line material
                var material = createSelectionMaterial(color, _selectCullBack);
                material.SetPass(0);

                GL.PushMatrix();
                GL.MultMatrix(Matrix4x4.identity);
                GL.Begin(GL.TRIANGLES);
                try
                {
                    foreach (var result in _selectedTriangles)
                    {
                        if (result.Transform == null)
                            continue;

                        // Draw triangles
                        Vector3 v0 = result.Transform.TransformPoint(result.VertexLocal0);
                        Vector3 v1 = result.Transform.TransformPoint(result.VertexLocal1);
                        Vector3 v2 = result.Transform.TransformPoint(result.VertexLocal2);

                        // displace tri slightly towards camera:
                        var d = (camPos - v0).normalized * 0.001f;

                        GL.Vertex3(v0.x + d.x, v0.y + d.y, v0.z + d.z);
                        GL.Vertex3(v1.x + d.x, v1.y + d.y, v1.z + d.z);
                        GL.Vertex3(v2.x + d.x, v2.y + d.y, v2.z + d.z);
                    }
                }
                finally
                {
                    GL.End();
                    GL.PopMatrix();
                }
            }
        }

        protected bool saveSelection(string name, bool suppressReplacementConfirmation = false)
        {
            bool didSave = false;

            var newShapshot = new SelectedTriangleSnapshot(name, _selectedTriangles);

            bool found = false;
            var settings = UVEditorSettings.GetOrCreateSettings();
            var snapshots = settings.SelectionSnapshots;
            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot.Name == name)
                {
                    found = true;
                    bool replace = true;
                    // Ask to confirm replacement if the time between the two snapshots is long.
                    if (!suppressReplacementConfirmation && settings.AskBeforeReplacingExistingSelectionSnapshot)
                    {
                        replace = EditorUtility.DisplayDialog(
                            "Replace Existing?",
                            "There already is a snapshot with the name '" + name + "'. Should it be replaced?",
                            "Yes", "No");
                    }
                    if (replace)
                    {
                        snapshots[i] = newShapshot;
                        settings.SelectionSnapshots.Sort((a, b) => (int)(b.Timestamp - a.Timestamp));
                        EditorUtility.SetDirty(settings);
                        SaveAssetHelper.SaveAssetIfDirty(settings);
                        didSave = true;
                    }

                    break;
                }
            }

            if (!found)
            {
                settings.SelectionSnapshots.Add(newShapshot);
                settings.SelectionSnapshots.Sort((a, b) => (int)(b.Timestamp - a.Timestamp));
                EditorUtility.SetDirty(settings);
                SaveAssetHelper.SaveAssetIfDirty(settings);
                didSave = true;
            }

            _lastSelectedTriangle = null;

            return didSave;
        }

        protected void deleteSavedSelection(string name)
        {
            var settings = UVEditorSettings.GetOrCreateSettings();
            var snapshots = settings.SelectionSnapshots;
            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                var snapshot = snapshots[i];
                if (snapshot.Name == name)
                {
                    snapshots.RemoveAt(i);
                    break;
                }
            }
        }

        public void LoadSelection(SelectedTriangleSnapshot snapshot, bool additive)
        {
            TriangleCache.RebuildBakedMeshesCache(SelectedObjects);

            _lastSelectedTriangle = null;

            if (!additive)
            {
                _selectedTriangles.Clear();
            }

            foreach (var obj in SelectedObjects)
            {
                var objSkinnedRenderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
                var objMeshFilter = obj.GetComponentInChildren<MeshFilter>();
                Component objComp = objSkinnedRenderer != null ? (Component)objSkinnedRenderer : (Component)objMeshFilter;
                int objVertexCount = objSkinnedRenderer != null ? objSkinnedRenderer.sharedMesh.vertexCount : objMeshFilter.sharedMesh.vertexCount;
                SelectedTriangleSnapshot.ObjectSnapshot objSnapshot = null;

                foreach (var snp in snapshot.ObjectSnapshots)
                {
                    if (snp.ObjectName == objComp.gameObject.name && snp.VertexCount == objVertexCount)
                    {
                        objSnapshot = snp;
                    }
                }

                // Try again: This time compare vertex count only (skip name).
                if (objSnapshot == null)
                {
                    foreach (var snp in snapshot.ObjectSnapshots)
                    {
                        if (snp.VertexCount == objVertexCount)
                        {
                            objSnapshot = snp;
                        }
                    }
                }

                // If none was found and only one object is in the snapshot then trust the user
                // that he/she knows what they are doing.
                bool compareByPos = false;
                if (objSnapshot == null)
                {
                    if (snapshot.ObjectSnapshots.Count == 1)
                    {
                        objSnapshot = snapshot.ObjectSnapshots[0];
                        // Flag to let the code know that we should compare the triangles by
                        // position and not vertex index.
                        compareByPos = true;
                    }
                }

                // If objSnapshot is still null here then no snapshot matches the object
                // and we skip it.
                if (objSnapshot == null)
                    continue;

                // LOAD selected triangles for component

                // Skip empty lists
                if (objSnapshot.Triangles.Count == 0)
                    return;

                var maxVertexIndex = 0;
                foreach (var tri in objSnapshot.Triangles)
                {
                    maxVertexIndex = Mathf.Max(maxVertexIndex, tri.TriangleIndices[0]);
                    maxVertexIndex = Mathf.Max(maxVertexIndex, tri.TriangleIndices[1]);
                    maxVertexIndex = Mathf.Max(maxVertexIndex, tri.TriangleIndices[2]);
                }

                // Skip if there are not enough vertices
                if (!compareByPos && objVertexCount <= maxVertexIndex)
                    continue;

                if (objComp == null)
                    continue;

                var mesh = TriangleCache.GetCachedMesh(objComp);
                if (mesh == null)
                    continue;

                // Find the triangles in the object mesh that fits the tris in the snapshot.
                // We do the comparison by local vertex coordinates.
                var objVertices = mesh.vertices;
                var objTriangles = mesh.triangles;

                if (compareByPos)
                {
                    int foundTriVertexIndex0;
                    int foundTriVertexIndex1;
                    int foundTriVertexIndex2;
                    int triVertexIndex0;
                    int triVertexIndex1;
                    int triVertexIndex2;
                    foreach (var tri in objSnapshot.Triangles)
                    {
                        // Find a matching triangle in the existing mesh.
                        foundTriVertexIndex0 = -1;
                        foundTriVertexIndex1 = -1;
                        foundTriVertexIndex2 = -1;
                        for (int t = 0; t < objTriangles.Length; t+=3)
                        {
                            triVertexIndex0 = objTriangles[t];
                            triVertexIndex1 = objTriangles[t + 1];
                            triVertexIndex2 = objTriangles[t + 2];
                            if (   tri.VertexLocal0 == objVertices[triVertexIndex0]
                                && tri.VertexLocal1 == objVertices[triVertexIndex1]
                                && tri.VertexLocal2 == objVertices[triVertexIndex2])
                            {
                                foundTriVertexIndex0 = triVertexIndex0;
                                foundTriVertexIndex1 = triVertexIndex1;
                                foundTriVertexIndex2 = triVertexIndex2;
                                break;
                            }
                        }
                        if (foundTriVertexIndex0 < 0)
                            continue;

                        var selectedTri = new SelectedTriangle();
                        selectedTri.Success = true;
                        selectedTri.Transform = objComp.transform;
                        selectedTri.Mesh = TriangleCache.GetCachedMesh(objComp);
                        selectedTri.Component = objComp;
                        selectedTri.SubMeshIndex = tri.SubMeshIndex;
                        selectedTri.TriangleIndices = new Vector3Int(foundTriVertexIndex0, foundTriVertexIndex1, foundTriVertexIndex2);
                        selectedTri.VertexLocal0 = objVertices[selectedTri.TriangleIndices[0]];
                        selectedTri.VertexLocal1 = objVertices[selectedTri.TriangleIndices[1]];
                        selectedTri.VertexLocal2 = objVertices[selectedTri.TriangleIndices[2]];
                        if (selectedTri.Transform != null)
                        {
                            selectedTri.VertexGlobal0 = selectedTri.Transform.TransformPoint(selectedTri.VertexLocal0);
                            selectedTri.VertexGlobal1 = selectedTri.Transform.TransformPoint(selectedTri.VertexLocal1);
                            selectedTri.VertexGlobal2 = selectedTri.Transform.TransformPoint(selectedTri.VertexLocal2);
                        }

                        SelectedTriangle alreadySelectedTri = null;
                        foreach (var oldTri in _selectedTriangles)
                        {
                            if (oldTri.Equals(selectedTri))
                            {
                                alreadySelectedTri = oldTri;
                                break;
                            }
                        }
                        if (alreadySelectedTri == null)
                        {
                            _selectedTriangles.Add(selectedTri);
                        }
                    }
                }
                else
                {
                    foreach (var tri in objSnapshot.Triangles)
                    {
                        var selectedTri = new SelectedTriangle();
                        selectedTri.Success = true;
                        selectedTri.Transform = objComp.transform;
                        selectedTri.Mesh = TriangleCache.GetCachedMesh(objComp);
                        selectedTri.Component = objComp;
                        selectedTri.SubMeshIndex = tri.SubMeshIndex;
                        selectedTri.TriangleIndices = tri.TriangleIndices;
                        selectedTri.VertexLocal0 = objVertices[selectedTri.TriangleIndices[0]];
                        selectedTri.VertexLocal1 = objVertices[selectedTri.TriangleIndices[1]];
                        selectedTri.VertexLocal2 = objVertices[selectedTri.TriangleIndices[2]];
                        if (selectedTri.Transform != null)
                        {
                            selectedTri.VertexGlobal0 = selectedTri.Transform.TransformPoint(selectedTri.VertexLocal0);
                            selectedTri.VertexGlobal1 = selectedTri.Transform.TransformPoint(selectedTri.VertexLocal1);
                            selectedTri.VertexGlobal2 = selectedTri.Transform.TransformPoint(selectedTri.VertexLocal2);
                        }

                        SelectedTriangle alreadySelectedTri = null;
                        foreach (var oldTri in _selectedTriangles)
                        {
                            if (oldTri.Equals(selectedTri))
                            {
                                alreadySelectedTri = oldTri;
                                break;
                            }
                        }
                        if (alreadySelectedTri == null)
                        {
                            _selectedTriangles.Add(selectedTri);
                        }
                    }
                }
            }

            UVEditor?.OnSelectedTrianglesChanged(_selectedTriangles);

            // Update mesh reference
            foreach (var selectedTri in _selectedTriangles)
            {
                selectedTri.Mesh = TriangleCache.GetCachedMesh(selectedTri.Component);
            }
        }
    }
}
