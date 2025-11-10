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
        // TEXTURE EDITING

        [System.NonSerialized] protected Texture2D _textureEditSliceForUI;
        [System.NonSerialized] protected Rect _textureEditSrcRectInUVs;
        [System.NonSerialized] protected RectInt _textureEditSrcRectInPixels;
        [System.NonSerialized] protected Vector2 _textureEditTotalUVDelta;

        private void internalStartTextureEditing()
        {
            startNewTextureEditCycle(recordUndo: true);
        }

        private void startNewTextureEditCycle(bool recordUndo)
        {
            if (_texture == null)
                return;

            // Record UNDO for texture and material assignment AND texture pixel information.
            if (recordUndo)
            {
                UndoStack.Instance.StartGroup();

                UndoStack.Instance.StartEntry();
                UndoStack.Instance.AddUndoAction(() => clearMeshCache());
                UndoStack.Instance.AddUndoAction(undoSetUVsOnGameObjectFunc(SelectedGameObject, _tmpUVsForCropping, _uvChannel));
                UndoStack.Instance.AddUndoAction(undoAssignMeshOnGameObjectFunc(SelectedGameObject, GetSharedMeshFromGameObject(SelectedGameObject)));
                UndoStack.Instance.AddUndoAction(undoSetTextureAndMaterialsOnGameObjectFunc(SelectedGameObject, _texture, _textureMaterial));
                UndoStack.Instance.EndEntry();
            }

            // Create new texture and material if necessary
            _texture = getNewTexture(_selectedObject, _selectedSubMeshIndex, _texture, recordUndo);
            _textureMaterial = getNewMaterial(_selectedObject, _selectedSubMeshIndex, _textureMaterial, recordUndo);
            _textureMaterial.mainTexture = _texture;

            // Assign new material(s)
            // We assign the new material to all material slots that have the same material.

            var renderer = GetRendererFromGameObject(SelectedGameObject);
            var tmpMaterials = renderer.sharedMaterials;
            var currentMaterial = tmpMaterials[_selectedSubMeshIndex];
            for (int i = 0; i < tmpMaterials.Length; i++)
            {
                if (tmpMaterials[i] == currentMaterial)
                {
                    tmpMaterials[i] = _textureMaterial;
                }
            }
            renderer.sharedMaterials = tmpMaterials;
            EditorUtility.SetDirty(renderer);

            updateTextureEditSelection();

            if (recordUndo)
            {
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

        private void updateTextureEditSelection()
        {
            if (_texture == null)
                return;

            // Copy sel rect which was derived from the selected vertices earlier.
            _textureEditSrcRectInUVs = _selRectInUVs;

            // Add margin to UVs
            float uvMarginX = _textureEditMargin / (float)_texture.width;
            float uvMarginY = _textureEditMargin / (float)_texture.height;
            _textureEditSrcRectInUVs = addMarignToRect(_textureEditSrcRectInUVs, uvMarginX, uvMarginY);
            // Convert to pixles
            _textureEditSrcRectInPixels = uvToPixelRect(_textureEditSrcRectInUVs, _texture);

            // Check each pixel in the rect on whether or not it is inside a selected triangle (not done yet) and
            // cache that result relative to the rect (these do not change and its probably a heavy process).
            // NOTICE: We also check if the pixel is out of bounds, if yes it's also masked. This avoids out
            // of bounds errors while copying pixels later.
            _tmpCopyMaskSize = _textureEditSrcRectInPixels.width * _textureEditSrcRectInPixels.height;
            // Allocate new array if necessary.
            if (_tmpCopyMask.Length < _tmpCopyMaskSize)
            {
                _tmpCopyMask = new byte[_tmpCopyMaskSize];
            }
            // Generate mask
            int xMin = _textureEditSrcRectInPixels.min.x;
            int yMin = _textureEditSrcRectInPixels.min.y;
            int xMax = _textureEditSrcRectInPixels.max.x;
            int yMax = _textureEditSrcRectInPixels.max.y;
            int width = _texture.width;
            int height = _texture.height;
            int index = 0;
            for (int y = yMin; y < yMax; y++)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    _tmpCopyMask[index] = 0;

                    // Check if in bounds
                    if (x >= 0 && y >= 0 && x < width && y < height)
                    {
                        // TODO/n2h: It would be nice if we cold check whether or not each pixel is inside any selected triangle
                        //           and only if they are set the mask to 1.
                        _tmpCopyMask[index] = 1;
                    }

                    index++;
                }
            }

            updateTextureSliceForUI();
        }

        private bool isInsideSelection()
        {
            return true;
        }

        private void updateTextureSliceForUI()
        {
            // Make texture readable
            var info = TextureUtils.MakeTextureAssetReadableAndUncompressed(_texture);

            // Update slice texture in UI windows for preview.
            var colors = GetPixelsMasked(_texture, _textureEditSrcRectInPixels.x, _textureEditSrcRectInPixels.y, _textureEditSrcRectInPixels.width, _textureEditSrcRectInPixels.height, _tmpCopyMask, new Color(0f, 0f, 0f, 0f));
            _textureEditSliceForUI = new Texture2D(_textureEditSrcRectInPixels.width, _textureEditSrcRectInPixels.height, TextureFormat.RGBA32, mipChain: false);
            //_textureEditSliceForUI.SetPixels(0, 0, _textureEditSliceForUI.width, _textureEditSliceForUI.height, colors); // <- We could use this but to remain future proof we use masked.
            SetPixelsMasked(_textureEditSliceForUI, 0, 0, _textureEditSliceForUI.width, _textureEditSliceForUI.height, colors, _tmpCopyMask, new Color(0f,0f,0f,0f), setEmpty: true);
            _textureEditSliceForUI.Apply();

            // Revert readability of texture.
            TextureUtils.ApplyTextureImporterInfo(_texture, info);
        }

        /// <summary>
        /// Gets the pixels based on the mask array. For pixels outside the texture bounds or unmasked the emptyColor will be copied.
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="xMin"></param>
        /// <param name="yMin"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="mask"></param>
        /// <param name="emptyColor"></param>
        /// <returns></returns>
        private Color[] GetPixelsMasked(Texture2D texture, int xMin, int yMin, int width, int height, byte[] mask, Color emptyColor)
        {
            var colors = new Color[width * height];

            int tWidth = texture.width;
            int tHeight = texture.width;

            int xMax = xMin + width;
            int yMax = yMin + height;

            int index = 0;
            for (int y = yMin; y < yMax; y++)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    if (_tmpCopyMask[index] == 1 && x >= 0 && x < tWidth && y >= 0 && y < tHeight)
                    {
                        colors[index] = texture.GetPixel(x, y);
                    }
                    else
                    {
                        colors[index] = emptyColor;
                    }
                    index++;
                }
            }

            return colors;
        }

        /// <summary>
        /// Sets the pixels based on the mask array
        /// For unmasked pixels the emptyColor will be copied only if setEmpty is enabled.
        /// If setEmpty is not enabled then unmasked pixels will be left unchanged.
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="xMin"></param>
        /// <param name="yMin"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="colors"></param>
        /// <param name="mask"></param>
        /// <param name="emptyColor"></param>
        /// <param name="setEmpty"></param>
        private void SetPixelsMasked(Texture2D texture, int xMin, int yMin, int width, int height, Color[] colors, byte[] mask, Color emptyColor, bool setEmpty)
        {
            int tWidth = texture.width;
            int tHeight = texture.height;

            int xMax = xMin + width;
            int yMax = yMin + height;

            int index = 0;
            for (int y = yMin; y < yMax; y++)
            {
                if (y < 0 || y >= tHeight)
                    continue;

                for (int x = xMin; x < xMax; x++)
                {
                    if (x < 0 || x >= tWidth)
                        continue;

                    if (_tmpCopyMask[index] == 1)
                    {
                        texture.SetPixel(x, y, colors[index]);
                    }
                    else
                    {
                        if (setEmpty)
                            texture.SetPixel(x, y, emptyColor); // < currently not used since we copy the whole rect all the time.
                    }
                    index++;
                }
            }
        }

        protected void internalStopTextureEditing()
        {
            _textureEditSliceForUI = null;
        }

        protected void applyChangesToTexture(bool recordUndo)
        {
            // Skip if nothing to change (no movement)
            if (Mathf.Approximately(_textureEditTotalUVDelta.sqrMagnitude, 0f))
                return;

            if (recordUndo)
            {
                UndoStack.Instance.StartGroup();

                UndoStack.Instance.StartEntry();
                UndoStack.Instance.AddUndoAction(() => clearMeshCache());
                UndoStack.Instance.AddUndoAction(undoSetUVsOnGameObjectFunc(SelectedGameObject, _tmpUVsForCropping, _uvChannel));
                UndoStack.Instance.AddUndoAction(undoAssignMeshOnGameObjectFunc(SelectedGameObject, GetSharedMeshFromGameObject(SelectedGameObject)));
                UndoStack.Instance.AddUndoAction(undoSetTextureAndMaterialsOnGameObjectFunc(SelectedGameObject, _texture, _textureMaterial));
                UndoStack.Instance.EndEntry();
            }

            // Make texture readable
            var info = TextureUtils.MakeTextureAssetReadableAndUncompressed(_texture);

            var sourceRect = _textureEditSrcRectInPixels;

            // Convert uv rect to pixels
            var targetRectInUVs = _textureEditSrcRectInUVs;
            targetRectInUVs.position += _textureEditTotalUVDelta;
            var targetRect = uvToPixelRect(targetRectInUVs, _texture);
            // Ensure same dimensions in case of rounding errors.
            targetRect.width = sourceRect.width;
            targetRect.height = sourceRect.height;

            // Skip if no change in pixel coordinates
            if (sourceRect.min == targetRect.min && sourceRect.max == targetRect.max)
                return;

            // Copy pixels in _tmpEditTextureHandle from sourceRect to targetRect
            var pixels = GetPixelsMasked(_texture, sourceRect.min.x, sourceRect.min.y, sourceRect.width, sourceRect.height,
                                         _tmpCopyMask, new Color(0f,0f,0f,0f));
            // Set pixels only within masked area.
            SetPixelsMasked(_texture, targetRect.min.x, targetRect.min.y, targetRect.width, targetRect.height, pixels,
                            _tmpCopyMask, emptyColor: new Color(0f,0f,0f,0f), setEmpty: false);
            _texture.Apply(true);

            // Save to file
            TextureUtils.SaveTextureAsPNG(_texture);

            // Revert any changes made to the texture importer (revert readability),
            TextureUtils.ApplyTextureImporterInfo(_texture, info);

            // Start new editing cycle
            _textureEditTotalUVDelta = Vector2.zero;

            if (recordUndo)
            {
                UndoStack.Instance.StartEntry();
                var currentMesh = GetSharedMeshFromGameObject(SelectedGameObject);
                UndoStack.Instance.AddRedoAction(undoAssignMeshOnGameObjectFunc(SelectedGameObject, currentMesh));
                UndoStack.Instance.AddRedoAction(undoSetTextureAndMaterialsOnGameObjectFunc(SelectedGameObject, _texture, _textureMaterial));
                _tmpUVsForCropping.Clear();
                currentMesh.GetUVs(_uvChannel, _tmpUVsForCropping);
                UndoStack.Instance.AddRedoAction(undoSetUVsOnGameObjectFunc(SelectedGameObject, _tmpUVsForCropping, _uvChannel));
                UndoStack.Instance.AddRedoAction(() => clearMeshCache());
                UndoStack.Instance.AddRedoAction(() => updateTextureSliceForUI());
                UndoStack.Instance.EndEntry();

                UndoStack.Instance.EndGroup();
            }

            startNewTextureEditCycle(recordUndo: false);
        }

        protected void startMovingTexture(IMouseEvent evt)
        {
        }

        /// <summary>
        /// Notice the pixel coordinates are from the bottom left corner (because they are UV-based).
        /// This is aligned with the GetPixels32(..) layout, see:
        /// https://docs.unity3d.com/ScriptReference/Texture2D.GetPixels32.html
        /// </summary>
        /// <param name="uvRect"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        protected RectInt uvToPixelRect(Rect uvRect, Texture2D texture)
        {
            var pixelRect = new RectInt();
            pixelRect.min = new Vector2Int(Mathf.FloorToInt(uvRect.min.x * texture.width), Mathf.FloorToInt(uvRect.min.y * texture.height));
            pixelRect.max = new Vector2Int(Mathf.CeilToInt(uvRect.max.x * texture.width), Mathf.CeilToInt(uvRect.max.y * texture.height));
            return pixelRect;
        }

        protected Vector2 pixelToUV(float x, float y, Texture2D texture)
        {
            return new Vector2(
                x / (float) texture.width,
                y / (float) texture.height
            );
        }

        [System.NonSerialized] protected int _tmpCopyMaskSize = 0;
        // The list contains the 0/1 bytemask info row by row, starting at the bottom left of
        // the selected pixel rect.
        [System.NonSerialized] protected byte[] _tmpCopyMask = new byte[256 * 256];

        [System.NonSerialized] protected int _textureEditMargin = 2;

        protected void moveTexture(MouseMoveEvent evt, Vector2 uvMouseMoveDelta)
        {
            _textureEditTotalUVDelta += uvMouseMoveDelta;
        }

        private Rect addMarignToRect(Rect sourceRect, float margin, float maxWidth, float maxHeight, bool clamp = true)
        {
            return addMarignToRect(sourceRect, margin, margin, maxWidth, maxHeight, 0f, 0f, clamp);
        }

        /// <summary>
        /// Adds margins to rect without clamping.
        /// </summary>
        /// <param name="sourceRect"></param>
        /// <param name="marginX"></param>
        /// <param name="marginY"></param>
        /// <returns></returns>
        private Rect addMarignToRect(Rect sourceRect, float marginX, float marginY)
        {
            return addMarignToRect(sourceRect, marginX, marginY, clamp: false);
        }

        private Rect addMarignToRect(Rect sourceRect, float marginX, float marginY, float maxX = 1f, float maxY = 1f, float minX = 0f, float minY = 0f, bool clamp = true)
        {
            if (clamp)
            {
                sourceRect.min = new Vector2(
                                    Mathf.Clamp(sourceRect.min.x - marginX, minX, maxX),
                                    Mathf.Clamp(sourceRect.min.y - marginY, minY, maxY)
                                );
                sourceRect.max = new Vector2(
                                    Mathf.Clamp(sourceRect.max.x + marginX, minX, maxX),
                                    Mathf.Clamp(sourceRect.max.y + marginY, minY, maxY)
                                );
            }
            else
            {
                sourceRect.min = new Vector2(
                                    sourceRect.min.x - marginX,
                                    sourceRect.min.y - marginY
                                );
                sourceRect.max = new Vector2(
                                    sourceRect.max.x + marginX,
                                    sourceRect.max.y + marginY
                                );
            }

            return sourceRect;
        }

        protected void stopMovingTexture()
        {
        }
    }
}