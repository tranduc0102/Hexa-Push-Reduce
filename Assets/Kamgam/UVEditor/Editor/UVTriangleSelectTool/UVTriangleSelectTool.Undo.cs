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
    partial class UVTriangleSelectTool
    {
        public void Undo()
        {
            UndoStack.Instance.Undo();
        }

        public void Redo()
        {
            UndoStack.Instance.Redo();
        }

        private System.Action undoSetSelectedTrianglesFunc(HashSet<SelectedTriangle> selectedTriangles)
        {
            // Make a copy
            var snapshot = new SelectedTriangleSnapshot("Undo Tris", selectedTriangles);

            // Apply
            return () => {
                LoadSelection(snapshot, additive: false);
                UVEditor?.OnSelectedTrianglesChanged(_selectedTriangles);
            };
        }
    }
}
