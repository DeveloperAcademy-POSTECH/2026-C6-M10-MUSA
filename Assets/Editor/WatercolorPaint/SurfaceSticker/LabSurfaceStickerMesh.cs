using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>
    /// Editor-only presentation geometry. Reads the saved body without enabling Read/Write,
    /// retains its actual triangles and adds a small normal offset. No collider or authority
    /// cell is created or modified. The material must mask the padded patch to paid coverage.
    /// </summary>
    public static class LabSurfaceStickerMesh
    {
        public readonly struct Projection
        {
            public readonly Vector3 Center, Right, Up;
            public readonly Rect Bounds;
            public Projection(Vector3 center, Vector3 right, Vector3 up, Rect bounds)
            {
                Center = center; Right = right; Up = up; Bounds = bounds;
            }

            /// <summary>Logarithmic spherical projection in BODY-local coordinates.</summary>
            public Vector2 Project(Vector3 position)
            {
                Vector2 p = Tangent(position.normalized, Center, Right, Up);
                return new Vector2((p.x - Bounds.xMin) / Bounds.width, (p.y - Bounds.yMin) / Bounds.height);
            }
        }

        public sealed class Result : IDisposable
        {
            public Mesh Mesh { get; internal set; }
            public int SourceVertexCount { get; internal set; }
            public int SourceTriangleCount { get; internal set; }
            public int SelectedTriangleCount { get; internal set; }
            public int CellsCount { get; internal set; }
            public float Offset { get; internal set; }
            public Projection Projection { get; internal set; }
            public void Dispose()
            {
                if (Mesh != null) Object.DestroyImmediate(Mesh);
                Mesh = null;
            }
        }

        /// <summary>
        /// Fit once to the complete intended footprint and reuse while revealing it. Calling
        /// Fit again for every growth frame would move the art UV and is deliberately not
        /// performed by Build. The caller owns the footprint/journal and freezes this frame.
        /// </summary>
        public static Projection Fit(LabPaintVisualStamp stamp, IReadOnlyList<int> paidCells,
            int columns, int rows, float paddingFraction = .04f)
        {
            ValidateCells(paidCells, columns, rows);
            if (!Finite(stamp.center) || !Finite(stamp.right) || stamp.center.sqrMagnitude < 1e-8f)
                throw new ArgumentException("A finite, nonzero stamp frame is required.", nameof(stamp));
            if (!float.IsFinite(paddingFraction) || paddingFraction < 0f || paddingFraction > .5f)
                throw new ArgumentOutOfRangeException(nameof(paddingFraction));
            Vector3 center = stamp.center.normalized;
            Vector3 right = stamp.right - center * Vector3.Dot(stamp.right, center);
            if (right.sqrMagnitude < 1e-8f)
                throw new ArgumentException("Stamp right must span the tangent plane.", nameof(stamp));
            right.Normalize();
            Vector3 up = Vector3.Cross(center, right).normalized;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (int cell in paidCells)
            {
                int x = cell % columns, y = cell / columns;
                // Include cell corners, not just centers: the art must reach the full paid
                // edge. Longitude wraps naturally when converted back to a direction.
                for (int corner = 0; corner < 4; corner++)
                {
                    float u = (x + (corner & 1)) / (float)columns;
                    float v = (y + (corner >> 1)) / (float)rows;
                    Vector2 p = Tangent(Direction(u, v), center, right, up);
                    minimum = Vector2.Min(minimum, p); maximum = Vector2.Max(maximum, p);
                }
            }
            Vector2 span = maximum - minimum;
            if (span.x < 1e-6f || span.y < 1e-6f)
                throw new InvalidOperationException("The footprint has a degenerate projection.");
            minimum -= span * paddingFraction; maximum += span * paddingFraction;
            return new Projection(center, right, up, Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y));
        }

        public static Result Build(Mesh source, IReadOnlyList<int> paidCells, int columns, int rows,
            Projection projection, float surfaceOffset = .0007f, int paddingCells = 1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateCells(paidCells, columns, rows);
            if (!float.IsFinite(surfaceOffset) || surfaceOffset < 0f)
                throw new ArgumentOutOfRangeException(nameof(surfaceOffset));
            if (paddingCells < 0 || paddingCells > 4) throw new ArgumentOutOfRangeException(nameof(paddingCells));
            if (!float.IsFinite(projection.Bounds.width) || !float.IsFinite(projection.Bounds.height)
                || projection.Bounds.width <= 0f || projection.Bounds.height <= 0f
                || !Finite(projection.Center) || projection.Center.sqrMagnitude < .5f)
                throw new ArgumentException("Create a valid frozen frame using Fit.", nameof(projection));

            bool[] nearby = new bool[checked(columns * rows)];
            var uniqueCells = new HashSet<int>();
            foreach (int cell in paidCells)
            {
                uniqueCells.Add(cell);
                int x = cell % columns, y = cell / columns;
                for (int dy = -paddingCells; dy <= paddingCells; dy++)
                    for (int dx = -paddingCells; dx <= paddingCells; dx++)
                    {
                        int cy = y + dy;
                        if (cy < 0 || cy >= rows) continue;
                        int cx = (x + dx + columns) % columns;
                        nearby[cy * columns + cx] = true;
                    }
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var authorityUv = new List<Vector2>();
            var triangles = new List<int>();
            int sourceCount, sourceTriangles = 0;

            // UnityEditor.MeshUtility supports editor inspection of non-readable meshes.
            // No importer flag, source vertex buffer or saved asset is changed.
            using (Mesh.MeshDataArray data = MeshUtility.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData input = data[0];
                sourceCount = input.vertexCount;
                using (var positions = new NativeArray<Vector3>(sourceCount, Allocator.Temp))
                using (var sourceNormals = new NativeArray<Vector3>(sourceCount, Allocator.Temp))
                {
                    input.GetVertices(positions);
                    bool hasNormals = input.HasVertexAttribute(VertexAttribute.Normal);
                    if (hasNormals) input.GetNormals(sourceNormals);
                    var mapped = new Dictionary<int, int>();
                    for (int submesh = 0; submesh < input.subMeshCount; submesh++)
                    {
                        SubMeshDescriptor part = input.GetSubMesh(submesh);
                        if (part.topology != MeshTopology.Triangles)
                            throw new ArgumentException("Only triangle body meshes are supported.", nameof(source));
                        sourceTriangles += part.indexCount / 3;
                        for (int i = 0; i < part.indexCount; i += 3)
                        {
                            int a = Index(input, part.indexStart + i) + part.baseVertex;
                            int b = Index(input, part.indexStart + i + 1) + part.baseVertex;
                            int c = Index(input, part.indexStart + i + 2) + part.baseVertex;
                            Vector3 pa = positions[a], pb = positions[b], pc = positions[c];
                            // Source triangles are retained; the saved body is already finely
                            // tessellated. Midpoint tests protect native triangles straddling
                            // a small island, and one-cell padding supports smooth edge masks.
                            if (!Near(pa) && !Near(pb) && !Near(pc) && !Near((pa + pb + pc) / 3f)
                                && !Near((pa + pb) * .5f) && !Near((pb + pc) * .5f) && !Near((pc + pa) * .5f)) continue;
                            Vector2 ua = AuthorityUV(pa), ub = AuthorityUV(pb), uc = AuthorityUV(pc);
                            bool seam = Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x)) - Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x)) > .5f;
                            triangles.Add(Add(a, ua, seam));
                            triangles.Add(Add(b, ub, seam));
                            triangles.Add(Add(c, uc, seam));
                        }
                    }

                    bool Near(Vector3 p)
                    {
                        Vector2 pUv = AuthorityUV(p);
                        int x = Mathf.FloorToInt(pUv.x * columns) % columns;
                        if (x < 0) x += columns;
                        int y = Mathf.Clamp(Mathf.FloorToInt(pUv.y * rows), 0, rows - 1);
                        return nearby[y * columns + x];
                    }

                    int Add(int original, Vector2 mapUv, bool seam)
                    {
                        if (!seam && mapped.TryGetValue(original, out int existing)) return existing;
                        int next = vertices.Count;
                        Vector3 p = positions[original];
                        Vector3 n = hasNormals ? sourceNormals[original].normalized : p.normalized;
                        if (n.sqrMagnitude < 1e-8f) n = p.normalized;
                        vertices.Add(p + n * surfaceOffset); normals.Add(n);
                        uv.Add(projection.Project(p));
                        // Seam triangles get independent vertices and continuous U, with
                        // Repeat sampling restoring the wrapped authority coordinates.
                        if (seam && mapUv.x < .5f) mapUv.x += 1f;
                        authorityUv.Add(mapUv);
                        if (!seam) mapped.Add(original, next);
                        return next;
                    }
                }
            }

            if (triangles.Count == 0) throw new InvalidOperationException("No saved body triangles intersect the footprint.");
            Mesh mesh = null;
            try
            {
                mesh = new Mesh {
                    name = "Surface sticker (saved body triangles)",
                    hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.SetVertices(vertices); mesh.SetNormals(normals);
                mesh.SetUVs(0, uv); mesh.SetUVs(1, authorityUv);
                mesh.SetTriangles(triangles, 0, true);
                return new Result {
                    Mesh = mesh, SourceVertexCount = sourceCount, SourceTriangleCount = sourceTriangles,
                    SelectedTriangleCount = triangles.Count / 3, CellsCount = uniqueCells.Count,
                    Offset = surfaceOffset, Projection = projection
                };
            }
            catch
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                throw;
            }
        }

        public static Vector2 AuthorityUV(Vector3 bodyLocalPosition)
        {
            Vector3 d = bodyLocalPosition.normalized;
            return new Vector2(Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI) + .5f, .5f - d.y * .5f);
        }

        public static Vector3 CellDirection(int cell, int columns, int rows)
        {
            if (columns < 2 || rows < 2 || cell < 0 || cell >= checked(columns * rows))
                throw new ArgumentOutOfRangeException(nameof(cell));
            return Direction((cell % columns + .5f) / columns, (cell / columns + .5f) / rows);
        }

        static Vector3 Direction(float u, float v)
        {
            float y = 1f - 2f * v;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = (u - .5f) * 2f * Mathf.PI;
            return new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
        }

        static Vector2 Tangent(Vector3 d, Vector3 center, Vector3 right, Vector3 up)
        {
            float cosine = Mathf.Clamp(Vector3.Dot(d, center), -1f, 1f);
            float angle = Mathf.Acos(cosine);
            float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
            if (angle < 1e-5f) return Vector2.zero;
            if (cosine < -.99999f)
                throw new ArgumentException("A local sticker cannot span the projection's antipode.");
            float factor = angle / Mathf.Max(sine, 1e-6f);
            return new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, up)) * factor;
        }

        static int Index(Mesh.MeshData data, int at) => data.indexFormat == IndexFormat.UInt16
            ? data.GetIndexData<ushort>()[at] : (int)data.GetIndexData<uint>()[at];

        static void ValidateCells(IReadOnlyList<int> cells, int columns, int rows)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (columns < 2 || rows < 2 || (long)columns * rows > 4194304)
                throw new ArgumentOutOfRangeException(nameof(columns));
            if (cells.Count == 0) throw new ArgumentException("A sticker needs actually paid cells.", nameof(cells));
            int count = columns * rows;
            foreach (int cell in cells)
                if (cell < 0 || cell >= count) throw new ArgumentOutOfRangeException(nameof(cells), "Invalid paid cell index.");
        }

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
    }
}
