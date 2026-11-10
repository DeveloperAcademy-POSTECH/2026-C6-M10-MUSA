using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>Shared, closed meshes. Object-space units are projectile diameters.</summary>
    internal sealed class LabClayShapes
    {
        public readonly Mesh ball;
        public readonly Mesh streak;
        public readonly Mesh[] splats = new Mesh[4];
        public readonly Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly int Tint = Shader.PropertyToID("_BaseColor");
        private static readonly int Gloss = Shader.PropertyToID("_Gloss");
        private static readonly int Sag = Shader.PropertyToID("_Sag");
        private static readonly int Ink = Shader.PropertyToID("_InkColor");
        private static readonly int Shadow = Shader.PropertyToID("_ShadowColor");
        private static readonly int Lit = Shader.PropertyToID("_LitColor");
        private static readonly int Light = Shader.PropertyToID("_LightColor");
        private static readonly int Glint = Shader.PropertyToID("_GlintColor");
        internal const float FiveColorStepSeconds = .9f, FiveColorFlowSpeed = 2f;

        public LabClayShapes(Shader shader)
        {
            material = new Material(shader) { name = "Wet pigment (shared)", hideFlags = HideFlags.DontSave };
            ball = BuildRound(false);
            streak = BuildRound(true);
            for (int i = 0; i < splats.Length; ++i) splats[i] = BuildSplat(i);
        }

        public MeshRenderer Create(Transform parent, string name, Mesh shape)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = shape;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        public void Paint(MeshRenderer renderer, Color color, float gloss, Vector3 sag)
        {
            properties.Clear();
            properties.SetColor(Tint, color);
            properties.SetFloat(Gloss, gloss);
            properties.SetVector(Sag, sag);
            if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(properties);
        }

        public void PaintOrb(MeshRenderer renderer, Material surface, Color fallback, float gloss)
        {
            if (surface != null && surface.shader.isSupported)
            {
                renderer.SetPropertyBlock(null);
                renderer.sharedMaterial = surface;
                return;
            }
            renderer.sharedMaterial = material;
            Paint(renderer, fallback, gloss, Vector3.zero);
        }

        public void PaintFiveColorOrb(MeshRenderer renderer, Material surface, Color color, float gloss)
        {
            if (surface == null || !surface.shader.isSupported || !surface.HasProperty(Ink))
            {
                renderer.sharedMaterial = material;
                Paint(renderer, color, gloss, Vector3.zero);
                return;
            }
            // Recolor the authored value ramp, retaining the paper texture, bands and dry brush.
            // Overriding only _BaseColor would leave the shadow and highlight bands gray.
            float middle = surface.GetColor(Tint).grayscale;
            properties.Clear();
            properties.SetColor(Ink, RampColor(color, surface.GetColor(Ink), middle));
            properties.SetColor(Shadow, RampColor(color, surface.GetColor(Shadow), middle));
            properties.SetColor(Tint, color);
            properties.SetColor(Lit, RampColor(color, surface.GetColor(Lit), middle));
            properties.SetColor(Light, RampColor(color, surface.GetColor(Light), middle));
            properties.SetColor(Glint, RampColor(color, surface.GetColor(Glint), middle));
            if (renderer.sharedMaterial != surface) renderer.sharedMaterial = surface;
            renderer.SetPropertyBlock(properties);
        }

        private static Color RampColor(Color pigment, Color authored, float middle)
        {
            float value = authored.grayscale;
            Color result = value <= middle ? pigment * (value / Mathf.Max(.001f, middle)) :
                Color.Lerp(pigment, Color.white, (value - middle) / Mathf.Max(.001f, 1f - middle));
            result.a = authored.a;
            return result;
        }

        public void ResetPaint(MeshRenderer renderer)
        {
            renderer.SetPropertyBlock(null);
            renderer.sharedMaterial = material;
        }

        public void Dispose()
        {
            Object.Destroy(material);
            Object.Destroy(ball);
            Object.Destroy(streak);
            foreach (Mesh splat in splats) Object.Destroy(splat);
        }

        private static Mesh BuildRound(bool tapered)
        {
            const int sides = 34, rows = 21;
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            vertices.Add(Vector3.down * .5f);
            colors.Add(new Color32(0,165,250,255));
            for (int j = 1; j < rows; ++j)
            {
                float latitude = Mathf.PI * (j / (float)rows - .5f);
                float y = Mathf.Sin(latitude), ring = Mathf.Cos(latitude);
                for (int i = 0; i < sides; ++i)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    Vector3 n = new Vector3(ring * Mathf.Cos(a), y, ring * Mathf.Sin(a));
                    float handMade = Mathf.Sin(3.4f*n.x + 2.3f*n.y) * Mathf.Sin(5f*n.z - n.y)
                        + .55f * Mathf.Cos(6.7f*n.y + 3.1f*n.z + n.x);
                    float seam = n.z + .26f * Mathf.Sin(4f*n.x + 2f*n.y);
                    float thumbprint = Mathf.Exp(-102f*seam*seam) * Mathf.Max(0, n.x+.35f*n.y);
                    Vector3 p = n * (.5f + .023f*handMade - .022f*thumbprint);
                    if (tapered)
                    {
                        float taper = Mathf.Lerp(.24f, 1.15f, (n.z+1f)*.5f);
                        p.x *= taper;
                        p.y *= taper;
                    }
                    vertices.Add(p);
                    colors.Add(new Color32(0, (byte)(145 + 53 * handMade - 25*thumbprint), (byte)(250-42*thumbprint), 255));
                }
            }
            int last = vertices.Count;
            vertices.Add(Vector3.up * .5f);
            colors.Add(new Color32(0,180,250,255));
            for (int i=0; i<sides; ++i)
            {
                int next = (i+1) % sides;
                triangles.Add(0); triangles.Add(1+i); triangles.Add(1+next);
                for (int j=0; j<rows-2; ++j)
                {
                    int a=1+j*sides+i, b=1+j*sides+next;
                    triangles.Add(a); triangles.Add(a+sides); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a+sides); triangles.Add(b+sides);
                }
                triangles.Add(last); triangles.Add(last-sides+next); triangles.Add(last-sides+i);
            }
            return Finish(tapered ? "Short pigment streak" : "Hand kneaded clay", vertices, colors, triangles);
        }

        // Cross-sections of pressed folds in the contact plane. The smooth union is sampled into
        // ONE closed surface; there are no intersections, detached ends or shadow seams between rolls.
        private readonly struct Fold
        {
            private readonly Vector2 center, axis, cross;
            private readonly float length, width, bend;
            public readonly float Height;

            public Fold(Vector2 center, float angle, float length, float width, float height, float bend)
            {
                this.center=center; this.length=length; this.width=width; this.bend=bend;
                axis = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                cross = new Vector2(-axis.y,axis.x);
                Height = height;
            }

            public float Inside(Vector2 p)
            {
                Vector2 offset=p-center;
                float u=Vector2.Dot(offset,axis)/length;
                float curved=bend*(1f-Mathf.Min(1f,u*u));
                float v=(Vector2.Dot(offset,cross)-curved)/width;
                // Superellipse across the stroke gives a thumb-shaped, blunt end.
                return 1f - u*u*u*u - v*v;
            }
        }

        private static float SmoothUnion(float a, float b, float k)
        {
            float h = Mathf.Max(0,k-Mathf.Abs(a-b))/k;
            return Mathf.Max(a,b) + .25f*h*h*k;
        }

        private static float Outline(Fold[] folds, Vector2 p)
        {
            float field=-40f;
            foreach (var fold in folds) field=SmoothUnion(field,fold.Inside(p),.34f);
            return field;
        }

        private static Mesh BuildSplat(int variant)
        {
            const int sides=88, bands=15;
            float seed=variant*1.29f;
            var folds=new Fold[9];
            // The palm and two crossing crests merge into the roots of the extruded fingers.
            folds[0]=new Fold(Vector2.zero,seed+.37f,.57f,.48f,.295f,.07f);
            folds[1]=new Fold(new Vector2(.07f,.05f),seed+.78f,.51f,.17f,.385f,.105f);
            folds[2]=new Fold(new Vector2(-.07f,-.035f),seed+2.11f,.41f,.165f,.34f,-.09f);
            float[] angles = {-.16f,.71f,1.90f,2.65f,3.45f,4.81f};
            float[] ends = {.94f,.67f,.70f,.97f,.58f,.82f};
            float[] widths = {.17f,.22f,.17f,.18f,.21f,.205f};
            for (int i=0; i<angles.Length; ++i)
            {
                float phi=angles[i]+seed+.13f*Mathf.Sin(2f*i+seed);
                Vector2 axis=new Vector2(Mathf.Cos(phi),Mathf.Sin(phi));
                float end=ends[i]+.035f*Mathf.Sin(seed+2.8f*i);
                float radius=(end-.09f)*.5f;
                folds[3+i]=new Fold(axis*(end-radius),phi,radius,widths[i],
                    .17f+.035f*Mathf.Cos(i+seed),.07f*Mathf.Sin(2.1f*i+seed));
            }
            var v=new List<Vector3>();
            var c=new List<Color32>();
            var t=new List<int>();
            // Duplicate the center only once. Density near the outer edge rounds the shoulders.
            AddSplatVertex(v,c,folds,Vector2.zero,0,seed,0);
            for (int i=0; i<sides; ++i)
            {
                float a=i*Mathf.PI*2f/sides;
                Vector2 dir = new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                float lo=.26f, hi=1.26f;
                for (int step=0; step<14; ++step)
                {
                    float r=(lo+hi)*.5f;
                    if (Outline(folds,dir*r)>0f) lo=r; else hi=r;
                }
                for (int j=1; j<=bands; ++j)
                {
                    float u=Mathf.Sin(.5f*Mathf.PI*j/bands);
                    AddSplatVertex(v,c,folds,dir*(lo*u),u,seed,a);
                }
                // Pull the rocker's underside IN and below the contact plane (no flat lit skirt).
                v.Add(new Vector3(dir.x*lo*.94f,-.028f,dir.y*lo*.94f));
                c.Add(new Color32(0,108,115,255));
            }
            int bottom=v.Count;
            v.Add(new Vector3(0,-.03f,0)); c.Add(new Color32(0,110,110,255));
            for (int i=0; i<sides; ++i)
            {
                int a=1+i*(bands+1), b=1+(i+1)%sides*(bands+1);
                t.Add(0); t.Add(b); t.Add(a);
                for (int j=0; j<bands; ++j)
                {
                    t.Add(a+j); t.Add(b+j); t.Add(a+j+1);
                    t.Add(b+j); t.Add(b+j+1); t.Add(a+j+1);
                }
                t.Add(bottom); t.Add(a+bands); t.Add(b+bands);
            }
            return Finish("Folded wet spread " + variant,v,c,t);
        }

        private static void AddSplatVertex(List<Vector3> vertices,List<Color32> colors,Fold[] folds,
            Vector2 p,float u,float seed,float angle)
        {
            float h=0f, first=0f, second=0f;
            foreach (var fold in folds)
            {
                float f=fold.Inside(p);
                if (f<=0f) continue;
                float dome=fold.Height*Mathf.Sqrt(f);
                if (dome>first) { second=first; first=dome; }
                else second=Mathf.Max(second,dome);
                h=SmoothUnion(h,dome,.034f);
            }
            float marbling=Mathf.Sin(p.x*14f+p.y*8f+seed+1.3f*Mathf.Sin(7f*p.y));
            float ridge=Mathf.Sin(7f*p.x-10f*p.y+seed);
            h *= 1f+.052f*marbling+.046f*ridge;
            vertices.Add(new Vector3(p.x,.001f+h,p.y));
            float crease=(1f-Mathf.SmoothStep(0f,1f,Mathf.Abs(first-second)/.085f))
                * Mathf.SmoothStep(0f,1f,second/.18f);
            float tone=Mathf.Clamp01(.60f+.16f*marbling+.12f*ridge-.30f*crease);
            float sag=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.63f,1f,u))
                * Mathf.SmoothStep(0f,1f,Mathf.Cos(2f*angle+seed));
            colors.Add(new Color32((byte)(255*sag),(byte)(255*tone),(byte)(249-69*crease),255));
        }

        private static Mesh Finish(string name, List<Vector3> v, List<Color32> c, List<int> t)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t,0);
            mesh.RecalculateNormals();
            Bounds b=mesh.bounds; b.Expand(.4f); mesh.bounds=b;
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
