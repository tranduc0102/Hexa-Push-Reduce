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
        /// <summary>
        /// Tries to select all triangles which are connected to the given selected triangle. However if the mesh has a lot of tris it will
        /// stop at some point because the time needed grows exponentially.
        /// </summary>
        /// <param name="mesh">The mesh to operate on.</param>
        /// <param name="limitToSubMesh">Enable to limit selection to the sub mesh which the given triangle is part of.</param>
        /// <param name="selectedSubMeshIndex">The selected sub mesh index (used only if limitToSubMesh is TRUE).</param>
        /// <param name="maxDuration">Select linked may take a long time for highpoly meshes. This limits the time it is allowed to take (in seconds).</param>
        /// <param name="showFailedPopup">If disabled then a log message will be used instead.</param>
        /// <param name="compareConnectedUVs">Enable to limit selection to do the link check in UV space instead of 3D space.</param>
        /// <param name="uvChannel">The UV channel index to use for comparison.</param>
        /// <returns></returns>
        public static List<int> AddLinked(
            List<int> selectedVerticesToAddLinkedTo,
            Mesh mesh,
            bool limitToSubMesh,
            int selectedSubMeshIndex,
            int maxDuration,
            bool showFailedPopup = true,
            bool compareConnectedUVs = false,
            int uvChannel = 0
            )
        {
            EditorUtility.DisplayProgressBar("Working", "Gathering linked triangles ..", 0.1f);
            try
            {
                var allConnectedVertices = new List<int>();
                var lastConnectedVertices = new List<int>(selectedVerticesToAddLinkedTo);
                var newConnectedVertices = new List<int>();
                    
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                long maxDurationInMilliSec = maxDuration * 1000;
                bool completed = false;
                while (!completed)
                {
                    newConnectedVertices.Clear();
                    int count = lastConnectedVertices.Count;
                    for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                    {
                        if (limitToSubMesh && selectedSubMeshIndex != subMeshIndex)
                            continue;

                        var tris = mesh.GetTriangles(subMeshIndex);
                        var vertices = mesh.vertices;
                        List<Vector2> uvs = new List<Vector2>();
                        mesh.GetUVs(uvChannel, uvs);

                        // For each submesh
                        if (watch.ElapsedMilliseconds > maxDurationInMilliSec)
                            break;

                        int triCount = tris.Length;
                        for (int i = 0; i < triCount; i += 3)
                        {
                            var index0 = tris[i];
                            var index1 = tris[i + 1];
                            var index2 = tris[i + 2];
                            var vertex0 = vertices[index0];
                            var vertex1 = vertices[index1];
                            var vertex2 = vertices[index2];

                            if (i % 3000 == 0 && watch.ElapsedMilliseconds > maxDurationInMilliSec)
                                break;

                            for (int v = 0; v < count; v++)
                            {
                                bool vertexCompare = false;
                                bool uvCompareResult = false;
                                if (compareConnectedUVs)
                                {
                                    // Do any of the vertices share UV locations?
                                    uvCompareResult =  v2Equal(uvs[index0], uvs[lastConnectedVertices[v]])
                                                    || v2Equal(uvs[index1], uvs[lastConnectedVertices[v]])
                                                    || v2Equal(uvs[index2], uvs[lastConnectedVertices[v]]);
                                }
                                else
                                {
                                    // Do any of the vertices share positions?
                                    // Sadly we can not only rely only on index checks since split vertices will have the same position but different indices.
                                    vertexCompare =    index0 == lastConnectedVertices[v]
                                                    || index1 == lastConnectedVertices[v]
                                                    || index2 == lastConnectedVertices[v]
                                                    || v3Equal(vertex0, vertices[lastConnectedVertices[v]])
                                                    || v3Equal(vertex1, vertices[lastConnectedVertices[v]])
                                                    || v3Equal(vertex2, vertices[lastConnectedVertices[v]]);
                                }

                                if (vertexCompare || uvCompareResult)
                                {
                                    // Add results
                                    selectedVerticesToAddLinkedTo.AddIfNotContained(index0);
                                    selectedVerticesToAddLinkedTo.AddIfNotContained(index1);
                                    selectedVerticesToAddLinkedTo.AddIfNotContained(index2);

                                    // update vertices
                                    if (!allConnectedVertices.Contains(index0))
                                    {
                                        allConnectedVertices.Add(index0);
                                        newConnectedVertices.Add(index0);
                                    }
                                    if (!allConnectedVertices.Contains(index1))
                                    {
                                        allConnectedVertices.Add(index1);
                                        newConnectedVertices.Add(index1);
                                    }
                                    if (!allConnectedVertices.Contains(index2))
                                    {
                                        allConnectedVertices.Add(index2);
                                        newConnectedVertices.Add(index2);
                                    }
                                }
                            }
                        }
                    }
                    if (newConnectedVertices.Count == 0)
                    {
                        completed = true;
                    }

                    lastConnectedVertices.Clear();
                    lastConnectedVertices.AddRange(newConnectedVertices);
                }

                if (watch.ElapsedMilliseconds > maxDurationInMilliSec)
                {
                    EditorApplication.delayCall += () =>
                    {
                        string msg = "Sorry, the mesh is too complex for linked polygon search (too many triangles)." +
                            "\n\nThe selection process has been aborted." +
                            "\n\nHINT: You can increase the allowed duration in the settings (Max Select Linked Duration).";
                        if (showFailedPopup)
                        {
                            EditorUtility.DisplayDialog(
                                "The mesh is too complex!", msg
                                , "OK");
                        }
                        else
                        {
                            Debug.LogWarning(msg);
                        }
                    };
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return selectedVerticesToAddLinkedTo;
        }

        static bool v2Equal(Vector2 a, Vector2 b)
        {
            return Vector2.SqrMagnitude(a - b) < 0.0000001f;
        }

        static bool v3Equal(Vector3 a, Vector3 b)
        {
            return Vector3.SqrMagnitude(a - b) < 0.0000001f;
        }
    }
}