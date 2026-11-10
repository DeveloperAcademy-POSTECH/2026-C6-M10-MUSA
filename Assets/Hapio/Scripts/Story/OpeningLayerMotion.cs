using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Deterministic, cut-clock motion. Layout remains on the parent so sampling never accumulates drift.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class OpeningLayerMotion : MonoBehaviour
    {
        public enum Kind { Reveal, Sigh, Sweep, Toss, Teeter, Drop, Escape, Smoke, Cloud }
        public Kind kind;
        public float delay, seconds = .65f, phase;
        public Vector2 travel = new Vector2(0, 8);
        public float angle = 8;
        public bool fadeAtEnd;
        public float Duration => delay + (kind == Kind.Sigh ? .45f : seconds);
        RectTransform rect;
        CanvasGroup group;
        Vector2 rest;
        Vector3 scale;
        Quaternion rotation;
        Image sigh;
        public void Sample(float elapsed)
        {
            if (rect == null)
            {
                rect = (RectTransform)transform; group = GetComponent<CanvasGroup>();
                if(kind==Kind.Sigh){sigh=GetComponentInChildren<Image>();if(sigh!=null){sigh.type=Image.Type.Filled;sigh.fillMethod=Image.FillMethod.Horizontal;sigh.fillOrigin=0;}}
                rest = rect.anchoredPosition; scale = rect.localScale; rotation = rect.localRotation;
            }
            float age = Mathf.Max(0, elapsed - delay);
            float t = Mathf.Clamp01(age / Mathf.Max(.01f, seconds));
            float ease = t * t * (3 - 2 * t);
            rect.anchoredPosition = rest; rect.localScale = scale; rect.localRotation = rotation;
            group.alpha = elapsed < delay ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .35f));
            switch (kind)
            {
                case Kind.Reveal:
                    rect.anchoredPosition = rest - travel * (1 - ease);
                    group.alpha = ease;
                    break;
                case Kind.Sigh:
                    if(sigh!=null)sigh.fillAmount=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.5f));
                    rect.anchoredPosition = rest + new Vector2(-18 + 40 * ease, -5 * ease);
                    rect.localScale = Vector3.Scale(scale, new Vector3(Mathf.Lerp(.6f, 1.08f, ease), Mathf.Lerp(.8f, 1, ease), 1));
                    group.alpha *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, t));
                    break;
                case Kind.Sweep:
                    float sweep = Mathf.Sin(age * Mathf.PI * 2 / .85f + phase) * Mathf.SmoothStep(0, 1, t);
                    rect.anchoredPosition = rest + new Vector2(travel.x * sweep, Mathf.Abs(sweep) * travel.y);
                    rect.localRotation = rotation * Quaternion.Euler(0, 0, angle * sweep);
                    break;
                case Kind.Toss:
                    float flight = Mathf.Repeat(age / 2.6f + phase, 1);
                    float envelope = Mathf.SmoothStep(0, 1, t);
                    rect.anchoredPosition = rest + envelope * new Vector2(travel.x * Mathf.Sin(flight * Mathf.PI * 2), travel.y * 4 * flight * (1 - flight));
                    rect.localRotation = rotation * Quaternion.Euler(0, 0, angle * Mathf.Sin(flight * Mathf.PI * 2) * envelope);
                    break;
                case Kind.Teeter:
                    float wobble = Mathf.Sin(age * 9) * Mathf.Lerp(.25f, 1, ease);
                    rect.localRotation = rotation * Quaternion.Euler(0, 0, angle * wobble);
                    rect.anchoredPosition = rest + new Vector2(travel.x * wobble, -Mathf.Abs(wobble) * 3);
                    break;
                case Kind.Drop:
                    group.alpha = 1;
                    float fall = t * t;
                    rect.anchoredPosition = rest + travel * fall;
                    rect.localRotation = rotation * Quaternion.Euler(0, 0, angle * fall);
                    if(fadeAtEnd) group.alpha *= 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,1,t));
                    break;
                case Kind.Escape:
                    rect.anchoredPosition = rest + travel * (1 - ease) + new Vector2(-travel.x * .35f, 45) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - seconds) / 1.5f));
                    rect.localScale = scale * Mathf.Lerp(.38f, 1, ease);
                    rect.localRotation = rotation * Quaternion.Euler(0, 0, angle * (1 - ease));
                    group.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .4f));
                    break;
                case Kind.Smoke:
                    rect.anchoredPosition = rest + new Vector2(Mathf.Sin(age * 1.7f + phase) * travel.x, -travel.y * (1 - ease));
                    rect.localScale = Vector3.Scale(scale, new Vector3(1 + .025f * Mathf.Sin(age * 2 + phase), Mathf.Lerp(.92f, 1, ease), 1));
                    group.alpha = ease * (fadeAtEnd ? 1 - .45f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.2f, 2.5f, age)) : 1);
                    break;
                case Kind.Cloud:
                    rect.anchoredPosition = rest + travel * Mathf.Sin(age * .35f);
                    break;
            }
        }
    }
}
