using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// #540 지귀 A "다 태워버리겠어" 불덩이 (그 자리 사람 전투 화면에만). 예고 말풍선 그림처럼 굵은 먹색 테두리의 만화풍 2D 불꽃이다.
    /// 판의 구슬은 쓰는 동안 건드리지 않고, 남은 시간은 박 위 불덩이 크기로 보인다.
    /// - 예고(2초): 박 위에 불씨가 탁 튀고 작은 불덩이로 붙는다 (Kindle).
    /// 불꽃은 모두 코드로 그린다(`LabProceduralFire`).
    /// - A 동안: 불덩이가 점점 커지며 더 빠르게 일렁이고, 끝이 다가오면 떨린다 (Grow).
    /// - 다 쓰면: 불덩이가 그 자리에 그대로 멈추고(흔들림·불꽃 움직임 없음) 푸른 가루와 함께 조용히 사라진다 (Douse).
    /// - 못 쓰고 끝나면: 불덩이가 화면 아래(내 조합판)로 떨어진다 (Fall). 그때 판의 구슬이 모두 탄다(Host 규칙).
    /// 시각은 Host 스냅샷의 A 시각과 내 자리 `talismanAt`만 쓴다. 화면 표시만 하며 판정·물리와 무관하다.
    /// </summary>
    public sealed class LabJigwiFireball : MonoBehaviour
    {
        public enum Phase { None, Kindle, Grow, Douse, Fall }
        public const float DouseSeconds = .7f;
        /// <summary>예고가 시작되고 불씨가 붙기까지(초). 지귀 목소리 "타올라라"가 터지는 때에 맞춘다.</summary>
        public const float KindleDelaySeconds = .7f;
        /// <summary>불꽃이 일렁이고 불티가 오르는 빠르기 (1 = 처음 만든 빠르기).</summary>
        public const float FireSpeed = 1.3f;
        public const float FallSeconds = (float)LabModel.JigwiTalismanFailBurnDelaySeconds;
        private const int SparkCount = 6;
        /// <summary>끝에 가까울수록 불꽃이 붉게 달아오르는 색 (곱함).</summary>
        private static readonly Color Hot = new Color(1f, .62f, .5f, 1f);
        private SpriteRenderer body;
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[SparkCount];
        private Camera view;
        private float frozenTime = -1f, frozenSize;
        private bool dousedBurst;

        public Phase Shown { get; private set; }

        /// <summary>
        /// 이 자리 사람의 불덩이 단계와 그 단계 안 진행(0~1). `size`는 불덩이 크기(0~1, 다 쓴 순간·끝나는 순간의 크기 포함).
        /// </summary>
        public static Phase Evaluate(LabSnapshot snapshot, ulong localPlayer, double now, out float progress, out float size)
        {
            progress = size = 0f;
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.rounds == null) return Phase.None;
            foreach (LabJigwiABRound round in snapshot.jigwiAB.rounds)
            {
                if (round == null || round.kind != LabJigwiABKind.PersonalBurn) continue;
                LabJigwiABPlayerFire seat = null;
                foreach (LabJigwiABPlayerFire item in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    if (item != null && item.playerId == localPlayer) seat = item;
                if (seat == null) continue;
                double span = Math.Max(.01d, round.endsAt - round.activeAt);
                if (seat.talismanAt >= 0d)
                {
                    double age = now - seat.talismanAt;
                    if (age < 0d || age >= DouseSeconds) continue;
                    progress = (float)(age / DouseSeconds);
                    size = Grown((float)((seat.talismanAt - round.activeAt) / span));
                    return Phase.Douse;
                }
                if (round.Warning(now))
                {
                    double lit = Math.Min(round.warningAt + KindleDelaySeconds, round.activeAt - .3d);
                    if (now < lit) return Phase.None;
                    progress = Mathf.Clamp01((float)((now - lit) / Math.Max(.01d, round.activeAt - lit)));
                    size = Grown(0f) * Mathf.SmoothStep(0f, 1f, progress);
                    return Phase.Kindle;
                }
                if (round.Active(now))
                {
                    progress = Mathf.Clamp01((float)((now - round.activeAt) / span));
                    size = Grown(progress);
                    return Phase.Grow;
                }
                if (round.resolution == LabJigwiABResolution.Expired && round.resolvedAt >= 0d)
                {
                    double age = now - round.resolvedAt;
                    if (age < 0d || age >= FallSeconds) continue;
                    progress = (float)(age / FallSeconds);
                    size = 1f;
                    return Phase.Fall;
                }
            }
            return Phase.None;
        }

        /// <summary>A 진행(0~1) → 불덩이 크기(0~1). 처음 작은 불씨에서 시작해 초반부터 빠르게 커진다(절반쯤에 이미 3/4 크기).</summary>
        public static float Grown(float t) => Mathf.Lerp(.27f, 1f, Mathf.Pow(Mathf.Clamp01(t), .6f)); // #540 starts 1.5× the first spark

        public static LabJigwiFireball Create()
        {
            var root = new GameObject("Jigwi Fireball (#540)");
            var fire = root.AddComponent<LabJigwiFireball>();
            fire.body = fire.Part("Fireball Body", LabProceduralFire.CartoonFlame(0), Color.white, 39);
            for (int i = 0; i < SparkCount; i++)
                fire.sparks[i] = fire.Part("Fireball Spark", LabProceduralFire.CartoonSpark(), Color.white, 40);
            RenderPipelineManager.beginCameraRendering += fire.Face;
            fire.Hide();
            return fire;
        }

        private SpriteRenderer Part(string label, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }

        public void Hide()
        {
            Shown = Phase.None;
            frozenTime = -1f; dousedBurst = false;
            if (body == null) return;
            foreach (SpriteRenderer part in GetComponentsInChildren<SpriteRenderer>(true)) part.enabled = false;
        }

        /// <summary>
        /// 한 프레임 그린다. time은 A 시각(초)이며 다 쓴 뒤에는 멈춘다. 위치와 크기는 전투 카메라 화면 기준이라
        /// 박 모양·크기와 상관없이 박 바로 위, 전투 화면 높이의 일정 비율로 보인다.
        /// </summary>
        public void Show(Phase phase, float progress, float size, LabTarget gourd, Camera battle, float time)
        {
            if (phase == Phase.None || gourd == null) { Hide(); return; }
            view = battle != null && battle.isActiveAndEnabled ? battle : Camera.main;
            if (view == null) { Hide(); return; }
            if (phase != Phase.Douse) { frozenTime = -1f; dousedBurst = false; }
            Shown = phase;
            time *= FireSpeed;

            Collider hitbox = gourd.Hitbox;
            Vector3 center = hitbox != null ? LabHangingTarget.DrawnPoint(hitbox, hitbox.bounds.center) : gourd.transform.position;
            Vector3 seen = view.WorldToViewportPoint(center);
            if (seen.z <= view.nearClipPlane) { Hide(); return; }
            // Just above the gourd on the battle view, a little in front of it.
            float depth = seen.z * .85f;
            Vector3 hover = view.ViewportToWorldPoint(new Vector3(Mathf.Clamp(seen.x, .25f, .75f), Mathf.Clamp(seen.y + .24f, .3f, .8f), depth));
            float viewHeight = view.orthographic ? view.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float scale = viewHeight * .16f;
            float radius = scale * size;
            float alpha = 1f;

            // The fire stops dead when the talisman is done: time freezes, the ball fades and shrinks a touch.
            if (phase == Phase.Douse)
            {
                if (frozenTime < 0f) { frozenTime = time; frozenSize = radius; }
                time = frozenTime;
                radius = frozenSize * Mathf.Lerp(1f, .85f, progress);
                alpha = 1f - progress * progress * (3f - 2f * progress);
                if (!dousedBurst)
                {
                    dousedBurst = true;
                    LabFiveColorSparkle.Burst(hover, Mathf.Max(.05f, radius * .5f), LabFiveColorSparkle.BlueTones);
                }
            }

            Vector3 position = hover;
            if (phase == Phase.Kindle)
            {
                // A spark snaps alight, then catches into a small ball.
                float snap = Mathf.Clamp01(progress / .15f);
                radius *= 1f + .5f * Mathf.Exp(-progress * 10f) * snap;
                alpha = snap;
            }
            else if (phase == Phase.Grow)
            {
                // Faster, bigger breathing as time runs out; it trembles near the end.
                float rate = Mathf.Lerp(1.5f, 7f, progress);
                radius *= 1f + Mathf.Lerp(.04f, .12f, progress) * Mathf.Sin(time * rate * Mathf.PI * 2f);
                float shake = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.75f, 1f, progress)) * scale * .08f;
                position += view.transform.right * (Mathf.PerlinNoise(time * 23f, 1.3f) - .5f) * 2f * shake
                    + view.transform.up * (Mathf.PerlinNoise(4.1f, time * 23f) - .5f) * 2f * shake;
            }
            else if (phase == Phase.Fall)
            {
                // Drops at the camera and out the bottom of the battle view onto the board.
                Vector3 exit = view.ViewportToWorldPoint(new Vector3(.5f, -.3f, depth * .5f));
                float e = progress * progress;
                position = Vector3.Lerp(hover, exit, e);
                radius *= Mathf.Lerp(1f, .8f, e); // nearer the camera, so it still looks bigger
                alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.85f, 1f, progress));
            }
            transform.position = position;
            transform.localScale = Vector3.one;

            float heat = phase == Phase.Grow ? progress : phase == Phase.Kindle ? 0f : 1f;
            // #540: a flat cartoon flame with a thick ink outline. Frames swap on a beat and it squashes
            // and stretches, so it flickers like a hand-drawn fire; near the end it glows redder.
            body.sprite = LabProceduralFire.CartoonFlame((int)(time * Mathf.Lerp(6f, 12f, heat)));
            float stretch = 1f + Mathf.Lerp(.05f, .1f, heat) * Mathf.Sin(time * Mathf.Lerp(9f, 18f, heat));
            float width = radius * 2.6f;
            Vector2 bodySize = body.sprite.bounds.size;
            float unit = width / Mathf.Max(.0001f, bodySize.x);
            body.transform.localScale = new Vector3(unit / Mathf.Sqrt(stretch), unit * stretch, 1f);
            body.transform.localPosition = Vector3.zero;
            Tint(body, Color.Lerp(Color.white, Hot, heat * heat * .6f), alpha);

            for (int i = 0; i < SparkCount; i++)
            {
                // Little ink-ringed sparks pop off the flame and drift up.
                SpriteRenderer spark = sparks[i];
                float t = Mathf.Repeat(time * Mathf.Lerp(.8f, 1.5f, heat) + i * .61803398875f, 1f);
                float side = Mathf.Sin(i * 2.4f + Mathf.Floor(time * Mathf.Lerp(.8f, 1.5f, heat) + i * .61803398875f) * 1.7f);
                spark.transform.localPosition = new Vector3(side * radius * .9f + Mathf.Sin(t * 6f + i) * radius * .15f,
                    radius * (.4f + 1.4f * t), -.02f);
                float sparkSize = radius * .22f * (1f - .6f * t);
                spark.transform.localScale = Vector3.one * sparkSize / Mathf.Max(.0001f, spark.sprite.bounds.size.x);
                Tint(spark, Color.white, alpha * (1f - t * t) * Mathf.Lerp(.5f, 1f, heat));
            }
        }

        private static void Tint(SpriteRenderer renderer, Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            renderer.color = color;
            renderer.enabled = color.a > .002f;
        }

        private void Face(ScriptableRenderContext context, Camera camera)
        {
            if (this == null || camera == null || camera != view) return;
            transform.rotation = camera.transform.rotation;
        }

        private void OnDestroy() => RenderPipelineManager.beginCameraRendering -= Face;
    }
}
