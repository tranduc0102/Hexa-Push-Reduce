using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kamgam.UVEditor
{
    public class LineDrawer : VisualElement
    {
        public struct SimpleVertex
        {
            public Vector2 Pos;
            public Color Tint;

            public SimpleVertex(Vector2 pos, Color tint) : this(pos)
            {
                Tint = tint;
            }

            public SimpleVertex(Vector2 pos) : this()
            {
                Pos = pos;
                Tint = Color.white;
            }

            public static implicit operator Vector2(SimpleVertex sv) => sv.Pos;
            public static implicit operator Vector3(SimpleVertex sv) => (Vector3) sv.Pos;
            public static implicit operator SimpleVertex(Vector2 v) => new SimpleVertex(v);
            public static implicit operator SimpleVertex(Vector3 v) => new SimpleVertex((Vector2)v);
        }

        /// <summary>
        /// NOTICE: If enabled then you also have to REVERSE the winding order of the vertices.
        /// </summary>
        public bool InvertVertical;
        public List<SimpleVertex> Vertices = new List<SimpleVertex>();
        /// <summary>
        /// The line width in pixels.
        /// </summary>
        public float LineWidth = 2f;
        public Color LineColor = new Color(1f, 1f, 1f, 1f);

        // If null then a new line is started after two new vertices have been added.
        public Vector2? _startVertex = null;

        // Cache of the last vertex because we need two points to draw a line ;)
        public Vector2? _previousVertex = null;

        private int _numOfVertices;
        public int NumOfVertices => _numOfVertices;
        public bool NextLinesCanBeDrawn(int numOfLinesToDraw = 1)
        {
            return _numOfVertices + numOfLinesToDraw * 6 <= MAX_NUM_OF_VERTICES;
        }

        public const int MAX_NUM_OF_VERTICES = 65535;

        public LineDrawer()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        public void ClearVertices()
        {
            Vertices.Clear();
            _numOfVertices = 0;
            _previousVertex = null;
            _startVertex = null;
        }

        public void StartNewLine()
        {
            _startVertex = null;
            _previousVertex = null;
        }

        /// <summary>
        /// Extends an existing line or starts a new one of none existed.<br />
        /// Add vertices in CCW order - or - if InvertVertical is TRUE then CW order.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void AddVertex(float x, float y)
        {
            AddVertex(new Vector2(x, y));
        }

        public void AddVertex(float x, float y, Color color)
        {
            AddVertex(new Vector2(x, y), color);
        }

        public void AddVertex(Vector2 vertex, bool closeLoop = false)
        {
            AddVertex(vertex, LineColor, closeLoop);
        }

        /// <summary>
        /// Extends an existing line or starts a new one of none existed.<br />
        /// Add vertices in CCW order - or - if InvertVertical is TRUE then CW order.
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="color"></param>
        /// <param name="closeLoop"></param>
        /// <returns>Whether or not the vertex has been added successfully.</returns>
        public void AddVertex(Vector2 vertex, Color color, bool closeLoop = false)
        {
            // Invert y-axis?
            float height = style.height.value.value;
            if (InvertVertical && !Mathf.Approximately(height, 0f))
            {
                vertex = new Vector2(vertex.x, height - vertex.y);
            }

            // Check if we have a previous point, if yes then draw a line.
            // If not then remember it and wait for the next point.
            if (_previousVertex.HasValue)
            {
                // Draw a line consiting of two triangles.
                if (InvertVertical)
                {
                    drawLineCW(_previousVertex.Value, vertex, color);
                }
                else
                {
                    drawLineCCW(_previousVertex.Value, vertex, color);
                }
            }

            _previousVertex = vertex;

            if (closeLoop && _startVertex.HasValue)
            {
                if (InvertVertical)
                {
                    drawLineCW(_previousVertex.Value, _startVertex.Value, color);
                }
                else
                {
                    drawLineCCW(_previousVertex.Value, _startVertex.Value, color);
                }
            }

            if (!_startVertex.HasValue)
                _startVertex = vertex;
        }

        private void drawLineCCW(Vector2 from, Vector2 to)
        {
            drawLineCCW(from, to, LineColor);
        }

        private void drawLineCCW(Vector2 from, Vector2 to, Color color)
        {
            // Visual elements can not have more than 65k vertices.
            if (_numOfVertices + 6 >= MAX_NUM_OF_VERTICES)
                throw new System.Exception("LineDrawer can not draw " + _numOfVertices + " because a VisualElement must not allocate more than 65535 vertices.");

            var vector = to - from;
            var normal = new Vector2(vector.y, -vector.x).normalized;

            // tri 1
            Vertices.Add(new SimpleVertex(from - normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(to + normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(from + normal * LineWidth * 0.5f, color));

            // tri 2
            Vertices.Add(new SimpleVertex(from - normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(to - normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(to + normal * LineWidth * 0.5f, color));

            _numOfVertices += 6;
        }

        private void drawLineCW(Vector2 from, Vector2 to)
        {
            drawLineCW(from, to, LineColor);
        }

        private void drawLineCW(Vector2 from, Vector2 to, Color color)
        {
            // Visual elements can not have more than 65k vertices.
            if (_numOfVertices + 6 >= MAX_NUM_OF_VERTICES)
                throw new System.Exception("LineDrawer can not draw " + (_numOfVertices + 6) + " because a VisualElement must not allocate more than 65535 vertices.");

            var vector = to - from;
            var normal = new Vector2(vector.y, -vector.x).normalized;

            // tri 1
            Vertices.Add(new SimpleVertex(from + normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(to + normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(from - normal * LineWidth * 0.5f, color));

            // tri 2
            Vertices.Add(new SimpleVertex(to + normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(to - normal * LineWidth * 0.5f, color));
            Vertices.Add(new SimpleVertex(from - normal * LineWidth * 0.5f, color));

            _numOfVertices += 6;
        }

        // Method called to generate the custom visual content (i.e., the filled triangle)
        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (_numOfVertices > MAX_NUM_OF_VERTICES)
                throw new System.Exception("LineDrawer can not generate visual content for " + _numOfVertices + " vertices because a VisualElement must not allocate more than 65535 vertices.");

            if (Vertices.Count == 0 || Vector3.SqrMagnitude(Vertices[0].Pos - Vertices[1].Pos) + Vector3.SqrMagnitude(Vertices[0].Pos - Vertices[1].Pos) < 0.001f)
                return;

            // TODO: Once Unity 2021 support can be dropped use the mgc.painter2D (available in 2022.1+) instead of creating the vertices ourselves.
            // see: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/UIElements.MeshGenerationContext-painter2D.html
            //      https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/UIElements/Core/Renderer/UIRPainter2D.cs#L104

            // Create a new MeshWriteData to draw the triangle
            var mesh = mgc.Allocate(Vertices.Count, Vertices.Count);

            ushort index = 0;
            foreach (var v in Vertices)
            {
                mesh.SetNextVertex(new Vertex { position = v.Pos, tint = v.Tint });    
                mesh.SetNextIndex(index);
                index++;
            }
        }

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<TriangleDrawer, UxmlTraits> { }
#endif
    }
}
