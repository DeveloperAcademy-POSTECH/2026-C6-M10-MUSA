using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabElementRelease
    {
        private void Earth(float t)
        {
            float gathered = Mathf.Clamp(settings.earthGatherEnd, .16f, .42f);
            float splitStart = Mathf.Clamp(settings.earthCrumbleStart, gathered + .03f, .72f);
            float edgeLead = Mathf.Clamp(settings.earthEdgeLead, .06f, .24f);
            int count = Mathf.Clamp(settings.grainsPerClump, 8, 20);
            if (lowDetail) count = Mathf.Max(8, count * 2 / 3);
            for (int clump = 0; clump < forms; clump++)
            {
                float delay = .018f * clump;
                float age = t - delay;
                if (age <= .16f) continue;
                float assemble = Open(age, .17f, gathered);
                float lift = 1f - Mathf.Pow(1f - Phase(age, .14f, .48f), 2f);
                float side = clump == 1 ? -1f : 1f;
                float reach = clump == 0 ? .63f : clump == 1 ? .46f : .17f;
                float height = clump == 0 ? .98f : clump == 1 ? .64f : 1.24f;
                Vector3 center = P(side * (.09f + lift * reach), .19f + height * lift, .12f * (clump - 1));
                // A compact region moves out with the plume. Neighbours close just a fraction of a grain diameter.
                // Smaller grit fills porous pockets and peels off the outlines earlier than the cores.
                int flecks = lowDetail ? count / 3 : count * 2 / 3;
                for (int i = 0; i < count + flecks; i++)
                {
                    int pattern = i < count ? i : 20 + i - count;
                    SandGrain grain = SandSamples.Grains[clump * SandSamples.PerClump + pattern];
                    float detaches = splitStart + edgeLead * (1f - grain.edge) + grain.splitJitter;
                    float loose = Mathf.Max(0f, age - detaches);
                    float fade = 1f - Open(age, detaches + .105f, Mathf.Min(.99f, detaches + .39f));
                    float grow = Open(age, .16f + grain.growJitter, gathered);
                    if (grow * fade < .015f) continue;
                    float packing = Mathf.Lerp(1.14f, .73f, assemble);
                    packing = Mathf.Lerp(packing, 1.03f, Open(loose, 0f, .17f));
                    Vector3 p = center + grain.offset * packing
                        + grain.launch * (loose * Mathf.Clamp(settings.escapeSpeed, .4f, 1.6f))
                        + fall * (loose * loose * 8.75f * Mathf.Clamp(settings.gravity, .2f, 2f));
                    DrawGrain(grain, p, grow * fade, loose * grain.spin, Color.Lerp(dark, mid, grain.pigment));
                }
            }
        }

        // Shape noise and the neighbourhood never change mid-flight. Bake the variants once,
        // shared by every pool slot, so sampling all five lanes never regenerates their topology.
        private static class SandSamples
        {
            internal const int PerClump = 34;
            internal static readonly SandGrain[] Grains = Build();

            private static SandGrain[] Build()
            {
                var samples = new SandGrain[3 * PerClump];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = new SandGrain(i / PerClump, i % PerClump);
                return samples;
            }
        }

        private sealed class SandGrain
        {
            internal readonly float edge, splitJitter, growJitter, spin, pigment;
            internal readonly Vector3 offset, launch;
            internal readonly Vector3[] vertices = new Vector3[15];
            internal readonly float[] tints = new float[15];

            internal SandGrain(int clump, int i)
            {
                int seed = 11 + i * 23 + clump * 443;
                float side = clump == 1 ? -1f : 1f;
                bool crumb = i >= 20;
                int cell = (i * 7) % 20;
                // Jitter an elongated bed of neighbouring grains, with uneven head/tail widths and holes.
                // Orientation is independent of position to avoid a circular array of petal-like pieces.
                float x = (cell % 5 - 2f + (Hash(seed) - .5f) * .70f) * .129f;
                float y = (cell / 5 - 1.5f + (Hash(seed + 1) - .5f) * .68f) * .092f;
                y *= 1f - .40f * Mathf.Clamp01(Mathf.Abs(x) / .29f);
                x += ((cell / 5) % 2 == 1 ? .023f : -.023f);
                if (crumb)
                {
                    x *= 1.20f;
                    y *= 1.45f;
                    y += (Hash(seed + 54) - .5f) * .068f;
                }
                edge = Mathf.Clamp01(Mathf.Sqrt(x*x/.11f + y*y/.035f));
                if (crumb) edge = Mathf.Max(.7f, edge);
                offset = P(x * side, y + x * (.29f + .08f * clump), (Hash(seed + 9) - .5f) * .09f);
                launch = P(side * .22f + offset.x * 4.85f, -.12f + offset.y * 2.9f, offset.z * 2f);
                splitJitter = .027f * Hash(seed + 3) - (crumb ? .055f : 0f);
                growJitter = .027f * Hash(seed + 2);
                spin = (Hash(seed + 12) - .5f) * 7f;
                pigment = .07f + .89f * Hash(seed + 6);
                float radius = Mathf.Lerp(.043f, .076f, Hash(seed + 5)) * (crumb ? .49f : 1f);
                Vector3 radii = P(.90f + .28f * Hash(seed + 8), .76f + .22f * Hash(seed + 4), .82f) * radius;
                float tilt = .20f + Hash(seed + 15) * .55f;
                float angle = Hash(seed + 17) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                // Three offset pentagonal rings give the grain blunt, unequal facets.
                for (int ring = 0; ring < 3; ring++)
                {
                    float section = ring == 1 ? 1f : .74f;
                    for (int j = 0; j < 5; j++)
                    {
                        float a = j * (Mathf.PI * 2f / 5f) + .15f * ring + tilt;
                        float irregular = .84f + .26f * Hash(seed + j + ring * 27);
                        float vx = Mathf.Cos(a) * radii.x * section * irregular;
                        float vy = (ring - 1) * radii.y * (.68f + .15f * Hash(seed + j + 44));
                        float vz = Mathf.Sin(a) * radii.z * section * irregular;
                        vertices[ring * 5 + j] = P(vx * cos - vy * sin, vx * sin + vy * cos, vz);
                        tints[ring * 5 + j] = .78f + .17f * Hash(seed + j + 61) + .10f * ring;
                    }
                }
            }
        }

        private void DrawGrain(SandGrain sample, Vector3 p, float radius, float spin, Color sand)
        {
            int first = positions.Count;
            float cos = Mathf.Cos(spin) * radius, sin = Mathf.Sin(spin) * radius;
            for (int i = 0; i < sample.vertices.Length; i++)
            {
                Vector3 v = sample.vertices[i];
                Vertex(p + P(v.x * cos - v.y * sin, v.x * sin + v.y * cos, v.z * radius), sand * sample.tints[i]);
            }
            for (int j = 0; j < 5; j++)
            {
                int k = (j + 1) % 5;
                for (int ring = 0; ring < 2; ring++)
                {
                    int start = first + ring * 5;
                    Tri(start + j, start + k, start + j + 5);
                    Tri(start + k, start + k + 5, start + j + 5);
                }
            }
            for (int i = 1; i < 4; i++)
            {
                Tri(first, first + i + 1, first + i);
                Tri(first + 10, first + 10 + i, first + 11 + i);
            }
        }

        private void Fire(float t)
        {
            for (int i = 0; i < forms; i++)
            {
                float side = (i & 1) == 0 ? 1f : -1f;
                float height = i == 0 ? 1.48f : i == 1 ? .90f : .67f;
                float x = i == 0 ? .20f : i == 1 ? -.23f : .47f;
                float age = t - .025f * i;
                float reveal = Open(age, .16f, .37f);
                float retract = Open(age, .47f, .87f);
                if (reveal <= .002f || retract > .998f) continue;
                float sway = Mathf.Sin(age * 17f + i * 1.9f);
                for (int fork = 0; fork < 2; fork++)
                {
                    int last = -1;
                    float forkBase = fork == 0 ? 0 : .39f;
                    float forkEnd = fork == 0 ? 1 : .82f;
                    for (int j = 0; j <= 16; j++)
                    {
                        float along = j / 16f;
                        float u = Mathf.Lerp(Mathf.Lerp(forkBase, forkEnd, retract), forkEnd, along * reveal);
                        float bend = (Mathf.Sin(u * 2.25f * Mathf.PI - .25f) + .46f * u * sway);
                        float branch = fork == 1 ? -side * .22f * Mathf.Pow(along, .76f) : 0;
                        Vector3 p = P(x * (.51f + u) + side * bend * (.03f + u * .137f) + branch,
                            .20f + height * u + .16f * retract, .04f * i + .045f * bend);
                        float bell = Mathf.Pow(Sin01(along), .82f) * Mathf.Lerp(1.5f, .48f, along);
                        float w = (.014f * (1f - along) + .125f * bell) * (1f - retract)
                            * reveal * (fork == 1 ? .47f : 1f) * Mathf.Lerp(.65f, 1f, height / 1.48f);
                        StripRow(ref last, p, P(w, -.42f * side * w, .10f * w), .62f * w,
                            body, Color.Lerp(light, mid, along), Color.Lerp(dark, mid, .73f));
                    }
                }
            }
            if (lowDetail) return;
            for (int i = 0; i < 5; i++)
            {
                float age = Phase(t - .026f * i, .27f, .91f);
                float r = .045f * Sin01(age);
                if (r < .002f) continue;
                Vector3 p = P((Hash(i + 37) - .5f) * (.46f + .97f * age), .54f + 1.05f * age, .13f);
                Facet(p, P((Hash(i + 22) - .5f) * r, 1.42f * r), P(.47f * r, 0, .19f * r), mid, light);
            }
        }

        private static Vector3 Curl(float u, int index)
        {
            float side = index == 0 ? 1f : -1f;
            float angle = Mathf.LerpUnclamped(-2.27f, 2.49f, u);
            float arc = index == 0 ? .60f : .37f;
            // A tapering spiral hooks back into the wave; the tip follows this same curve before detaching.
            float curlRadius = 1f - .43f * Open(u, .67f, 1f);
            float baseX = index == 0 ? .13f : -.055f;
            return P(baseX + side * arc * (Mathf.Cos(angle) * curlRadius + .62f),
                .20f + arc * 1.27f * (Mathf.Sin(angle) * curlRadius + .77f),
                .03f * index + .095f * Mathf.Sin(u * Mathf.PI));
        }

        private void Water(float t)
        {
            for (int i = 0; i < 2; i++)
            {
                float age = t - .035f * i;
                float to = Open(age, .13f, .35f);
                float from = Open(age, .49f, .88f);
                if (to <= from + .002f) continue;
                int last = -1;
                for (int j = 0; j <= 24; j++)
                {
                    float along = j / 24f;
                    float u = Mathf.Lerp(from, to, along);
                    Vector3 p = Curl(u, i);
                    Vector3 tangent = Curl(u + .002f, i) - Curl(u - .002f, i);
                    Vector3 across = P(-tangent.y, tangent.x).normalized;
                    float crest = Mathf.Sin(Mathf.PI * Mathf.Clamp01((u - .26f) / .74f));
                    float taper = .30f + .70f * Mathf.Pow(Sin01(along), .60f);
                    float w = (i == 0 ? .145f : .114f) * taper * (1f + .50f * crest) * (1f - from) * to;
                    StripRow(ref last, p, across * w, w * .27f, Color.Lerp(dark, body, .72f),
                        Color.Lerp(body, mid, .70f), light);
                }

                // A meniscus rides the curl tip before its separate droplet lifts off.
                float flight = Mathf.Max(0, age - .45f);
                float r = (i == 0 ? .097f : .067f) * to * (1f - Open(age, .62f, .88f));
                float dir = i == 0 ? -1f : 1f;
                Drop(Curl(to, i) + P(dir * flight * 1.0f, flight * .84f - 2.6f * flight * flight, .02f),
                    r, Color.Lerp(mid, light, .50f));
                if (lowDetail) continue;
                for (int splash = 0; splash < 2; splash++)
                {
                    float u = Phase(age - .047f * splash, .30f, .74f);
                    float size = (.033f + .013f * splash) * Sin01(u);
                    Vector3 p = Curl(.62f + .35f * u, i) + P(-dir * .16f * u, .28f * u, .05f);
                    if (size > .002f) Drop(p, size, light);
                }
            }
        }

        private void Drop(Vector3 center, float r, Color c)
        {
            if (r <= .002f) return;
            const int sides = 7, latitudes = 5;
            int first = positions.Count;
            for (int k = 0; k <= latitudes; k++)
            {
                float angle = Mathf.PI * k / latitudes;
                for (int l = 0; l < sides; l++)
                {
                    float turn = Mathf.PI * 2f * l / sides;
                    Vector3 n = P(Mathf.Cos(turn) * Mathf.Sin(angle), Mathf.Cos(angle), Mathf.Sin(turn) * Mathf.Sin(angle));
                    float shade = .84f + .10f * n.y - .06f * n.x;
                    Vertex(center + r * P(n.x, n.y * 1.10f, n.z), c * shade);
                }
            }
            for (int k = 0; k < latitudes; k++)
            {
                int a = first + k * sides;
                for (int j = 0; j < sides; j++)
                {
                    int next = (j + 1) % sides;
                    if (k > 0) Tri(a + j, a + next, a + sides + j);
                    if (k < latitudes - 1) Tri(a + next, a + sides + next, a + sides + j);
                }
            }
        }

        private void Wood(float t)
        {
            float grow = Open(t, .16f, .30f);
            float shrink = 1f - Open(t, .57f, .90f);
            if (grow * shrink < .003f) return;
            Vector3 root = P(.02f, .18f);
            Vector3 fork = P(.07f, .71f, .025f);
            Vector3 elbow = P(-.20f, .46f, .06f);
            Stem(root, elbow, fork, grow, shrink, .037f);
            for (int i = 0; i < forms; i++)
            {
                float side = i == 1 ? -1 : 1;
                float h = i == 0 ? .47f : i == 1 ? .30f : .12f;
                float x = i == 0 ? .18f : i == 1 ? .34f : .40f;
                Vector3 foot = OnStem(root, elbow, fork, grow * (i == 2 ? .43f : 1f));
                Vector3 tip = foot + P(side * x, h, .023f * i);
                float branchGrow = Open(t, .19f + .015f * i, .31f + .013f * i);
                Vector3 bend = foot + P(side * x * .24f, .70f * h);
                Stem(foot, bend, tip, branchGrow, shrink, .024f);
                for (int leaf = 0; leaf < 2; leaf++)
                {
                    float open = Open(t, .211f + .014f * i + .034f * leaf, .34f + .017f * i + .034f * leaf);
                    if (open < .005f) continue;
                    float turn = (1f - shrink) * (.62f + .10f * i);
                    float tilt = side * (leaf == 0 ? .99f : -.53f) * open + turn;
                    float length = (leaf == 0 ? .50f : .36f) * open * (i == 2 ? .68f : 1f) * shrink;
                    Vector3 basePoint = OnStem(foot, bend, tip, branchGrow * (leaf == 1 ? .81f : 1f))
                        + P(0, .15f * (1f - shrink), .10f * (1f - shrink));
                    Leaf(basePoint, tilt, length, .24f + .57f * open, turn);
                }
            }
        }

        private static Vector3 OnStem(Vector3 a, Vector3 b, Vector3 c, float u) =>
            Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(b, c, u), u);

        private void Stem(Vector3 a, Vector3 b, Vector3 c, float progress, float decay, float radius)
        {
            if (progress * decay < .005f) return;
            int row = -1;
            for (int i = 0; i <= 9; i++)
            {
                float u = i / 9f * progress;
                Vector3 p = OnStem(a, b, c, u);
                float w = radius * (1f - .67f * u) * decay;
                StripRow(ref row, p, P(w, 0), w * .52f, body, mid, light);
            }
        }

        private void Leaf(Vector3 origin, float tilt, float length, float openWidth, float turn)
        {
            Vector3 axis = P(Mathf.Sin(tilt), Mathf.Cos(tilt), -.15f);
            Vector3 side = P(axis.y * Mathf.Cos(turn), -axis.x * Mathf.Cos(turn), Mathf.Sin(turn));
            int row = -1;
            for (int i = 0; i <= 10; i++)
            {
                float u = i / 10f;
                float bow = Sin01(u);
                float w = length * .39f * Mathf.Pow(bow, .87f) * openWidth * Mathf.Lerp(1.22f, .78f, u);
                Vector3 p = origin + length * (axis * u + P(.09f * bow, -.045f * bow, .18f * bow));
                StripRow(ref row, p, side * w, .48f * w, body, light, Color.Lerp(mid, body, .32f));
            }
        }

        private void Metal(float t)
        {
            for (int i = 0; i < forms; i++)
            {
                float age = t - .032f * i;
                float opened = Open(age, .19f, .34f);
                float decay = 1f - Open(age, .55f, .81f);
                if (opened * decay < .002f) continue;
                float sideways = i == 0 ? .55f : i == 1 ? -.66f : .93f;
                Vector3 axis = P(sideways, i == 2 ? .62f : 1f, .06f * i).normalized;
                Vector3 cross = P(axis.y, -axis.x).normalized;
                // The lead blade shows its broad bevel as it separates. Smaller shards turn earlier;
                // even the two-shard quality setting retains a readable face through the reflection.
                float flipStart = i == 0 ? .47f : i == 1 ? .255f : .355f;
                float turn = -.16f + Mathf.PI * Open(age, flipStart, flipStart + .235f);
                Vector3 crossFace = cross * Mathf.Cos(turn) + P(0, 0, 1f) * Mathf.Sin(turn);
                float travel = .27f + (.78f + (i == 0 ? .20f : 0)) * opened
                    + .33f * Phase(age, .34f, .82f) * Mathf.Clamp(settings.escapeSpeed, .4f, 1.6f);
                Vector3 p = P(0, .16f, .06f * i) + axis * travel;
                float radius = opened * decay * (i == 0 ? .47f : .30f);
                float gleam = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(turn - 1.10f) / .37f), 2f);
                Facet(p, axis * radius, crossFace * (radius * .36f), dark, Color.Lerp(light, Color.white, gleam));
                if (gleam > .04f && i == 0 && !lowDetail)
                {
                    Vector3 point = p + axis * radius;
                    Facet(point, P(0, .11f * gleam), P(.012f * gleam, 0), light, Color.white);
                    Facet(point, P(.088f * gleam, 0), P(0, .010f * gleam), light, Color.white);
                }
            }
        }

        private void Facet(Vector3 center, Vector3 axis, Vector3 across, Color shade, Color highlight)
        {
            int a = Vertex(center - axis, shade);
            Vertex(center - across - axis * .09f, Color.Lerp(shade, highlight, .27f));
            Vertex(center + axis, highlight);
            Vertex(center + across + axis * .12f, Color.Lerp(shade, highlight, .63f));
            Vertex(center + P(0, 0, across.magnitude * .31f), highlight);
            Tri(a, a + 1, a + 4); Tri(a + 1, a + 2, a + 4);
            Tri(a + 2, a + 3, a + 4); Tri(a + 3, a, a + 4);
        }
    }
}
