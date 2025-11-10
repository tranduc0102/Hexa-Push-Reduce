using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kamgam.UVEditor
{
    /// <summary>
    /// A serializable representation of all the data in a selected triangle.
    /// </summary>
    [System.Serializable]
    public class SelectedTriangleSnapshot : System.IEquatable<SelectedTriangleSnapshot>
    {
        public string Name;
        public long Timestamp; // Timestamp is not part of equal comparison.
        public List<ObjectSnapshot> ObjectSnapshots;

        public int TriangleCount
        {
            get 
            {
                if (ObjectSnapshots == null || ObjectSnapshots.Count == 0)
                    return 0;

                int sum = 0;
                for (int i = 0; i < ObjectSnapshots.Count; i++)
                {
                    sum += ObjectSnapshots[i].Triangles.Count;
                }

                return sum;
            }
            
        }

        [System.Serializable]
        public class ObjectSnapshot : System.IEquatable<ObjectSnapshot>
        {
            public string ObjectName;
            public int VertexCount;

            [HideInInspector]
            public List<SelectedTriangleData> Triangles;

            public ObjectSnapshot(Component comp, HashSet<SelectedTriangle> triangles)
            {
                if (comp == null)
                    return;

                ObjectName = comp.gameObject.name;

                var skinnedRenderer = comp as SkinnedMeshRenderer;
                if (skinnedRenderer != null)
                {
                    VertexCount = skinnedRenderer.sharedMesh.vertexCount;
                }
                else
                {
                    var meshFilter = comp as MeshFilter;
                    VertexCount = meshFilter.sharedMesh.vertexCount;
                }

                Triangles = new List<SelectedTriangleData>(triangles.Count);
                foreach (var tri in triangles)
                {
                    if (tri.Component != comp)
                        continue;

                    var triData = new SelectedTriangleData(tri);
                    Triangles.Add(triData);
                }
            }

            public bool Equals(ObjectSnapshot other)
            {
                return ObjectName == other.ObjectName
                    && VertexCount == other.VertexCount
                    && Triangles.HasEqualElements(other.Triangles);
            }
        }

        public SelectedTriangleSnapshot(string name, HashSet<SelectedTriangle> triangles)
        {
            Name = name;
            Timestamp = GetTimestamp();

            List<Component> components = new List<Component>();
            foreach (var tri in triangles)
            {
                if (!components.Contains(tri.Component))
                {
                    components.Add(tri.Component);
                }
            }

            ObjectSnapshots = new List<ObjectSnapshot>(components.Count);
            foreach (var comp in components)
            {
                var snapshot = new ObjectSnapshot(comp, triangles);
                ObjectSnapshots.Add(snapshot);
            }
        }

        static long GetTimestamp()
        {
            DateTime currentTime = DateTime.UtcNow;
            return ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
        }

        public bool Equals(SelectedTriangleSnapshot other)
        {
            return Name == other.Name
                && ObjectSnapshots.HasEqualElements(other.ObjectSnapshots);
        }
    }
}
