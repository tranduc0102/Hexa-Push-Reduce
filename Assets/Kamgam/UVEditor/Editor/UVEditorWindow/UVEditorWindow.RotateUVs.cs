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
        [System.NonSerialized] protected Vector2 _rotationCenterUV;
        [System.NonSerialized] protected Vector2 _rotationLastMouseUVPos;

        private void startRotatingUVs(int cornerHandle, MouseDownEvent evt)
        {
            // Rotating is not supported in texture edit mode.
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
            _rotationCenterUV = _gizmoScaleRectBL.max + (_gizmoScaleRectTR.min - _gizmoScaleRectBL.max) * 0.5f;
            _rotationLastMouseUVPos = _uvMousePos;

            if (_applyChangesImmediately)
                applyUVChangesToSelectedObjectMesh(recordUndo: true, _uvChannel);
        }

        private void rotateUVs(int rotationHandle, MouseMoveEvent evt)
        {
            // Update cursor
            _cursorTargetElement.style.cursor = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.RotateArrow);
            _nextCursorOnMouseUp = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.Arrow);

            // Rotate
            var uvMouseDelta = new Vector2(
                evt.mouseDelta.x / _textureContainer.resolvedStyle.width,
                -evt.mouseDelta.y / _textureContainer.resolvedStyle.height
            );
            var centerToMouseOld = _rotationLastMouseUVPos - _rotationCenterUV;
            _rotationLastMouseUVPos += uvMouseDelta;
            var centerToMouseNew = _rotationLastMouseUVPos - _rotationCenterUV;
            var angleInRad = Vector2.SignedAngle(centerToMouseOld, centerToMouseNew) * Mathf.Deg2Rad;

            // Calculate cosine and sine of the angle
            float cosAngle = Mathf.Cos(angleInRad);
            float sinAngle = Mathf.Sin(angleInRad);

            if (!_selectedVertices.IsNullOrEmpty())
            {
                var uvs = getUVWorkingCopyForSelection(_uvChannel);

                foreach (var v in _selectedVertices)
                {
                    uvs[v] = applyRotationToUV(cosAngle, sinAngle, uvs[v], _rotationCenterUV);
                }

                if (_applyChangesImmediately)
                    applyUVChangesToSelectedObjectMesh(recordUndo: false, _uvChannel);

                // Update since UVs have changed.
                updateSelectionRect();
            }

            _uvsAreDirty = true;
        }

        private Vector2 applyRotationToUV(float cosAngle, float sinAngle, Vector2 uv, Vector2 center)
        {
            // Translate vertex to the origin (relative to center)
            Vector2 translatedUVs = uv - center;

            // Apply 2D rotation matrix
            Vector2 rotatedUVs = new Vector2(
                translatedUVs.x * cosAngle - translatedUVs.y * sinAngle,
                translatedUVs.x * sinAngle + translatedUVs.y * cosAngle
            );

            // Translate the rotated point back to original position relative to the center
            return rotatedUVs + center;
        }

        private void stopRotatingUVs(int cornerHandle)
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