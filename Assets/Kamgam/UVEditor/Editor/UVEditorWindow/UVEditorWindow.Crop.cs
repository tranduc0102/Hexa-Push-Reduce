using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Kamgam.UVEditor;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;


namespace Kamgam.UVEditor
{
    public partial class UVEditorWindow : EditorWindow
    {
        [System.NonSerialized] protected float _cropAspectRatio; // Width / height
        protected Rect _cropStartRectInUVs;
        protected int _lastDraggedCropCornerHandle;

        protected void internalStartCroppingCycle()
        {
            _cropRectInUVs = new Rect(0f, 0f, 1f, 1f);
            _cropStartRectInUVs = _cropRectInUVs;
            _lastDraggedCropCornerHandle = 1; // 1 = BL

            _cropWidthInput.SetValueWithoutNotify(_texture.width.ToString());
            _cropHeightInput.SetValueWithoutNotify(_texture.height.ToString());

            updateCropInputColorBasedOnPowerOfTwo(_cropWidthInput);
            updateCropInputColorBasedOnPowerOfTwo(_cropHeightInput);
        }

        private void updateCropInputColorBasedOnPowerOfTwo(TextField tf)
        {
            var input = tf.Q<VisualElement>(className: "unity-base-text-field__input");

            if (int.TryParse(tf.value, out int intValue))
            {
                if (Mathf.IsPowerOfTwo(intValue))
                    input.style.color = Color.green;
                else
                    input.style.color = StyleKeyword.Null;
            }
            else
            {
                input.style.color = StyleKeyword.Null;
            }
        }

        private void startCropDrag(int cornerHandle, MouseDownEvent evt)
        {
            // Clear control to avoid "Should not be capturing when there is a hotcontrol" errors.
            GUI.FocusControl(null);

            // Memorize starting conditions
            _cropAspectRatio = _texture.width / _texture.height;
        }

        private void onCropHeightInputChanged(ChangeEvent<string> evt)
        {
            onCropInputSizeChanged(_lastDraggedCropCornerHandle);
        }

        private void onCropWidthInputChanged(ChangeEvent<string> evt)
        {
            onCropInputSizeChanged(_lastDraggedCropCornerHandle);
        }

        private void onCropInputSizeChanged(int cornerHandle)
        {
            if (cornerHandle == 0)
                return;

            if (   int.TryParse(_cropWidthInput.value, out int widthInput)
                && int.TryParse(_cropHeightInput.value, out int heightInput))
            {

                float widthUV = widthInput / (float)_texture.width;
                float heightUV = heightInput / (float)_texture.height;

                // Corner (1 = BL, 2 = TL, 3 = TR, 4 = BR)
                if (cornerHandle == 1)
                {
                    _cropRectInUVs.min = _cropRectInUVs.max - new Vector2(widthUV, heightUV);
                }
                else if (cornerHandle == 2)
                {
                    var min = _cropRectInUVs.min;
                    var max = _cropRectInUVs.max;
                    _cropRectInUVs.min = new Vector2(max.x - widthUV, min.y);
                    _cropRectInUVs.max = new Vector2(max.x, min.y + heightUV);
                }
                else if (cornerHandle == 3)
                {
                    _cropRectInUVs.max = _cropRectInUVs.min + new Vector2(widthUV, heightUV);
                }
                else // if (cornerHandle == 4)
                {
                    var min = _cropRectInUVs.min;
                    var max = _cropRectInUVs.max;
                    _cropRectInUVs.min = new Vector2(min.x, max.y - heightUV);
                    _cropRectInUVs.max = new Vector2(min.x + widthUV, max.y);
                }

                updateCropInputColorBasedOnPowerOfTwo(_cropWidthInput);
                updateCropInputColorBasedOnPowerOfTwo(_cropHeightInput);
            }
        }


        private void cropDrag(int cornerHandle, MouseMoveEvent evt)
        {
            _lastDraggedCropCornerHandle = cornerHandle;

            // Update cursor (1 = BL, 2 = TL, 3 = TR, 4 = BR)
            if (cornerHandle == 1 || cornerHandle == 3) _cursorTargetElement.style.cursor = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.ResizeUpRight);
            if (cornerHandle == 2 || cornerHandle == 4) _cursorTargetElement.style.cursor = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.ResizeUpLeft);
            _nextCursorOnMouseUp = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.Arrow);

            // Clamp mouse pos to min/max to avoid inverted min/max.
            var mousePosMinClamp = new Vector2(
                Mathf.Clamp(_uvMousePos.x, float.MinValue, _cropRectInUVs.max.x),
                Mathf.Clamp(_uvMousePos.y, float.MinValue, _cropRectInUVs.max.y)
            );
            var mousePosMaxClamp = new Vector2(
                Mathf.Clamp(_uvMousePos.x, _cropRectInUVs.min.x, float.MaxValue),
                Mathf.Clamp(_uvMousePos.y, _cropRectInUVs.min.y, float.MaxValue)
            );

            // Update rect based on what corner is being dragged.
            if (cornerHandle == 1)
            {
                _cropRectInUVs.min = mousePosMinClamp;
            }
            else if (cornerHandle == 2)
            {
                _cropRectInUVs.min = new Vector2(mousePosMinClamp.x, _cropRectInUVs.min.y);
                _cropRectInUVs.max = new Vector2(_cropRectInUVs.max.x, mousePosMaxClamp.y);
            }
            else if (cornerHandle == 3)
            {
                _cropRectInUVs.max = mousePosMaxClamp;
            }
            else // if (cornerHandle == 4)
            {
                _cropRectInUVs.min = new Vector2(_cropRectInUVs.min.x, mousePosMinClamp.y);
                _cropRectInUVs.max = new Vector2(mousePosMaxClamp.x, _cropRectInUVs.max.y);
            }

            // Keep the aspect ratio if shift is pressed.
            if (evt.shiftKey)
            {
                // Update _cropRectInUVs to match _cropAspectRatio
                float newWidth;
                float newHeight;
                var aspect = _cropRectInUVs.width / _cropRectInUVs.height;
                if (aspect > _cropAspectRatio)
                {
                    // _cropRectInUVs is wider than the starting aspect ratio
                    newWidth = _cropRectInUVs.height * _cropAspectRatio;
                    newHeight = _cropRectInUVs.height;
                }
                else
                {
                    // _cropRectInUVs is higher than the starting aspect ratio
                    newWidth = _cropRectInUVs.width;
                    newHeight = _cropRectInUVs.width / _cropAspectRatio;
                }
                if (cornerHandle == 1)
                {
                    _cropRectInUVs.min = _cropRectInUVs.max - new Vector2(newWidth, newHeight);
                }
                else if (cornerHandle == 2)
                {
                    var min = _cropRectInUVs.min;
                    var max = _cropRectInUVs.max;
                    min.x = _cropRectInUVs.max.x - newWidth;
                    max.y = _cropRectInUVs.min.y + newHeight;
                    _cropRectInUVs.min = min;
                    _cropRectInUVs.max = max;
                }
                else if (cornerHandle == 3)
                {
                    _cropRectInUVs.max = _cropRectInUVs.min + new Vector2(newWidth, newHeight);
                }
                else // if (cornerHandle == 4)
                {
                    var min = _cropRectInUVs.min;
                    var max = _cropRectInUVs.max;
                    max.x = _cropRectInUVs.min.x + newWidth;
                    min.y = _cropRectInUVs.max.y - newHeight;
                    _cropRectInUVs.min = min;
                    _cropRectInUVs.max = max;
                }
            }

            // Clamp to 0..1 range
            _cropRectInUVs.min = new Vector2(
                Mathf.Clamp01(_cropRectInUVs.min.x),
                Mathf.Clamp01(_cropRectInUVs.min.y)
                );
            _cropRectInUVs.max = new Vector2(
                Mathf.Clamp01(_cropRectInUVs.max.x),
                Mathf.Clamp01(_cropRectInUVs.max.y)
                );

            var cropRectInPixels = uvToPixelRect(_cropRectInUVs, _texture);
            _cropWidthInput.SetValueWithoutNotify(cropRectInPixels.width.ToString());
            _cropHeightInput.SetValueWithoutNotify(cropRectInPixels.height.ToString());
            updateCropInputColorBasedOnPowerOfTwo(_cropWidthInput);
            updateCropInputColorBasedOnPowerOfTwo(_cropHeightInput);
        }

        private void stopCropDrag(int cornerHandle)
        {
        }

        protected void internalStopCroppingCycle()
        {
            _lastDraggedCropCornerHandle = 0;
        }

        private List<int> _tmpHandledIndicesForCropping = new List<int>();
        private List<Vector2> _tmpUVsForCropping = new List<Vector2>();

        private void applyCrop()
        {
            // Skip if crop area is too small.
            float pixelWidth = Mathf.RoundToInt(_cropRectInUVs.width * _texture.width);
            float pixelHeight = Mathf.RoundToInt(_cropRectInUVs.height * _texture.height);
            if (pixelWidth <= 0 || pixelHeight <= 0)
                return;

            // Skip if crop area has not changed.
            if (_cropRectInUVs == _cropStartRectInUVs)
                return;

            _tmpUVsForCropping.Clear();
            var mesh = GetSharedMeshFromGameObject(SelectedGameObject);
            mesh.GetUVs(_uvChannel, _tmpUVsForCropping);

            UndoStack.Instance.StartGroup();

            UndoStack.Instance.StartEntry();
            UndoStack.Instance.AddUndoAction(() => clearMeshCache());
            UndoStack.Instance.AddUndoAction(undoSetUVsOnGameObjectFunc(SelectedGameObject, _tmpUVsForCropping, _uvChannel));
            UndoStack.Instance.AddUndoAction(undoAssignMeshOnGameObjectFunc(SelectedGameObject, mesh));
            UndoStack.Instance.AddUndoAction(undoSetTextureAndMaterialsOnGameObjectFunc(SelectedGameObject, _texture, _textureMaterial));
            UndoStack.Instance.EndEntry();

            _texture = getNewTexture(_selectedObject, _selectedSubMeshIndex, _texture, recordUndo: true);
            var newMesh = getNewMesh(_selectedObject, mesh, recordUndo: true);
            EditorUtility.SetDirty(GetRendererFromGameObject(SelectedGameObject));
            _textureMaterial = getNewMaterial(_selectedObject, _selectedSubMeshIndex, _textureMaterial, recordCreationUndo: true);
            _textureMaterial.mainTexture = _texture;

            // Assign new mesh (we need no undo for this as that's handled by getNewMesh()).
            if (newMesh != null)
            {
                SetSharedMeshOnGameObject(SelectedGameObject, newMesh);
            }

            // Assign new material to all sub meshes that have the same material as the one of subMeshIndex.
            var renderer = GetRendererFromGameObject(SelectedGameObject);
            var materials = renderer.sharedMaterials;
            var existingMaterial = materials[_selectedSubMeshIndex];
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == existingMaterial)
                {
                    materials[i] = _textureMaterial;
                }
            }
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);

            // UVs
            {
                _tmpUVsForCropping.Clear();
                newMesh.GetUVs(_uvChannel, _tmpUVsForCropping);

                // Calc UV change offset and scale.
                var uvOffset = new Vector2(
                    _cropStartRectInUVs.min.x - _cropRectInUVs.min.x,
                    _cropStartRectInUVs.min.y - _cropRectInUVs.min.y
                    );

                var uvScale = new Vector2(
                    _cropStartRectInUVs.width / _cropRectInUVs.width,
                    _cropStartRectInUVs.height / _cropRectInUVs.height
                    );

                // Change only those UVs that have the same main texture.
                List<int> handledIndices = new List<int>();
                for (int s = 0; s < newMesh.subMeshCount; s++)
                {
                    // Update: We better check for the same texture, since that's the really relevant part.
                    bool sameTexture = false;
                    if (renderer.sharedMaterials[s] != null && _textureMaterial != null)
                    {
                        sameTexture = renderer.sharedMaterials[s].mainTexture == _textureMaterial.mainTexture;
                    }
                    else
                    {
                        sameTexture = renderer.sharedMaterials[s] == _textureMaterial;
                    }

                    if (!sameTexture)
                        continue;

                    // Change uvs in copy
                    var tris = newMesh.GetTriangles(s);
                    for (int i = 0; i < tris.Length; i++)
                    {
                        if (!handledIndices.Contains(tris[i]))
                        {
                            handledIndices.Add(tris[i]);
                            _tmpUVsForCropping[tris[i]] = (_tmpUVsForCropping[tris[i]] + uvOffset) * uvScale;
                        }
                    }
                }

                // Apply to mesh
                newMesh.SetUVs(_uvChannel, _tmpUVsForCropping);

                // Clear UV cache
                setUVsWorkingCopy(_selectedObject, _uvChannel, _tmpUVsForCropping);
                //TriangleCache.BuildUVCache(getSelectedComponent(), newMesh);
            }

            // Texture
            {
                // Make texture readable
                var info = TextureUtils.MakeTextureAssetReadableAndUncompressed(_texture);

                // Copy pixels from texture.
                var pixelRect = uvToPixelRect(_cropRectInUVs, _texture);
                var colors = _texture.GetPixels(pixelRect.min.x, pixelRect.min.y, pixelRect.width, pixelRect.height);

                var texturePath = AssetDatabase.GetAssetPath(_texture);
                if (texturePath != null)
                {
                    // Create new texture
                    var croppedTexture = new Texture2D(pixelRect.width, pixelRect.height, TextureFormat.RGB24, mipChain: false);
                    croppedTexture.SetPixels(colors);

                    // Save to file
                    TextureUtils.SaveTextureAsPNG(croppedTexture, texturePath);
                    AssetDatabase.ImportAsset(texturePath);
                }

                // Revert any changes made to the texture importer (revert readability),
                TextureUtils.ApplyTextureImporterInfo(_texture, info);
            }

            UndoStack.Instance.StartEntry();
            var currentMesh = GetSharedMeshFromGameObject(SelectedGameObject);
            UndoStack.Instance.AddRedoAction(undoAssignMeshOnGameObjectFunc(SelectedGameObject, currentMesh));
            UndoStack.Instance.AddRedoAction(undoSetTextureAndMaterialsOnGameObjectFunc(SelectedGameObject, _texture, _textureMaterial));
            _tmpUVsForCropping.Clear();
            currentMesh.GetUVs(_uvChannel, _tmpUVsForCropping);
            UndoStack.Instance.AddRedoAction(undoSetUVsOnGameObjectFunc(SelectedGameObject, _tmpUVsForCropping, _uvChannel));
            UndoStack.Instance.AddRedoAction(() => clearMeshCache());
            UndoStack.Instance.EndEntry(); 

            UndoStack.Instance.EndGroup();
        }
    }
}