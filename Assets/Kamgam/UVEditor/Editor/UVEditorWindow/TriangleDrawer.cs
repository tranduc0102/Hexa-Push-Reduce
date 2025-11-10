using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kamgam.UVEditor
{
    public class TriangleDrawer : VisualElement
    {
        public struct SimpleVertex
        {
            public Vector2 Pos;
            public Color Tint;

            public SimpleVertex(Vector2 pos, Color tint) : this(pos)
            {
                Tint = tint;
            }

            public SimpleVertex(float x, float y) : this(new Vector2(x, y))
            {
                Tint = Color.white;
            }

            public SimpleVertex(Vector2 pos) : this()
            {
                Pos = pos;
                Tint = Color.white;
            }

            public static implicit operator Vector2(SimpleVertex sv) => sv.Pos;
            public static implicit operator Vector3(SimpleVertex sv) => (Vector3)sv.Pos;
            public static implicit operator SimpleVertex(Vector2 v) => new SimpleVertex(v);
            public static implicit operator SimpleVertex(Vector3 v) => new SimpleVertex((Vector2)v);
        }

        /// <summary>
        /// NOTICE: If enabled then you also have to REVERSE the winding order of the vertices.
        /// </summary>
        public bool InvertVertical;
        public List<SimpleVertex> Vertices = new List<SimpleVertex>();
        public Color Tint = new Color(1f, 1f, 1f, 1f);

        public TriangleDrawer()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        public void ClearVertices()
        {
            Vertices.Clear();
        }

        public void AddVertex(float x, float y)
        {
            AddVertex(x, y, Tint);
        }

        /// <summary>
        /// Add vertices in CCW order - or - if InvertedVertical is TRUE then CW order.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void AddVertex(float x, float y, Color color)
        {
            AddVertex(new Vector2(x, y), color);
        }

        public void AddVertex(Vector2 vertex)
        {
            AddVertex(vertex, Tint);
        }

        /// <summary>
        /// Add vertices in CCW order - or - if InvertedVertical is TRUE then CW order.
        /// </summary>
        /// <param name="vertex"></param>
        public void AddVertex(Vector2 vertex, Color color)
        {
            float height = style.height.value.value;
            if (InvertVertical && !Mathf.Approximately(height, 0f))
            {
                var invertedVertex = new SimpleVertex(new Vector2(vertex.x, height - vertex.y), color);
                Vertices.Add(invertedVertex);
            }
            else
            {
                var v = new SimpleVertex(vertex, color);
                Vertices.Add(v);
            }
        }

        // Method called to generate the custom visual content (i.e., the filled triangle)
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (Vertices.Count == 0 || Vector3.SqrMagnitude(Vertices[0].Pos - Vertices[1].Pos) + Vector3.SqrMagnitude(Vertices[0].Pos - Vertices[1].Pos) < 0.001f)
                return;

            // Create a new MeshWriteData to draw the triangle
            var mesh = context.Allocate(Vertices.Count, Vertices.Count);

            ushort index = 0;
            foreach (var v in Vertices)
            {
                mesh.SetNextVertex(new Vertex { position = v, tint = v.Tint });    
                mesh.SetNextIndex(index);
                index++;
            }
        }

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<TriangleDrawer, UxmlTraits> { }
#endif
    }
}
