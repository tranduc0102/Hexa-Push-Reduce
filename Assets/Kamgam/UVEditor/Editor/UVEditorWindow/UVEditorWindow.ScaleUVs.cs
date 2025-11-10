using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Kamgam.UVEditor;
using System;
using System.Collections.Generic;
using System.IO;


namespace Kamgam.UVEditor
{
    public partial class UVEditorWindow : EditorWindow
    {
        [System.NonSerialized] protected Vector2 _scaleCenterUV;
        [System.NonSerialized] protected Vector2 _scaleLastMouseUVPos;

        private void startScalingUVs(int cornerHandle, MouseDownEvent evt)
        {
            // Scaling is not supported in texture edit mode.
            if (_editTextureEnabled)
                return;

            // Start undo group, we will end it after the rotaion has stopped.
            UndoStack.Instance.StartGroup();

            UndoStack.Instance.StartEntry();
            UndoStack.Instance.AddUndoAction(undoSetUVsWorkingCopyFunc());
            UndoStack.Instance.EndEntry();

            // Clear control to avoid "Should not be capturing when there is a hotcontrol" errors.
            GUI.FocusControl(null);

            // Memorize starting conditions
            if (cornerHandle == 1)
                _scaleCenterUV = _selRectInUVs.max; // for bottom/left the scale center is top/right, ..
            else if (cornerHandle == 2)
                _scaleCenterUV = _selRectInUVs.min + new Vector2(_selRectInUVs.width, 0f);
            else if (cornerHandle == 3)
                _scaleCenterUV = _selRectInUVs.min;
            else // if (cornerHandle == 4)
                _scaleCenterUV = _selRectInUVs.min + new Vector2(0f, _selRectInUVs.height);

            _scaleLastMouseUVPos = _uvMousePos;

            if (_applyChangesImmediately)
                applyUVChangesToSelectedObjectMesh(recordUndo: true, _uvChannel);
        }

        private void scaleUVs(int cornerHandle, MouseMoveEvent evt)
        {
            // Update cursor
            if (cornerHandle == 1 || cornerHandle == 3) _cursorTargetElement.style.cursor = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.ResizeUpRight);
            if (cornerHandle == 2 || cornerHandle == 4) _cursorTargetElement.style.cursor = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.ResizeUpLeft);
            _nextCursorOnMouseUp = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.Arrow);

            // Scale
            var uvMouseDelta = new Vector2(
                        evt.mouseDelta.x / _textureContainer.resolvedStyle.width,
                        -evt.mouseDelta.y / _textureContainer.resolvedStyle.height
                        );
            var centerToMouseOld = _scaleLastMouseUVPos - _scaleCenterUV;
            _scaleLastMouseUVPos += uvMouseDelta;
            var centerToMouseNew = _scaleLastMouseUVPos - _scaleCenterUV;

            // Keep aspect ratio if shift is pressed.
            Vector2 scale;
            if (evt.shiftKey)
            {
                float aspectUV = _selRectInUVs.width / _selRectInUVs.height;
                float aspectMove = Mathf.Abs(uvMouseDelta.x / uvMouseDelta.y);
                
                // TODO: Revisit this part to improve (especially the *0.7 below).
                var tmpScaleLastMouseUVPos = _scaleLastMouseUVPos - uvMouseDelta;
                if (aspectUV < aspectMove)
                {
                    uvMouseDelta = new Vector2(uvMouseDelta.x, -uvMouseDelta.x / aspectUV);
                }
                else
                {
                    uvMouseDelta = new Vector2(uvMouseDelta.y, uvMouseDelta.y / aspectUV);
                }
                tmpScaleLastMouseUVPos += uvMouseDelta * 0.7f;
                centerToMouseNew = tmpScaleLastMouseUVPos - _scaleCenterUV;

                if (aspectUV < aspectMove)
                {
                    // UV selection is less wide than the mouse move
                    scale = new Vector2(
                        centerToMouseNew.x / centerToMouseOld.x,
                        centerToMouseNew.x / centerToMouseOld.x);
                }
                else
                {
                    scale = new Vector2(
                        centerToMouseNew.y / centerToMouseOld.y,
                        centerToMouseNew.y / centerToMouseOld.y);
                }
            }
            else
            {
                // Calculate x and y scale separately
                scale = new Vector2(
                    centerToMouseNew.x / centerToMouseOld.x,
                    centerToMouseNew.y / centerToMouseOld.y);
            }

            if (!_selectedVertices.IsNullOrEmpty())
            {
                var uvs = getUVWorkingCopyForSelection(_uvChannel);
                foreach (var v in _selectedVertices)
                {
                    uvs[v] = _scaleCenterUV + (uvs[v] - _scaleCenterUV) * scale;
                }

                if (_applyChangesImmediately)
                    applyUVChangesToSelectedObjectMesh(recordUndo: false, _uvChannel);

                // Update since UVs have changed.
                updateSelectionRect();
            }

            _uvsAreDirty = true;
        }

        private void stopScalingUVs(int cornerHandle)
        {
            UndoStack.Instance.StartEntry();
            UndoStack.Instance.AddRedoAction(undoSetUVsWorkingCopyFunc());
            UndoStack.Instance.EndEntry();

            if (_applyChangesImmediately)
                applyUVChangesToSelectedObjectMesh(recordUndo: true, _uvChannel);

            UndoStack.Instance.EndGroup();

            _uvsAreDirty = true;
        }
    }
}