using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #724 UT 검정 구슬 모이기 (기본): 연기를 다 닦으면 Host가 판 가운데에 검정 구슬을 준다. 이 화면은 그 구슬을 잠깐 숨기고,
    /// 판 네 쪽에서 파편 4개가 톡 나타나 하나씩 차례로 가운데 빈자리로 빨려 들어가 채워지는 모습을 보여 준 뒤 구슬을 꺼내 보인다
    /// (BeginAssembly). 플레이어가 할 일은 없다. 연기를 아직 닦는 중이면 다 걷힐 때까지 기다렸다 시작한다.
    /// #724 UT 검정 구슬 빈자리 (조합대로 파편을 받을 때): 내 판에 연기 파편이 있는 동안 판 가운데에 검정 구슬 크기의 흐린 점선 원이 떠서 숨 쉬듯 깜빡인다
    /// ("이 구슬을 채우면 된다"). 파편을 톡 탭하면 그 파편이 빈자리로 휙 빨려 들어가고, 들어간 만큼 원이 북 → 서 → 남 → 동 차례로
    /// 검게 채워진다. 네 쪽이 다 차면 Host가 그 자리에 검정 구슬을 만든다 (LabModel.SocketSootShard).
    /// 표시만 한다. 탭은 LabOrbBoard가 받아 "use" 요청으로 보낸다.
    /// </summary>
    public sealed class LabSootSocket : MonoBehaviour
    {
        public const float FlySeconds = .24f, PopSeconds = .22f, AppearSeconds = .3f;
        /// <summary>#724 UT: 빈 구슬 크기. 검정 구슬(보통 구슬의 LabModel.SootOrbSizeScale배)의 1.3배라 판 위에서 한눈에 보인다.</summary>
        public const float SizeScale = LabModel.SootOrbSizeScale * 1.3f;
        /// <summary>파편을 놓았을 때 빈 구슬에 넣은 것으로 보는 거리 (빈 구슬 반지름 배).</summary>
        public const float DropReach = 1.25f;
        private const int GhostOrder = -8, FillOrder = -7, FlyOrder = 30;
        private static Sprite ghostSprite;

        /// <summary>모이기: 파편이 나타나는 시간, 첫 파편이 날기 시작하는 때, 파편 사이 간격, 다 모인 뒤 구슬이 나오기까지.</summary>
        public const float AppearPartSeconds = .2f, FirstFlyAt = .55f, FlyGap = .28f, RevealAfter = .06f;
        private LabOrbView assemblyView;
        private string assemblyId;
        private float assemblyAge = -1f;
        private bool assemblyWaits;
        private readonly SpriteRenderer[] assemblyParts = new SpriteRenderer[4];
        private int assemblyArrived, assemblyMask;
        /// <summary>#724: one shard just landed in the socket (count so far); the reveal sound rises with it.</summary>
        public System.Action<int> PieceArrived;
        /// <summary>#724: the shards are flying together into a black orb now.</summary>
        public bool Assembling => assemblyId != null;

        private LabOrbBoard board;
        private SpriteRenderer ghost;
        private readonly SpriteRenderer[] fills = new SpriteRenderer[4];
        private bool shown;
        // #724 QA: pieces as bits (1 북 / 2 서 / 4 남 / 8 동): each sits in its own place in the empty orb.
        private int hostFilled, predicted, shownFilled;
        private float alpha, pop = -1f, predictedAt;

        public bool Shown => shown;
        private SphereCollider wall;

        // ---- #724 QA: 연결 연기 — 판 위 파편마다 가운데 빈 구슬까지 검은 연기 줄기가 이어져, 연기 알갱이가 빈 구슬 쪽으로 흘러든다 ----
        public const int TendrilPuffs = 9, MaxTendrils = 6;
        public const float TendrilFlowSeconds = 1.6f, TendrilAlpha = .42f;
        private readonly System.Collections.Generic.List<Vector3> shardPoints = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<SpriteRenderer> puffs = new System.Collections.Generic.List<SpriteRenderer>();
        private static Sprite puffSprite;
        private const int TendrilOrder = -9;
        /// <summary>How many shards have a smoke tendril to the empty orb now.</summary>
        public int Tendrils { get; private set; }

        private void DrawTendrils(Vector3 center, float diameter)
        {
            if (shown && alpha > .001f) board.CollectSootShardPoints(shardPoints); else shardPoints.Clear();
            int count = Mathf.Min(MaxTendrils, shardPoints.Count);
            Tendrils = count;
            int needed = count * TendrilPuffs;
            while (puffs.Count < needed) puffs.Add(Part("Soot Tendril Puff", PuffSprite(), TendrilOrder));
            float flow = Mathf.Repeat(Time.unscaledTime / TendrilFlowSeconds, 1f);
            float puffSize = board.OrbRadius * 1.15f;
            for (int k = 0; k < puffs.Count; k++)
            {
                SpriteRenderer puff = puffs[k];
                int tendril = k / TendrilPuffs, i = k % TendrilPuffs;
                bool on = tendril < count;
                if (puff.gameObject.activeSelf != on) puff.gameObject.SetActive(on);
                if (!on) continue;
                Vector3 from = shardPoints[tendril];
                Vector3 to = center;
                Vector3 along = to - from;
                float length = along.magnitude;
                // Stop at the empty orb's rim; nothing between a shard lying on it and the orb.
                float end = Mathf.Max(0f, length - diameter * .45f);
                // Each puff drifts towards the orb and loops; a soft sideways sway makes it read as smoke, not a rope.
                float u = Mathf.Repeat(i / (float)TendrilPuffs + flow + tendril * .13f, 1f);
                Vector3 side = length > .0001f ? new Vector3(-along.y, along.x, 0f) / length : Vector3.zero;
                float sway = Mathf.Sin((u * 3f + Time.unscaledTime * 1.3f + tendril) * Mathf.PI) * puffSize * .18f;
                Vector3 at = from + (length > .0001f ? along / length : Vector3.zero) * (end * u) + side * sway;
                puff.transform.position = new Vector3(at.x, at.y, center.z);
                float fade = Mathf.Sin(u * Mathf.PI); // thin at both ends
                float size = puffSize * Mathf.Lerp(1.1f, .6f, u);
                puff.transform.localScale = Vector3.one * (size / Mathf.Max(.0001f, puff.sprite.bounds.size.x));
                puff.transform.localRotation = Quaternion.Euler(0f, 0f, (u * 140f + tendril * 47f + i * 31f) % 360f);
                puff.color = new Color(.08f, .07f, .1f, TendrilAlpha * alpha * fade);
            }
        }

        /// <summary>A soft, slightly lumpy round of smoke (white; tinted ink).</summary>
        private static Sprite PuffSprite()
        {
            if (puffSprite != null) return puffSprite;
            const int size = 64;
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = (px + .5f) / size * 2f - 1f, y = (py + .5f) / size * 2f - 1f;
                    float angle = Mathf.Atan2(y, x);
                    float edge = .78f + .1f * Mathf.Sin(angle * 3f) + .06f * Mathf.Sin(angle * 5f + 1.3f);
                    float r = Mathf.Sqrt(x * x + y * y) / edge;
                    float a = Mathf.Clamp01(1f - r);
                    pixels[py * size + px] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab soot tendril puff", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            puffSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            puffSprite.name = texture.name;
            puffSprite.hideFlags = HideFlags.DontSave;
            return puffSprite;
        }

        /// <summary>#724: whether a finger let go over the empty orb (a shard dropped there flies in).</summary>
        public bool Covers(Vector2 worldPoint)
        {
            if (!shown || board == null) return false;
            Vector3 center = board.WorldPointFor(LabModel.SootSocketCenter);
            float radius = board.OrbRadius * SizeScale; // half the empty orb's width
            return (worldPoint - new Vector2(center.x, center.y)).sqrMagnitude <= Mathf.Pow(radius * DropReach, 2f);
        }
        /// <summary>Pieces drawn filled now (bits: the Host's plus shards still flying in).</summary>
        public int FilledShown => shownFilled;

        public static LabSootSocket For(LabOrbBoard board)
        {
            LabSootSocket socket = board.GetComponentInChildren<LabSootSocket>(true);
            if (socket != null) return socket;
            var item = new GameObject("Soot Socket");
            item.layer = board.gameObject.layer;
            item.transform.SetParent(board.transform, false);
            socket = item.AddComponent<LabSootSocket>();
            socket.board = board;
            return socket;
        }

        /// <summary>From the snapshot: whether to show the empty orb, and which pieces (bits) the Host has put in it.</summary>
        public void Set(bool show, int filled)
        {
            shown = show;
            filled &= LabModel.SootWholePieces;
            if (filled != hostFilled) { hostFilled = filled; predicted &= ~filled; }
            if (!show) predicted = 0;
        }

        /// <summary>A shard was tapped: a copy of it flies into the socket, and the socket fills one step on arrival.</summary>
        public void PlayFlyIn(LabOrbView shard)
        {
            if (shard == null) return;
            if (board == null) board = GetComponentInParent<LabOrbBoard>();
            int pieces = shard.SootPieces > 0 ? shard.SootPieces : 1;
            var part = new GameObject("Soot Shard Fly In");
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = LabSootShardArt.SpriteFor(pieces, false);
            renderer.sortingOrder = FlyOrder;
            var fly = part.AddComponent<FlyIn>();
            // The picture starts exactly where the shard is drawn (its whole-orb picture, shifted so the piece is on the body)
            // and ends as the empty orb's picture, so the piece lands in its own place.
            Vector2 centroid = LabSootShardArt.Centroid(pieces);
            float width = shard.ShardArtWidth;
            Vector3 start = shard.transform.position - new Vector3(centroid.x, centroid.y, 0f) * width * .5f;
            fly.Begin(this, start, pieces, width);
        }

        /// <summary>
        /// #724: a black orb from the smoke appeared on my board: hide it (and hold it still) and play its four shards flying in.
        /// waitForSmoke keeps the show until the smoke over the board is gone.
        /// </summary>
        public void BeginAssembly(LabOrbView view, bool waitForSmoke)
        {
            if (view == null) return;
            if (board == null) board = GetComponentInParent<LabOrbBoard>();
            if (assemblyId == view.OrbId) { assemblyWaits = waitForSmoke && assemblyAge < 0f; return; }
            FinishAssembly(); // a second one replaces the first (it shows at once)
            assemblyView = view;
            assemblyId = view.OrbId;
            assemblyAge = -1f;
            assemblyArrived = 0;
            assemblyMask = 0;
            assemblyWaits = waitForSmoke;
            view.SetAssemblyHidden(true);
            if (board != null) board.SetOrbLocked(assemblyId, true);
        }

        /// <summary>#724: the smoke over the board is still being wiped (the shards wait under it).</summary>
        public void HoldAssembly(bool waiting)
        {
            if (assemblyId != null && assemblyAge < 0f) assemblyWaits = waiting;
        }

        private void FinishAssembly()
        {
            if (assemblyId == null) return;
            for (int i = 0; i < assemblyParts.Length; i++) if (assemblyParts[i] != null) assemblyParts[i].gameObject.SetActive(false);
            if (assemblyView != null)
            {
                assemblyView.SetAssemblyHidden(false);
                if (assemblyView.isActiveAndEnabled) assemblyView.PlaySpawnPop(Vector3.zero, .55f, .25f);
                if (board != null)
                {
                    board.SetOrbLocked(assemblyId, false);
                    board.NotifySootJoined(assemblyView, true);
                }
            }
            assemblyView = null;
            assemblyId = null;
            assemblyAge = -1f;
        }

        private void DriveAssembly(float dt)
        {
            if (assemblyId == null) return;
            if (assemblyView == null) { assemblyId = null; return; } // thrown or gone: nothing to show
            if (assemblyAge < 0f)
            {
                if (assemblyWaits) return;
                assemblyAge = 0f;
            }
            assemblyAge += dt;
            float diameter = board.OrbRadius * 2f * SizeScale;
            Vector3 center = board.WorldPointFor(LabModel.SootSocketCenter);
            for (int i = 0; i < 4; i++)
            {
                if (assemblyParts[i] == null)
                {
                    assemblyParts[i] = Part("Soot Shard " + i, LabSootShardArt.SpriteFor(1 << i, false), FlyOrder);
                }
                SpriteRenderer part = assemblyParts[i];
                float appear = Mathf.Clamp01((assemblyAge - i * .06f) / AppearPartSeconds);
                float flyStart = FirstFlyAt + i * FlyGap;
                float t = Mathf.Clamp01((assemblyAge - flyStart) / FlySeconds);
                bool landed = assemblyAge >= flyStart + FlySeconds;
                part.gameObject.SetActive(appear > 0f && !landed);
                if (landed)
                {
                    if (assemblyArrived <= i)
                    {
                        assemblyArrived = i + 1;
                        assemblyMask |= 1 << i;
                        pop = 0f;
                        PieceArrived?.Invoke(assemblyArrived);
                    }
                    continue;
                }
                // Each shard pops up a little way out from its own side of the socket, then is sucked in (speeding up).
                Vector3 home = board.WorldPointFor(LabModel.JigwiSootShardSpot(i));
                Vector3 from = Vector3.Lerp(center, home, .55f);
                float eased = t * t;
                part.transform.position = Vector3.LerpUnclamped(from, center, eased);
                float overshoot = appear < 1f ? Mathf.Sin(appear * Mathf.PI) * .25f : 0f;
                Fit(part, diameter * (appear + overshoot));
                part.transform.position = new Vector3(part.transform.position.x, part.transform.position.y, center.z);
                part.color = Color.white;
            }
            if (assemblyAge >= FirstFlyAt + 3 * FlyGap + FlySeconds + RevealAfter) FinishAssembly();
        }

        private void Arrived(int pieces)
        {
            predicted |= pieces & ~hostFilled;
            predictedAt = Time.unscaledTime;
            pop = 0f;
        }

        private void LateUpdate()
        {
            if (board == null) board = GetComponentInParent<LabOrbBoard>();
            float dt = Time.unscaledDeltaTime;
            // A prediction the Host never confirmed (refused) gives up after a moment.
            if (predicted > 0 && Time.unscaledTime - predictedAt > 1.5f) predicted = 0;
            if (board != null) DriveAssembly(dt);
            bool assemblingNow = assemblyId != null && assemblyAge >= 0f;
            alpha = Mathf.MoveTowards(alpha, shown || assemblingNow ? 1f : 0f, dt / AppearSeconds);
            // The empty orb sits still in the middle: other orbs bounce off it like off a wall.
            if (wall == null)
            {
                wall = gameObject.AddComponent<SphereCollider>();
                wall.enabled = false;
            }
            bool solid = shown || assemblyId != null;
            if (wall.enabled != solid) wall.enabled = solid;
            if (board != null) wall.radius = board.OrbRadius * SizeScale * .94f / Mathf.Max(.0001f, transform.lossyScale.x);
            if (alpha <= .001f)
            {
                foreach (SpriteRenderer puff in puffs) if (puff.gameObject.activeSelf) puff.gameObject.SetActive(false);
                Tendrils = 0;
                if (ghost != null && ghost.gameObject.activeSelf)
                {
                    ghost.gameObject.SetActive(false);
                    foreach (SpriteRenderer piece in fills) piece.gameObject.SetActive(false);
                }
                return;
            }
            if (board == null) return;
            EnsureParts();
            ghost.gameObject.SetActive(true);
            shownFilled = assemblingNow ? assemblyMask : (hostFilled | predicted) & LabModel.SootWholePieces;

            Vector3 center = board.WorldPointFor(LabModel.SootSocketCenter);
            transform.position = center;
            float diameter = board.OrbRadius * 2f * SizeScale;
            float popScale = 1f;
            if (pop >= 0f)
            {
                pop += dt;
                float t = Mathf.Clamp01(pop / PopSeconds);
                popScale = 1f + .18f * Mathf.Sin(t * Mathf.PI);
                if (pop >= PopSeconds) pop = -1f;
            }
            // While still empty the dashed ring breathes so the eye finds it.
            float breathe = shownFilled == 0 ? .5f + .5f * Mathf.Sin(Time.unscaledTime * 3.2f) : 1f;
            DrawTendrils(center, diameter);
            Fit(ghost, diameter * (1f + .05f * (1f - breathe)) * popScale);
            for (int q = 0; q < 4; q++)
            {
                fills[q].gameObject.SetActive((shownFilled & (1 << q)) != 0);
                Fit(fills[q], diameter * popScale);
                fills[q].color = new Color(1f, 1f, 1f, alpha);
            }
            ghost.color = new Color(1f, 1f, 1f, alpha * Mathf.Lerp(.75f, 1f, breathe));
        }

        private static void Fit(SpriteRenderer renderer, float diameter)
        {
            if (renderer.sprite == null) return;
            float width = Mathf.Max(.0001f, renderer.sprite.bounds.size.x);
            renderer.transform.localScale = Vector3.one * (diameter / width);
            renderer.transform.localPosition = Vector3.zero;
        }

        /// <summary>World width the socket's art covers (for the flying shard to shrink into).</summary>
        private float ArtWidth => (board != null ? board.OrbRadius : .3f) * 2f * SizeScale;

        private void EnsureParts()
        {
            if (ghost != null) return;
            ghost = Part("Soot Socket Ring", GhostSprite(), GhostOrder);
            for (int q = 0; q < 4; q++) fills[q] = Part("Soot Socket Piece " + q, LabSootShardArt.SpriteFor(1 << q, false), FillOrder);
        }

        private SpriteRenderer Part(string label, Sprite sprite, int order)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>
        /// #724 UT: the empty orb: a see-through glassy shell with a dark ink rim, a soft shine top left, and the four cracks
        /// (diagonals, where the shards meet) faintly drawn, so it reads as "a black orb, not filled yet".
        /// </summary>
        private static Sprite GhostSprite()
        {
            if (ghostSprite != null) return ghostSprite;
            const int size = 160;
            var pixels = new Color[size * size];
            Color ink = new Color(.1f, .09f, .13f), body = new Color(.32f, .29f, .38f), shine = new Color(1f, 1f, 1f);
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = (px + .5f) / size * 2f - 1f, y = (py + .5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float inside = Mathf.Clamp01((.94f - r) * size * .5f);
                    // A brushed ink rim: its width wobbles round the circle like the shards' rough edges.
                    float wob = .045f * (1f + .45f * Mathf.Sin(Mathf.Atan2(y, x) * 7f + .6f) + .3f * Mathf.Sin(Mathf.Atan2(y, x) * 13f + 2.1f));
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(r - .9f) / Mathf.Max(.02f, wob)) * Mathf.Clamp01((.97f - r) * size * .5f);
                    // Glassy: darker towards the rim, clear in the middle.
                    float shade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.3f, .9f, r));
                    // #724 QA: the cracks follow the shards' own zigzag broken edges (LabSootShardArt), not straight lines.
                    float crack = 0f;
                    if (r > .06f && r < .94f)
                    {
                        float angle = Mathf.Repeat(Mathf.Atan2(y, x) * Mathf.Rad2Deg, 360f);
                        for (int face = 0; face < 4; face++)
                        {
                            float d = Mathf.Abs(Mathf.DeltaAngle(angle, LabSootShardArt.CrackAngle(r, face))) * Mathf.Deg2Rad * r;
                            crack = Mathf.Max(crack, Mathf.Clamp01(1f - d / .022f));
                        }
                        crack *= inside;
                    }
                    float glint = Mathf.Clamp01(1f - new Vector2(x + .38f, y - .42f).magnitude / .22f);
                    Color c = Color.Lerp(body, ink, shade);
                    float a = inside * (.18f + .3f * shade) + crack * .55f;
                    c = Color.Lerp(c, ink, crack * .8f);
                    c = Color.Lerp(c, shine, glint * .8f);
                    a = Mathf.Max(a, glint * .55f * inside);
                    c = Color.Lerp(c, ink, rim);
                    a = Mathf.Max(a, rim * .95f);
                    pixels[py * size + px] = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab soot empty orb", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            ghostSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            ghostSprite.name = "Lab soot empty orb";
            ghostSprite.hideFlags = HideFlags.DontSave;
            return ghostSprite;
        }

        /// <summary>The copy of a tapped shard flying into the socket (speeding up, shrinking to the socket's size).</summary>
        private sealed class FlyIn : MonoBehaviour
        {
            private LabSootSocket socket;
            private Vector3 from;
            private int pieces;
            private float age, startWidth;
            private SpriteRenderer image;

            public void Begin(LabSootSocket owner, Vector3 start, int pieceBits, float width)
            {
                socket = owner;
                from = start;
                pieces = pieceBits;
                image = GetComponent<SpriteRenderer>();
                startWidth = Mathf.Max(.01f, width);
                transform.position = start;
                Fit(image, startWidth);
            }

            private void Update()
            {
                age += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(age / FlySeconds);
                float eased = t * t; // speeds up like being sucked in
                Vector3 to = socket != null ? socket.transform.position : from;
                transform.position = Vector3.LerpUnclamped(from, to, eased) + Vector3.up * Mathf.Sin(t * Mathf.PI) * startWidth * .12f;
                Fit(image, Mathf.Lerp(startWidth, socket != null ? socket.ArtWidth : startWidth, eased));
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
                if (t < 1f) return;
                if (socket != null) socket.Arrived(pieces);
                Destroy(gameObject);
            }
        }
    }
}
