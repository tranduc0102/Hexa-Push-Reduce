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
        protected int _tmpCopySourceUVChannel = -1;
        protected List<Vector2> _tmpCopySourceUVs = new List<Vector2>();
        protected List<int> _tmpSelectedCopySourceUVIndices = new List<int>();

        public void CopyUVs(int uvChannel)
        {
            _tmpCopySourceUVChannel = uvChannel;

            _tmpCopySourceUVs.Clear();
            var uvs = getUVWorkingCopyForSelection(uvChannel);
            _tmpCopySourceUVs.AddRange(uvs);

            _tmpSelectedCopySourceUVIndices.Clear();
            for (int i = 0; i < _selectedVertices.Count; i++)
            {
                _tmpSelectedCopySourceUVIndices.Add(_selectedVertices[i]);
            }

            Logger.LogMessage($"Copied {_tmpSelectedCopySourceUVIndices.Count} UVs from channel {uvChannel}.");
        }

        public void PasteUVs(int uvChannel)
        {
            // Cancel paste if no copy source exists or if target is the same as source.
            if (_tmpCopySourceUVChannel < 0 || _tmpCopySourceUVChannel == uvChannel)
            {
                Logger.LogMessage($"Cancelled UV paste because either no copy source exists or target channel is the same as source channel.");
                return;
            }

            if (_tmpCopySourceUVs.Count == 0 || _tmpSelectedCopySourceUVIndices.Count == 0)
                return;

            var uvs = getUVWorkingCopyForSelection(uvChannel);
            if (_tmpCopySourceUVs.Count != uvs.Count && uvs.Count > 0)
            {
                Logger.LogMessage("Aborting paste because the target UV channel count does not match source count.");
                return;
            }

            while(uvs.Count < _tmpCopySourceUVs.Count)
            {
                uvs.Add(Vector2.zero);
            }

            bool pasteAll = EditorUtility.DisplayDialog("Paste all or selected only?", $"Paste all the UVs from channel{_tmpCopySourceUVChannel} or only the selected.", "All", "Selected only");

            if (pasteAll)
            {
                uvs.Clear();
                uvs.AddRange(_tmpCopySourceUVs);
            }
            else
            {
                foreach (var index in _tmpSelectedCopySourceUVIndices)
                {
                    uvs[index] = _tmpCopySourceUVs[index];
                }
            }

            //var uvA = getUVWorkingCopyForSelection(_tmpCopySourceUVChannel);
            //var uvB = getUVWorkingCopyForSelection(uvChannel);
            //for (int i = 0; i < uvA.Count; i++)
            //{
            //    if (uvB[i] != Vector2.zero)
            //        Debug.Log(uvA[i] + " == " + uvB[i]);
            //}

            bool apply = _applyChangesImmediately;
            if (!apply)
            {
                apply = EditorUtility.DisplayDialog("Apply pasted UVs?", "Would you like to apply (save) the paste UVs?\n\nIf not then you will have to apply/save them manually.", "Yes", "No");
            }

            if(apply)
            {
                applyUVChangesToSelectedObjectMesh(recordUndo: true, uvChannel);
            }

            _uvsAreDirty = true;
            ClearCacheAndSelection();

            Logger.LogMessage($"Pasted {_tmpSelectedCopySourceUVIndices.Count} UVs to channel {uvChannel}.");
        }
    }
}