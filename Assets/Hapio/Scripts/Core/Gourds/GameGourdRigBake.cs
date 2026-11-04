#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using C6Lab;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hapio
{
    /// <summary>
    /// #507 PR-2: the pure part of baking the Stage 2 Yagwanggwi gourd into the gourd model contract
    /// (<see cref="LabGourdVisualRig"/>). It reads the art prefab without changing it and returns meshes, the yokai
    /// timeline and a legacy clip in memory. The Editor tool (`GameGourdRigBaker`) saves them; the freshness test calls
    /// this again and compares. Editor only (the art meshes are not Read/Write, so they are read with
    /// <see cref="Mesh.AcquireReadOnlyMeshData(Mesh)"/>).
    /// </summary>
    public static class GameGourdRigBake
    {
        public const string SourcePrefab = "Assets/Hapio/Art/Gourds/Stage02_Yagwanggwi/Prefabs/Yagwanggwi_Gourd_GameReady.prefab";
        /// <summary>The white gourd's in-battle height (hitbox AABB at the default Gourd Visual Size).</summary>
        public const float TargetBodyHeight = 2.390325f;
        /// <summary>The Gourd Visual Size at which the baked prefab is used at scale 1 (LabConfig default).</summary>
        public const float ReferenceVisualSize = 1.4f;
        public const int HitColumns = 48, HitRows = 24;
        public const int CycleFrames = 48;
        public const string ModelPath = "Yagwanggwi_Gourd";
        public const string VesselPath = "Vessel_Forward_Tilt.002";
        public const string BodyPath = VesselPath + "/model_part0.001";
        public const string LidHingePath = VesselPath + "/Lid_Hinge.001";
        public const string LidPath = LidHingePath + "/model_part3.001";
        public const string SpiritPath = VesselPath + "/Spirit_Peek_Control.001";
        public const string YokaiBodyPath = SpiritPath + "/Eye_Pop_Control.001";
        public const string RopeBindingPath = "Rear_Rope_Binding.001";
        /// <summary>The model's own hanging rope. The game draws its own rope, so these are removed.</summary>
        public static readonly string[] ModelRopeParts =
        {
            "Rear_Rope_Strand_1.001", "Rear_Rope_Strand_2.001", "Rear_Rope_Strand_3.001",
            "Rear_Rope_Binding.001", "Rear_Rope_Eye_Tie.001"
        };

        public sealed class Result
        {
            public float Scale;
            /// <summary>Paint space in the (scaled) art root space: origin at the body centre, +Y along the body axis.</summary>
            public Vector3 PaintOrigin;
            public Quaternion PaintRotation;
            public Mesh PaintShell, LidPaint, HitShell;
            public Vector3 RopeSocket, SizeAnchor;
            public float LidOpensAt, RisenAt, LookFrom, LookTo, EyesBackAt, SinkFrom, SunkAt, LidClosedAt, ClipLength;
            public AnimationClip Clip;
            public int BodyTriangles, OuterTriangles, LidTriangles;
            public float StarShapedRatio, HitMaxDeviation, HitMeanDeviation;
            public readonly StringBuilder Log = new StringBuilder();

            public void Destroy()
            {
                foreach (Object item in new Object[] { PaintShell, LidPaint, HitShell, Clip })
                    if (item != null) Object.DestroyImmediate(item);
            }
        }

        /// <summary>Bake from the saved art prefab. The caller destroys the returned objects (<see cref="Result.Destroy"/>).</summary>
        public static Result Compute()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            if (prefab == null) throw new InvalidOperationException("Missing art prefab " + SourcePrefab);
            GameObject art = Object.Instantiate(prefab);
            try { return Compute(art); }
            finally { Object.DestroyImmediate(art); }
        }

        /// <summary>Bake from an instance of the art prefab at the origin. Scales the instance to the target height.</summary>
        public static Result Compute(GameObject art)
        {
            var result = new Result();
            Transform root = art.transform;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform model = Require(root, ModelPath);
            AnimationClip source = SourceClip(model);
            source.SampleAnimation(model.gameObject, 0f); // closed pose

            MeshRenderer bodyRenderer = Require(model, BodyPath).GetComponent<MeshRenderer>();
            MeshRenderer lidRenderer = Require(model, LidPath).GetComponent<MeshRenderer>();
            Transform vessel = Require(model, VesselPath);

            float height = bodyRenderer.bounds.size.y;
            result.Scale = TargetBodyHeight / height;
            root.localScale *= result.Scale;
            result.Log.AppendLine("bodyHeightBefore=" + height.ToString("F4") + " scale=" + result.Scale.ToString("F5"));

            // Paint frame: +Y along the body's own axis (Blender Z-up plus the art's static 30° forward tilt), X along the
            // vessel's right, origin at the body's centre measured in that frame.
            Vector3 axis = vessel.forward.normalized;
            Vector3 right = Vector3.ProjectOnPlane(vessel.right, axis).normalized;
            Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(right, axis), axis);
            MeshData body = Read(bodyRenderer.GetComponent<MeshFilter>().sharedMesh);
            Matrix4x4 bodyToWorld = bodyRenderer.transform.localToWorldMatrix;
            Quaternion inverse = Quaternion.Inverse(rotation);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = -min;
            foreach (Vector3 v in body.Positions)
            {
                Vector3 p = inverse * bodyToWorld.MultiplyPoint3x4(v);
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            Vector3 origin = rotation * ((min + max) * .5f);
            result.PaintOrigin = origin; result.PaintRotation = rotation;
            Matrix4x4 worldToPaint = Matrix4x4.TRS(origin, rotation, Vector3.one).inverse;
            result.Log.AppendLine("paintOrigin=" + origin.ToString("F4") + " axis=" + axis.ToString("F4")
                + " bodyExtentInFrame=" + ((max - min) * .5f).ToString("F4"));

            // Body in paint space; keep only the outer wall (the art body is double-walled).
            Triangles bodyTris = Transform(body, worldToPaint * bodyToWorld);
            result.BodyTriangles = bodyTris.Count;
            var bodyGrid = new DirectionGrid(bodyTris);
            var outer = new List<int>();
            float epsilon = 1e-3f * Mathf.Max(max.x - min.x, max.y - min.y);
            int starFacing = 0;
            for (int t = 0; t < bodyTris.Count; t++)
            {
                Vector3 centroid = bodyTris.Centroid(t);
                float distance = centroid.magnitude;
                if (distance < 1e-6f) continue;
                float farthest = bodyGrid.Farthest(centroid / distance);
                if (distance < farthest - epsilon) continue;
                outer.Add(t);
                if (Vector3.Dot(bodyTris.FaceNormal(t), centroid) > 0f) starFacing++;
            }
            result.OuterTriangles = outer.Count;
            result.StarShapedRatio = outer.Count > 0 ? starFacing / (float)outer.Count : 0f;
            result.PaintShell = bodyTris.Build(outer, "Stage02 Yagwanggwi paint shell");
            result.Log.AppendLine("bodyTris=" + bodyTris.Count + " outerTris=" + outer.Count
                + " starShaped=" + result.StarShapedRatio.ToString("P2"));

            // Lid in the same paint space (closed pose); it is parented under the hinge so it opens with its paint.
            Triangles lidTris = Transform(Read(lidRenderer.GetComponent<MeshFilter>().sharedMesh),
                worldToPaint * lidRenderer.transform.localToWorldMatrix);
            result.LidTriangles = lidTris.Count;
            result.LidPaint = lidTris.Build(null, "Stage02 Yagwanggwi lid paint");

            // Hit shell: the outermost radius of the outer wall and the closed lid along 48 x 24 directions.
            Triangles outerTris = bodyTris.Subset(outer);
            var hitGrid = new DirectionGrid(outerTris, lidTris);
            result.HitShell = BuildHitShell(hitGrid, result);
            // Deviation: how far the outer wall lies from the hit shell along rays from the paint origin.
            MeasureHitDeviation(outerTris, result);
            result.Log.AppendLine("hitTris=" + result.HitShell.triangles.Length / 3 + " hitMaxDeviation="
                + result.HitMaxDeviation.ToString("F4") + " hitMeanDeviation=" + result.HitMeanDeviation.ToString("F4"));

            Transform binding = Require(root, ModelPath + "/" + RopeBindingPath);
            Bounds bindingBounds = binding.GetComponent<Renderer>().bounds;
            result.RopeSocket = new Vector3(bindingBounds.center.x, bindingBounds.max.y, bindingBounds.center.z);
            Bounds hitBounds = result.HitShell.bounds;
            result.SizeAnchor = origin + rotation * new Vector3(0f, hitBounds.max.y, 0f);
            result.Log.AppendLine("ropeSocket=" + result.RopeSocket.ToString("F4") + " sizeAnchor=" + result.SizeAnchor.ToString("F4")
                + " hitTopInPaint=" + hitBounds.max.y.ToString("F4"));

            ExtractTimeline(source, result);
            result.Clip = BuildLegacyClip(source);
            return result;
        }

        public static AnimationClip SourceClip(Transform model)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null
                || animator.runtimeAnimatorController.animationClips.Length == 0)
                throw new InvalidOperationException("The art model has no performance clip.");
            return animator.runtimeAnimatorController.animationClips[0];
        }

        /// <summary>
        /// Profile times from the lid curve of the first 2-second cycle: closed until it starts opening, fully open
        /// (risen), held open (look-around), sinking once it is 20% closed, sunk when nearly shut, then closed.
        /// </summary>
        private static void ExtractTimeline(AnimationClip clip, Result result)
        {
            AnimationCurve lid = Curve(clip, LidHingePath, "m_LocalRotation.x");
            float fps = clip.frameRate;
            var open = new float[CycleFrames + 1];
            float closedValue = lid.Evaluate(0f), openValue = closedValue;
            for (int f = 0; f <= CycleFrames; f++)
            {
                float value = lid.Evaluate(f / fps);
                if (Mathf.Abs(value - closedValue) > Mathf.Abs(openValue - closedValue)) openValue = value;
            }
            float span = openValue - closedValue;
            if (Mathf.Abs(span) < 1e-4f) throw new InvalidOperationException("The lid never opens in the first cycle.");
            for (int f = 0; f <= CycleFrames; f++) open[f] = (lid.Evaluate(f / fps) - closedValue) / span;
            int opens = -1, risen = -1, held = -1, sink = -1, sunk = -1, closed = -1;
            for (int f = 0; f <= CycleFrames; f++)
            {
                if (opens < 0 && open[f] > .01f) opens = f - 1;
                if (opens >= 0 && risen < 0 && open[f] > .99f) risen = f;
                if (risen >= 0 && open[f] > .99f) held = f;
                if (held >= 0 && f > held && sink < 0 && open[f] < .8f) sink = f;
                if (sink >= 0 && sunk < 0 && open[f] < .05f) sunk = f;
                if (sunk >= 0 && closed < 0 && open[f] < .001f) closed = f;
            }
            if (opens < 0 || risen < 0 || held <= risen || sink < 0 || sunk < 0 || closed < 0)
                throw new InvalidOperationException("Could not find the lid keys (" + opens + "," + risen + "," + held + ","
                    + sink + "," + sunk + "," + closed + ").");
            result.LidOpensAt = opens / fps;
            result.RisenAt = risen / fps;
            result.LookFrom = risen / fps;
            result.LookTo = held / fps;
            result.SinkFrom = sink / fps;
            result.EyesBackAt = (result.LookTo + result.SinkFrom) * .5f;
            result.SunkAt = sunk / fps;
            result.LidClosedAt = closed / fps;
            result.ClipLength = CycleFrames / fps;
            result.Log.AppendLine("timeline frames opens=" + opens + " risen=" + risen + " held=" + held + " sink=" + sink
                + " sunk=" + sunk + " closed=" + closed + " cycle=" + CycleFrames);
        }

        /// <summary>The lid, peek and eye curves of the first cycle as a legacy clip. Decorations (sandal) do not loop.</summary>
        private static AnimationClip BuildLegacyClip(AnimationClip source)
        {
            var clip = new AnimationClip { name = "Yagwanggwi_Peek", legacy = true, frameRate = source.frameRate, wrapMode = WrapMode.Once };
            float end = CycleFrames / source.frameRate;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type != typeof(Transform)) continue;
                if (!binding.path.StartsWith(LidHingePath, StringComparison.Ordinal)
                    && !binding.path.StartsWith(SpiritPath, StringComparison.Ordinal)) continue;
                // The art lid and body meshes are replaced by the baked paint meshes.
                if (binding.path == LidPath || binding.path.StartsWith(LidPath + "/", StringComparison.Ordinal)
                    || binding.path == BodyPath) continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                var keys = new List<Keyframe>();
                foreach (Keyframe key in curve.keys) if (key.time <= end + 1e-4f) keys.Add(key);
                if (keys.Count == 0 || Mathf.Abs(keys[keys.Count - 1].time - end) > 1e-4f) keys.Add(new Keyframe(end, curve.Evaluate(end)));
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
            }
            return clip;
        }

        public static AnimationCurve Curve(AnimationClip clip, string path, string property)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.path == path && binding.propertyName == property && binding.type == typeof(Transform))
                    return AnimationUtility.GetEditorCurve(clip, binding);
            throw new InvalidOperationException("Missing curve " + path + " " + property);
        }

        private static Mesh BuildHitShell(DirectionGrid grid, Result result)
        {
            int rings = HitRows - 1;
            var radii = new float[rings, HitColumns];
            float north = grid.Farthest(Vector3.up), south = grid.Farthest(Vector3.down);
            int missing = 0;
            var missingRings = new SortedSet<int>();
            for (int r = 0; r < rings; r++)
                for (int c = 0; c < HitColumns; c++)
                {
                    radii[r, c] = grid.Farthest(Direction(r, c));
                    if (radii[r, c] <= 0f) { missing++; missingRings.Add(r); }
                }
            // A direction with no surface (the narrow gap between the closed lid and the body's opening) is bridged
            // along its column between the nearest hits above and below, so the shell closes over the gap.
            var filled = (float[,])radii.Clone();
            for (int c = 0; c < HitColumns; c++)
                for (int r = 0; r < rings; r++)
                {
                    if (radii[r, c] > 0f) continue;
                    int up = r - 1, down = r + 1;
                    while (up >= 0 && radii[up, c] <= 0f) up--;
                    while (down < rings && radii[down, c] <= 0f) down++;
                    float above = up >= 0 ? radii[up, c] : north, below = down < rings ? radii[down, c] : south;
                    float t = (r - up) / (float)(down - up);
                    filled[r, c] = Mathf.Lerp(above, below, t);
                }
            radii = filled;
            if (north <= 0f) north = Average(radii, 0);
            if (south <= 0f) south = Average(radii, rings - 1);
            result.Log.AppendLine("hitMissingDirections=" + missing + (missing > 0 ? " rings=" + string.Join(",", missingRings) : "")
                + " north=" + north.ToString("F4") + " south=" + south.ToString("F4"));

            var vertices = new List<Vector3> { Vector3.up * north };
            for (int r = 0; r < rings; r++)
                for (int c = 0; c < HitColumns; c++) vertices.Add(Direction(r, c) * radii[r, c]);
            vertices.Add(Vector3.down * south);
            int last = vertices.Count - 1;
            var indices = new List<int>();
            int Ring(int r, int c) => 1 + r * HitColumns + (c % HitColumns);
            for (int c = 0; c < HitColumns; c++) indices.AddRange(new[] { 0, Ring(0, c + 1), Ring(0, c) });
            for (int r = 0; r < rings - 1; r++)
                for (int c = 0; c < HitColumns; c++)
                {
                    indices.AddRange(new[] { Ring(r, c), Ring(r, c + 1), Ring(r + 1, c) });
                    indices.AddRange(new[] { Ring(r, c + 1), Ring(r + 1, c + 1), Ring(r + 1, c) });
                }
            for (int c = 0; c < HitColumns; c++) indices.AddRange(new[] { last, Ring(rings - 1, c), Ring(rings - 1, c + 1) });
            var mesh = new Mesh { name = "Stage02 Yagwanggwi hit shell" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Average(float[,] radii, int ring)
        {
            float sum = 0f;
            for (int c = 0; c < HitColumns; c++) sum += radii[ring, c];
            return sum / HitColumns;
        }

        /// <summary>Ring r (0 = just below the north pole) and column c, evenly spaced in latitude and longitude.</summary>
        public static Vector3 Direction(int ring, int column)
        {
            float polar = Mathf.PI * (ring + 1) / HitRows;
            float azimuth = 2f * Mathf.PI * column / HitColumns;
            float s = Mathf.Sin(polar);
            return new Vector3(s * Mathf.Cos(azimuth), Mathf.Cos(polar), s * Mathf.Sin(azimuth));
        }

        private static void MeasureHitDeviation(Triangles outer, Result result)
        {
            var shell = new Triangles(result.HitShell);
            var grid = new DirectionGrid(shell);
            float worst = 0f, total = 0f; int count = 0;
            for (int i = 0; i < outer.Positions.Count; i += 5)
            {
                Vector3 p = outer.Positions[i];
                float distance = p.magnitude;
                if (distance < 1e-6f) continue;
                float shellRadius = grid.Farthest(p / distance);
                if (shellRadius <= 0f) continue;
                float deviation = Mathf.Abs(distance - shellRadius);
                worst = Mathf.Max(worst, deviation); total += deviation; count++;
            }
            result.HitMaxDeviation = worst;
            result.HitMeanDeviation = count > 0 ? total / count : 0f;
        }

        private static Transform Require(Transform root, string path)
        {
            Transform found = root.Find(path);
            if (found == null) throw new InvalidOperationException("Missing art part " + path);
            return found;
        }

        private struct MeshData
        {
            public Vector3[] Positions, Normals;
            public Vector2[] Uv;
            public int[] Indices;
        }

        private static MeshData Read(Mesh mesh)
        {
            using (Mesh.MeshDataArray array = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                Mesh.MeshData data = array[0];
                var result = new MeshData
                {
                    Positions = new Vector3[data.vertexCount],
                    Normals = new Vector3[data.vertexCount],
                    Uv = new Vector2[data.vertexCount]
                };
                var positions = new Unity.Collections.NativeArray<Vector3>(data.vertexCount, Unity.Collections.Allocator.Temp);
                data.GetVertices(positions); positions.CopyTo(result.Positions);
                if (data.HasVertexAttribute(VertexAttribute.Normal)) { data.GetNormals(positions); positions.CopyTo(result.Normals); }
                positions.Dispose();
                if (data.HasVertexAttribute(VertexAttribute.TexCoord0))
                {
                    var uv = new Unity.Collections.NativeArray<Vector2>(data.vertexCount, Unity.Collections.Allocator.Temp);
                    data.GetUVs(0, uv); uv.CopyTo(result.Uv); uv.Dispose();
                }
                var indices = new List<int>();
                for (int s = 0; s < data.subMeshCount; s++)
                {
                    var sub = new Unity.Collections.NativeArray<int>(data.GetSubMesh(s).indexCount, Unity.Collections.Allocator.Temp);
                    data.GetIndices(sub, s);
                    indices.AddRange(sub);
                    sub.Dispose();
                }
                result.Indices = indices.ToArray();
                return result;
            }
        }

        private static Triangles Transform(MeshData data, Matrix4x4 matrix)
        {
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            var positions = new List<Vector3>(data.Positions.Length);
            var normals = new List<Vector3>(data.Positions.Length);
            foreach (Vector3 v in data.Positions) positions.Add(matrix.MultiplyPoint3x4(v));
            foreach (Vector3 n in data.Normals) normals.Add(normalMatrix.MultiplyVector(n).normalized);
            return new Triangles(positions, normals, new List<Vector2>(data.Uv), data.Indices);
        }

        /// <summary>Triangles with shared vertex attributes, in paint space.</summary>
        private sealed class Triangles
        {
            public readonly List<Vector3> Positions, Normals;
            public readonly List<Vector2> Uv;
            public readonly int[] Indices;
            public int Count => Indices.Length / 3;

            public Triangles(List<Vector3> positions, List<Vector3> normals, List<Vector2> uv, int[] indices)
            { Positions = positions; Normals = normals; Uv = uv; Indices = indices; }

            public Triangles(Mesh mesh) : this(new List<Vector3>(mesh.vertices), new List<Vector3>(mesh.normals),
                new List<Vector2>(), mesh.triangles) { }

            public Vector3 A(int t) => Positions[Indices[t * 3]];
            public Vector3 B(int t) => Positions[Indices[t * 3 + 1]];
            public Vector3 C(int t) => Positions[Indices[t * 3 + 2]];
            public Vector3 Centroid(int t) => (A(t) + B(t) + C(t)) / 3f;
            public Vector3 FaceNormal(int t) => Vector3.Cross(B(t) - A(t), C(t) - A(t));

            public Triangles Subset(List<int> triangles)
            {
                var indices = new int[triangles.Count * 3];
                for (int i = 0; i < triangles.Count; i++)
                    for (int k = 0; k < 3; k++) indices[i * 3 + k] = Indices[triangles[i] * 3 + k];
                return new Triangles(Positions, Normals, Uv, indices);
            }

            /// <summary>A compact mesh of the chosen triangles (all when null), keeping normals and UV0.</summary>
            public Mesh Build(List<int> triangles, string name)
            {
                var remap = new Dictionary<int, int>();
                var positions = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
                var indices = new List<int>();
                int count = triangles != null ? triangles.Count : Count;
                for (int i = 0; i < count; i++)
                {
                    int t = triangles != null ? triangles[i] : i;
                    for (int k = 0; k < 3; k++)
                    {
                        int source = Indices[t * 3 + k];
                        if (!remap.TryGetValue(source, out int target))
                        {
                            target = positions.Count; remap[source] = target;
                            positions.Add(Positions[source]);
                            normals.Add(Normals.Count > source ? Normals[source] : Vector3.up);
                            uv.Add(Uv.Count > source ? Uv[source] : Vector2.zero);
                        }
                        indices.Add(target);
                    }
                }
                var mesh = new Mesh { name = name, indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>Triangles binned by the directions (from the paint origin) they span, for rays from the origin.</summary>
        private sealed class DirectionGrid
        {
            private const int Rows = 64, Columns = 128;
            private readonly List<int>[] bins = new List<int>[Rows * Columns];
            private readonly List<Vector3> a = new List<Vector3>(), b = new List<Vector3>(), c = new List<Vector3>();

            public DirectionGrid(params Triangles[] sets)
            {
                for (int i = 0; i < bins.Length; i++) bins[i] = new List<int>();
                foreach (Triangles set in sets)
                    for (int t = 0; t < set.Count; t++) Add(set.A(t), set.B(t), set.C(t));
            }

            private void Add(Vector3 p, Vector3 q, Vector3 r)
            {
                int index = a.Count;
                a.Add(p); b.Add(q); c.Add(r);
                Vector3 centroid = (p + q + r) / 3f;
                int rowMin = Rows, rowMax = -1;
                var columns = new List<int>();
                bool pole = false;
                foreach (Vector3 v in new[] { p, q, r, centroid })
                {
                    if (v.sqrMagnitude < 1e-12f) { pole = true; continue; }
                    Bin(v.normalized, out int row, out int column);
                    rowMin = Mathf.Min(rowMin, row); rowMax = Mathf.Max(rowMax, row);
                    columns.Add(column);
                    if (row <= 1 || row >= Rows - 2) pole = true;
                }
                rowMin = Mathf.Max(0, rowMin - 1); rowMax = Mathf.Min(Rows - 1, rowMax + 1);
                columns.Sort();
                int start = columns[0], end = columns[columns.Count - 1];
                // Take the shorter way around the seam.
                int widest = 0, widestAt = 0;
                for (int i = 0; i < columns.Count; i++)
                {
                    int next = i + 1 < columns.Count ? columns[i + 1] : columns[0] + Columns;
                    if (next - columns[i] > widest) { widest = next - columns[i]; widestAt = i; }
                }
                start = widestAt + 1 < columns.Count ? columns[widestAt + 1] : columns[0];
                end = columns[widestAt];
                int span = (end - start + Columns) % Columns;
                for (int row = rowMin; row <= rowMax; row++)
                {
                    if (pole || span > Columns / 4)
                    {
                        for (int column = 0; column < Columns; column++) bins[row * Columns + column].Add(index);
                        continue;
                    }
                    for (int k = -1; k <= span + 1; k++)
                        bins[row * Columns + ((start + k) % Columns + Columns) % Columns].Add(index);
                }
            }

            private static void Bin(Vector3 direction, out int row, out int column)
            {
                row = Mathf.Clamp(Mathf.FloorToInt((.5f - direction.y * .5f) * Rows), 0, Rows - 1);
                float u = Mathf.Atan2(direction.z, direction.x) / (2f * Mathf.PI) + .5f;
                column = Mathf.Clamp(Mathf.FloorToInt(u * Columns), 0, Columns - 1);
            }

            /// <summary>The farthest hit along a ray from the origin, or 0 when nothing is hit.</summary>
            public float Farthest(Vector3 direction)
            {
                Bin(direction, out int row, out int column);
                float best = 0f;
                foreach (int t in bins[row * Columns + column])
                    if (Hit(direction, a[t], b[t], c[t], out float distance) && distance > best) best = distance;
                return best;
            }

            private static bool Hit(Vector3 direction, Vector3 p, Vector3 q, Vector3 r, out float distance)
            {
                distance = 0f;
                Vector3 e1 = q - p, e2 = r - p;
                Vector3 h = Vector3.Cross(direction, e2);
                float det = Vector3.Dot(e1, h);
                if (Mathf.Abs(det) < 1e-12f) return false;
                float inv = 1f / det;
                Vector3 s = -p;
                float u = inv * Vector3.Dot(s, h);
                if (u < -1e-5f || u > 1f + 1e-5f) return false;
                Vector3 qv = Vector3.Cross(s, e1);
                float v = inv * Vector3.Dot(direction, qv);
                if (v < -1e-5f || u + v > 1f + 1e-5f) return false;
                distance = inv * Vector3.Dot(e2, qv);
                return distance > 0f;
            }
        }
    }
}
#endif
