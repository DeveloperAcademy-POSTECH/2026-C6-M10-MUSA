using UnityEngine;

namespace C6Lab
{
    internal sealed class LabClayImpactSlot
    {
        private readonly Transform root, pressed, spread;
        private readonly MeshRenderer pressRenderer, spreadRenderer;
        private readonly MeshFilter spreadFilter;
        private readonly Transform[] drops = new Transform[5];
        private readonly MeshRenderer[] dropRenderers = new MeshRenderer[5];
        private readonly Vector3[] velocities = new Vector3[5];
        private readonly float[] sizes = new float[5];
        // Allocate renderers only when a pooled slot first presents an enhanced reward. Never allocate per frame.
        private readonly MeshRenderer[] stars = new MeshRenderer[12];
        private int starCount;
        private float starLife, starSize, starReach;
        private readonly LabClayShapes shapes;
        private readonly LabFiveElementProfile art;
        private Transform target;
        private bool followsTarget;
        private Vector3 anchor, normal, tangent, incoming;
        private Quaternion contactRotation;
        private Color color;
        private int dropCount;
        private float strength, diameter, markLife, contactLife, totalLife, oblique, spin;
        private float pressEnd, spreadDuration, dropLife, spreadScale, pressWidthScale;
        private bool fiveColor, enhancedFiveColor, singleTone;
        private Color32[] fiveColorPalette;
        private float fiveColorStartAge;
        private Material fiveColorSurface;
        public float Age { get; private set; }
        public bool Active { get; private set; }

        public LabClayImpactSlot(Transform parent, int index, LabClayShapes shapes, LabFiveElementProfile art)
        {
            this.shapes = shapes;
            this.art = art;
            root = new GameObject("Impact " + index).transform;
            root.SetParent(parent, false);
            root.gameObject.SetActive(false);
            pressRenderer=shapes.Create(root,"Contact squash",shapes.ball);
            pressed=pressRenderer.transform;
            spreadRenderer=shapes.Create(root,"Wet surface mark",shapes.splats[0]);
            spread=spreadRenderer.transform;
            spreadRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            spreadRenderer.receiveShadows=true;
            pressRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            spreadFilter=spread.GetComponent<MeshFilter>();
            for (int i=0; i<drops.Length; ++i)
            {
                dropRenderers[i]=shapes.Create(root,"Thrown clay " + i,shapes.ball);
                drops[i]=dropRenderers[i].transform;
            }
        }

        public void Begin(LabOrbElement element, Vector3 point, Vector3 surfaceNormal, Vector3 direction, float diameter,
            float strength, Transform follow, bool monochrome, float markSeconds, uint seed, bool reduced,
            bool fiveColor = false, bool enhanceFiveColor = true)
        {
            this.diameter=diameter;
            this.strength=strength;
            this.fiveColor=fiveColor;
            ClearStars();
            enhancedFiveColor=fiveColor && enhanceFiveColor && art.fiveColorBoostEnabled;
            singleTone=monochrome;
            spreadScale=enhancedFiveColor ? Mathf.Clamp(art.fiveColorSpreadScale,1f,1.8f) : 1f;
            pressWidthScale=enhancedFiveColor ? Mathf.Clamp(art.fiveColorPressWidthScale,1f,1.5f) : 1f;
            dropLife=enhancedFiveColor ? Mathf.Clamp(art.fiveColorDropLifetime,.2f,1.3f) : art.dropLifetime;
            fiveColorPalette=fiveColor ? LabFiveColorSparkle.FiveColors : null;
            fiveColorStartAge=0f;
            fiveColorSurface=fiveColor ? art.OrbSurface(element,true) : null;
            markLife=markSeconds;
            float contact=enhancedFiveColor ? Mathf.Clamp(art.fiveColorContactSeconds,.25f,1.5f) : art.contactSeconds;
            float press=enhancedFiveColor ? Mathf.Clamp(art.fiveColorPressSeconds,.025f,.15f) : art.pressSeconds;
            float spreadTime=enhancedFiveColor ? Mathf.Clamp(art.fiveColorSpreadSeconds,.04f,.25f) : art.spreadSeconds;
            contactLife=Mathf.Max(.25f,contact)/Mathf.Lerp(.95f,1.13f,strength);
            pressEnd=Mathf.Max(.012f,press)*Mathf.Lerp(1.12f,.80f,strength);
            spreadDuration=Mathf.Max(.025f,spreadTime)*Mathf.Lerp(1.16f,.67f,strength);
            totalLife=Mathf.Max(contactLife,markLife,dropLife+.035f);
            Age=0f;
            color=fiveColor ? LabFiveColorSparkle.ColorAt(0f,fiveColorPalette) : art.Pigment(element,monochrome);
            direction=LabFiveElementVfx.Direction(direction,Vector3.down);
            Vector3 n=LabFiveElementVfx.Direction(surfaceNormal,-direction);
            Vector3 right=Vector3.ProjectOnPlane(Camera.main != null ? Camera.main.transform.right : Vector3.right,n);
            if (right.sqrMagnitude < .001f) right=Vector3.ProjectOnPlane(Vector3.forward,n);
            if (right.sqrMagnitude < .001f) right=Vector3.ProjectOnPlane(Vector3.up,n);
            right.Normalize();
            // Keep local +Y perpendicular to the hit and X readable across the gallery.
            target=follow;
            followsTarget=target != null;
            anchor=followsTarget ? target.InverseTransformPoint(point) : point;
            normal=followsTarget ? target.localToWorldMatrix.transpose.MultiplyVector(n) : n;
            tangent=followsTarget ? target.InverseTransformVector(right) : right;
            incoming=followsTarget ? target.InverseTransformVector(direction) : direction;
            oblique=Vector3.ProjectOnPlane(direction,n).magnitude;
            spin=(seed%360u)*Mathf.Deg2Rad;
            root.localScale=Vector3.one*diameter;
            SyncSurface();
            spreadFilter.sharedMesh=shapes.splats[seed % (uint)shapes.splats.Length];
            shapes.PaintOrb(pressRenderer, art.OrbSurface(element, monochrome), color, art.wetGloss);
            shapes.Paint(spreadRenderer,color,art.wetGloss,Vector3.zero);
            spreadRenderer.enabled=false;
            dropCount=Mathf.Clamp(enhancedFiveColor ? art.fiveColorClayDrops : art.clayDrops,3,5);
            if (reduced) dropCount=Mathf.Min(3,dropCount);
            for (int i=0; i<drops.Length; ++i)
            {
                dropRenderers[i].enabled=false;
                shapes.ResetPaint(dropRenderers[i]);
                if (i>=dropCount) continue;
                float a=spin+6.283185f*(i+.15f*Mathf.Sin(spin+i*3.4f))/dropCount;
                float radial=(2.68f+.71f*Mathf.Sin(spin*3+i*2.3f))*Mathf.Lerp(.9f,1.8f,strength);
                float lift=(2.25f+.57f*Mathf.Cos(spin+i*2.1f))*Mathf.Lerp(1f,1.28f,strength);
                velocities[i]=new Vector3(radial*Mathf.Cos(a),lift,radial*Mathf.Sin(a))*art.dropSpeed;
                sizes[i]=(.22f+.08f*Mathf.Sin(spin+2.8f*i)) *
                    (enhancedFiveColor ? Mathf.Clamp(art.fiveColorDropSizeScale,1f,2f) : 1f);
                Color dropColor=fiveColor ? LabFiveColorSparkle.ColorAt(
                    (i+1)*LabClayShapes.FiveColorStepSeconds,fiveColorPalette) : color;
                shapes.Paint(dropRenderers[i],dropColor,art.wetGloss,Vector3.zero);
            }
            PrepareStars(reduced);
            root.gameObject.SetActive(true);
            Active=true;
            Tick(0f);
        }

        private void PrepareStars(bool reduced)
        {
            if (!enhancedFiveColor || !art.fiveColorStarsEnabled) return;
            int requested = Mathf.Clamp(art.fiveColorStarCount, 5, stars.Length);
            if (reduced) requested = Mathf.Min(5, requested);
            starLife = Mathf.Clamp(art.fiveColorStarLifetime, .15f, 1f);
            starSize = Mathf.Clamp(art.fiveColorStarSize, .1f, 1f);
            starReach = Mathf.Clamp(art.fiveColorStarReach, 1f, 4f);
            for (int i=0; i<requested; ++i)
            {
                if (stars[i] == null) stars[i] = shapes.CreateStar(root, "Reward star " + i);
                if (stars[i] == null) break;
                ++starCount;
            }
            if (starCount > 0) totalLife = Mathf.Max(totalLife, starLife);
        }

        private void ClearStars()
        {
            foreach (var star in stars)
            {
                if (star == null) continue;
                star.enabled = false;
                star.SetPropertyBlock(null);
            }
            starCount = 0;
            starLife = 0f;
        }

        public void TintFiveColor(float visualAge, Color32[] palette)
        {
            if (!Active || !fiveColor || float.IsNaN(visualAge) || float.IsInfinity(visualAge)) return;
            fiveColorStartAge=Mathf.Max(0f,visualAge)-Age;
            fiveColorPalette=palette != null && palette.Length>0 ? palette : LabFiveColorSparkle.FiveColors;
            Tick(0f);
        }

        private void SyncSurface()
        {
            Vector3 n=followsTarget ? target.worldToLocalMatrix.transpose.MultiplyVector(normal) : normal;
            n=LabFiveElementVfx.Direction(n,Vector3.up);
            Vector3 x=followsTarget ? target.TransformVector(tangent) : tangent;
            x=Vector3.ProjectOnPlane(x,n).normalized;
            Vector3 z=Vector3.Cross(x,n).normalized;
            root.SetPositionAndRotation(followsTarget ? target.TransformPoint(anchor) : anchor,Quaternion.LookRotation(z,n));
            Vector3 worldDirection=followsTarget ? target.TransformVector(incoming) : incoming;
            Vector3 slide = Vector3.ProjectOnPlane(worldDirection,n);
            Vector3 contactForward=slide.sqrMagnitude>.0001f ? slide.normalized : z;
            contactRotation=Quaternion.Inverse(root.rotation)*Quaternion.LookRotation(contactForward,n);
        }

        public void Tick(float dt)
        {
            Age+=dt;
            if (Age>=totalLife || (followsTarget && (!target || !target.gameObject.activeInHierarchy)))
            {
                Return();
                return;
            }
            if (followsTarget) SyncSurface();
            TickStars();
            float paletteAge=fiveColorStartAge+Age;
            if (fiveColor)
            {
                color=enhancedFiveColor && singleTone ? art.monochrome : LabFiveColorSparkle.ColorAt(paletteAge,fiveColorPalette);
                shapes.PaintFiveColorOrb(pressRenderer,fiveColorSurface,color,art.wetGloss);
            }

            float compression=Mathf.SmoothStep(0f,1f,Age/pressEnd);
            float releaseBall=1f-Mathf.SmoothStep(0f,1f,(Age-pressEnd*.83f)/(spreadDuration*.85f));
            pressRenderer.enabled=releaseBall>.008f;
            if (pressRenderer.enabled)
            {
                float height=Mathf.Lerp(1f,.31f,compression)*releaseBall;
                float width=Mathf.Lerp(1f,1.42f*pressWidthScale,compression)*releaseBall;
                pressed.localScale=new Vector3(width,height,width*(1+.12f*oblique*compression));
                pressed.localPosition=new Vector3(0f,height*.49f,.06f*oblique*compression);
                pressed.localRotation=contactRotation;
            }

            float growth=Mathf.Clamp01((Age-pressEnd*.58f)/spreadDuration);
            float wetEnd=Mathf.Max(markLife,contactLife);
            float fade=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(wetEnd-.17f,wetEnd,Age));
            spreadRenderer.enabled=growth>0f && fade>.005f;
            if (spreadRenderer.enabled)
            {
                // A fast initial push followed by deceleration, without an extra ring or another hit event.
                float curve=enhancedFiveColor ? 1f-(1f-growth)*(1f-growth)*(1f-growth) :
                    art.spread != null ? Mathf.Clamp01(art.spread.Evaluate(growth)) : growth;
                float radius=Mathf.Lerp(.28f,Mathf.Lerp(art.normalSpread,art.strongSpread,strength)*spreadScale,curve)*fade;
                float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.69f,Age));
                float h=Mathf.Lerp(1.14f,.79f,settle)*Mathf.Lerp(1f,.72f,strength)*Mathf.Sqrt(fade);
                spread.localRotation=contactRotation;
                spread.localPosition=contactRotation*new Vector3(0,.006f,.16f*curve*oblique);
                spread.localScale=new Vector3(radius,h,radius*(1+.26f*oblique));
                if (Age <= .7f || dt==0f || fiveColor)
                {
                    Vector3 down=spread.InverseTransformDirection(Vector3.down);
                    down.y=0f;
                    shapes.Paint(spreadRenderer,color,art.wetGloss,down*(settle*art.tipSag));
                }
            }

            float elapsed=Age-pressEnd*.69f;
            float life=Mathf.Max(.01f,dropLife);
            bool showDrops=elapsed>=0f && elapsed<life;
            Vector3 gravity=root.InverseTransformDirection(Vector3.down)*art.gravityDiameters;
            for (int i=0; i<dropCount; ++i)
            {
                dropRenderers[i].enabled=showDrops;
                if (!showDrops) continue;
                if (fiveColor)
                    shapes.Paint(dropRenderers[i],enhancedFiveColor ? RewardDropColor(i) : LabFiveColorSparkle.ColorAt(
                        paletteAge*LabClayShapes.FiveColorFlowSpeed+(i+1)*LabClayShapes.FiveColorStepSeconds,
                        fiveColorPalette),art.wetGloss,Vector3.zero);
                float size=sizes[i]*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(life*.57f,life,elapsed)));
                Vector3 p=velocities[i]*elapsed + gravity*(.5f*elapsed*elapsed);
                bool resting=p.y < size*.39f;
                p.y=Mathf.Max(size*.39f,p.y);
                drops[i].localPosition=p;
                drops[i].localScale= size*(resting ? new Vector3(1.18f,.67f,1.22f) : new Vector3(1.04f,.85f,1.47f));
                drops[i].localRotation=Quaternion.Euler(resting ? 0f : elapsed*(175+30*i), spin*Mathf.Rad2Deg+i*74f,0f);
            }
        }

        private void TickStars()
        {
            if (starCount == 0) return;
            bool shown = Age < starLife;
            float u = Mathf.Clamp01(Age / starLife);
            float expansion = 1f-(1f-u)*(1f-u)*(1f-u);
            float sizeEnvelope = Mathf.Lerp(.6f,1f,Mathf.Clamp01(u/.12f)) *
                (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.25f,1f,u)));
            float alpha = 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.22f,1f,u));
            for (int i=0; i<starCount; ++i)
            {
                var renderer = stars[i];
                renderer.enabled = shown;
                if (!shown) continue;
                float angle = spin + 6.283185f*(i+.12f*Mathf.Sin(spin+i*2.7f))/starCount;
                float reach = starReach*(.91f+.09f*Mathf.Sin(spin+i*1.8f));
                float distance = Mathf.Lerp(.55f,reach,expansion);
                var t = renderer.transform;
                t.localPosition = new Vector3(Mathf.Cos(angle)*distance,
                    .12f+.48f*expansion-.12f*u*u, Mathf.Sin(angle)*distance);
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one*(starSize*(.9f+.1f*Mathf.Sin(spin+i*3.1f))*sizeEnvelope);
                Color pigment = RewardDropColor(i);
                Color tint = singleTone ? pigment : Color.Lerp(pigment, Color.white, .22f);
                tint.a = alpha;
                shapes.PaintStar(renderer,tint,spin+i*.71f+(i%2==0 ? 1f : -1f)*u*.9f);
            }
        }

        private Color RewardDropColor(int index)
        {
            if (singleTone) return art.monochrome;
            // Hold the five distinct pigments for this short splash; don't change all drops into blended colors.
            int first=Mathf.FloorToInt(Mathf.Repeat(fiveColorStartAge/LabClayShapes.FiveColorStepSeconds,fiveColorPalette.Length));
            return fiveColorPalette[(first+index)%fiveColorPalette.Length];
        }

        public void Return()
        {
            if (!Active) return;
            Active=false;
            ClearStars();
            target=null;
            followsTarget=false;
            fiveColor=false; enhancedFiveColor=false; singleTone=false;
            fiveColorStartAge=0f;
            fiveColorPalette=null;
            fiveColorSurface=null;
            shapes.ResetPaint(pressRenderer);
            shapes.ResetPaint(spreadRenderer);
            foreach (var renderer in dropRenderers) shapes.ResetPaint(renderer);
            root.gameObject.SetActive(false);
        }
    }
}
