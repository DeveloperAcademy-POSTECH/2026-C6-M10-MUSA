using UnityEngine;

namespace C6Lab
{
    internal sealed class LabClayFlightSlot
    {
        private readonly Transform root, ball, neck;
        private readonly Transform[] drops = new Transform[3], wisps = new Transform[2];
        private readonly MeshRenderer[] renderers = new MeshRenderer[7];
        private readonly LabClayShapes shapes;
        private readonly LabFiveElementProfile art;
        // The short tail samples distance along the actual game path (including a curved throw).
        private readonly Vector3[] history = new Vector3[32];
        private int head, count;
        private Vector3 position, forward;
        private float age, phase;
        private bool lowDetail;
        private Color32[] fiveColorPalette;
        private float fiveColorAge;
        public int Version { get; private set; }
        public bool Active { get; private set; }
        public float Diameter { get; private set; }
        public float Strength { get; private set; }
        public LabOrbElement Element { get; private set; }
        public bool Monochrome { get; private set; }
        public bool FiveColor { get; private set; }
        public float FiveColorAge => fiveColorAge;
        public Color32[] FiveColorPalette => fiveColorPalette;

        public LabClayFlightSlot(Transform parent, int index, LabClayShapes shapes, LabFiveElementProfile art)
        {
            this.shapes = shapes;
            this.art = art;
            root = new GameObject("Flight " + index).transform;
            root.SetParent(parent, false);
            root.gameObject.SetActive(false);
            renderers[0]=shapes.Create(root,"Wet clay",shapes.ball); ball=renderers[0].transform;
            renderers[0].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            renderers[1]=shapes.Create(root,"Pulling neck",shapes.streak); neck=renderers[1].transform;
            for (int i=0; i<drops.Length; ++i)
            {
                renderers[i+2]=shapes.Create(root,"Breaking drop " + i,shapes.ball);
                drops[i]=renderers[i+2].transform;
            }
            for (int i=0; i<wisps.Length; ++i)
            {
                renderers[i+5]=shapes.Create(root,"Pigment streak " + i,shapes.streak);
                wisps[i]=renderers[i+5].transform;
            }
        }

        public void Begin(LabOrbElement element, Vector3 start, Vector3 direction, float diameter,
            float strength, bool mono, uint seed, bool reduced, bool fiveColor = false)
        {
            Version = unchecked(Version+1);
            Active = true;
            Element = element;
            Diameter = diameter;
            Strength = strength;
            Monochrome = mono;
            FiveColor = fiveColor;
            fiveColorAge = 0f;
            fiveColorPalette = fiveColor ? LabFiveColorSparkle.FiveColors : null;
            lowDetail = reduced;
            position = start;
            forward = LabFiveElementVfx.Direction(direction, Vector3.forward);
            history[0] = start;
            head=0; count=1; age=0f;
            phase = (seed % 1021u) / 1021f;
            root.position=start;
            root.rotation=Quaternion.identity;
            root.localScale=Vector3.one;
            Color pigment=art.Pigment(element,mono);
            Color streakColor=Color.Lerp(pigment,mono ? Color.white : ElementAccent(element),.52f);
            shapes.PaintOrb(renderers[0], art.OrbSurface(element, mono), pigment, art.wetGloss);
            for (int i=1; i<renderers.Length; ++i)
                shapes.Paint(renderers[i],i < 5 ? pigment : streakColor,art.wetGloss,Vector3.zero);
            for (int i=0; i<drops.Length; ++i) drops[i].gameObject.SetActive(!lowDetail || i==0);
            root.gameObject.SetActive(true);
            Tick(0);
        }

        public bool TintFiveColor(float visualAge, Color32[] palette)
        {
            if (!Active || !FiveColor || float.IsNaN(visualAge) || float.IsInfinity(visualAge)) return false;
            fiveColorAge = Mathf.Max(0f, visualAge);
            fiveColorPalette = palette != null && palette.Length > 0 ? palette : LabFiveColorSparkle.FiveColors;
            PaintFiveColor();
            return true;
        }

        private void PaintFiveColor()
        {
            shapes.PaintFiveColorOrb(renderers[0], art.OrbSurface(Element, true),
                LabFiveColorSparkle.ColorAt(fiveColorAge, fiveColorPalette), art.wetGloss);
            shapes.Paint(renderers[1], TailColor(.25f), art.wetGloss, Vector3.zero);
            for (int i=0; i<drops.Length; ++i)
            {
                float p=Mathf.Repeat(age*3.7f+phase+i*.338f,1f);
                shapes.Paint(renderers[i+2], TailColor(p), art.wetGloss, Vector3.zero);
            }
            for (int i=0; i<wisps.Length; ++i)
                shapes.Paint(renderers[i+5], TailColor(.4f+.3f*i), art.wetGloss, Vector3.zero);
        }

        private Color TailColor(float distance)
        {
            // Match the old five-key palette flow, sampled on the existing clay tail pieces.
            float seconds=fiveColorAge*LabClayShapes.FiveColorFlowSpeed +
                (1f-Mathf.Clamp01(distance))*4f*LabClayShapes.FiveColorStepSeconds;
            return LabFiveColorSparkle.ColorAt(seconds, fiveColorPalette);
        }

        private static Color ElementAccent(LabOrbElement element)
        {
            switch (element)
            {
                case LabOrbElement.Fire: return new Color(1f,.69f,.11f);
                case LabOrbElement.Water: return new Color(.24f,.91f,1f);
                case LabOrbElement.Wood: return new Color(.74f,.96f,.21f);
                case LabOrbElement.Metal: return new Color(1f,.9f,.71f);
                default: return new Color(1f,.8f,.35f);
            }
        }

        public void Move(Vector3 next, Vector3 direction)
        {
            Vector3 delta = next-position;
            forward = LabFiveElementVfx.Direction(direction, LabFiveElementVfx.Direction(delta, forward));
            // Keep a minimum distance between samples so 120 Hz and low-speed shots still retain the tail.
            if ((next-history[head]).sqrMagnitude > Diameter*Diameter*.004f)
            {
                head=(head+1)%history.Length;
                history[head]=next;
                count=Mathf.Min(count+1,history.Length);
            }
            position=next;
            root.position=next;
        }

        private Vector3 Back(float diameters)
        {
            float remaining = diameters*Diameter;
            Vector3 p = position;
            for (int i=0; i<count; ++i)
            {
                int previous=(head-i+history.Length)%history.Length;
                Vector3 q=history[previous];
                float length=Vector3.Distance(p,q);
                if (length>.00001f && length>=remaining) return Vector3.Lerp(p,q,remaining/length);
                remaining -= length;
                p=q;
            }
            // Grow from the launch point. Never stretch all the way to an old lease's position.
            return p;
        }

        public void Tick(float dt)
        {
            age+=dt;
            if (FiveColor) fiveColorAge+=dt;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            right = LabFiveElementVfx.Direction(right, Vector3.right);
            Vector3 up = Vector3.Cross(forward,right).normalized;
            Quaternion flight = Quaternion.LookRotation(forward,up);
            float stretch = .23f*(1f-Mathf.SmoothStep(0,1,age/Mathf.Max(.01f,art.launchStretchSeconds)));
            float squeeze=1f/Mathf.Sqrt(1f+stretch);
            ball.localPosition=Vector3.zero;
            // Rotate the hand-shaped surface around its long axis while preserving launch squash.
            ball.rotation=flight*Quaternion.AngleAxis(360f*phase+age*210f,Vector3.forward);
            ball.localScale=Diameter*new Vector3(squeeze,squeeze,1+stretch);
            float wave=.5f+.5f*Mathf.Sin((age*6.5f+phase)*Mathf.PI*2);
            float tail=art.trailDiameters;
            Vector3 neckHead=Back(.24f);
            Vector3 neckEnd=Back(Mathf.Min(tail,.6f+.26f*wave));
            Stretch(neck,neckHead,neckEnd,Diameter*(.26f-.075f*wave),forward);
            for (int i=0; i<drops.Length; ++i)
            {
                if (lowDetail && i>0) continue;
                float p=Mathf.Repeat(age*3.7f+phase+i*.338f,1f);
                Vector3 track=Back(Mathf.Lerp(.57f,tail+.24f,p));
                drops[i].position=track + Vector3.down*(Diameter*.23f*p*p)
                    + right*(Diameter*.055f*Mathf.Sin(phase*5+i*2));
                float size=Diameter*Mathf.Lerp(.23f,.042f,p);
                drops[i].localScale=new Vector3(size,size,size*(1.5f-.4f*p));
                drops[i].rotation=flight;
            }
            for (int i=0; i<wisps.Length; ++i)
            {
                float side = i==0 ? -1f : 1f;
                Vector3 offset=(right*side + up*.42f)*Diameter*.22f;
                Vector3 front=Back(.38f+.14f*i) + offset;
                Vector3 end=Back(tail*(.8f+.12f*side)+wave*.1f) + offset*.7f + up*(.05f*Diameter);
                Stretch(wisps[i],front,end,Diameter*(.114f-.022f*i),forward);
            }
            if (FiveColor) PaintFiveColor();
        }

        private static void Stretch(Transform t, Vector3 front, Vector3 back, float width, Vector3 forward)
        {
            Vector3 delta = front-back;
            t.position=(front+back)*.5f;
            t.rotation=Quaternion.LookRotation(LabFiveElementVfx.Direction(delta,forward));
            t.localScale=new Vector3(width,width,delta.magnitude);
        }

        public void Return()
        {
            if (!Active) return;
            Active=false; count=0; head=0; age=0f;
            FiveColor=false; fiveColorAge=0f; fiveColorPalette=null;
            foreach (var renderer in renderers) shapes.ResetPaint(renderer);
            root.gameObject.SetActive(false);
        }
    }
}
