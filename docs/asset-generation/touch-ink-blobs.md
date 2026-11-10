# 터치 먹물 번짐 그림 생성 기록 (#587)

`Assets/Hapio/Art/UI/TouchInk/TouchInkBlob01~08.png`는 외부 이미지 없이 아래 코드로 Unity Editor에서 만든 그림이다. 참고 이미지는 분위기를 맞추는 데만 봤고, 픽셀을 가져오거나 저장소에 넣지 않았다.

- **seed:** 01~08 순서로 11, 23, 37, 41, 59, 67, 83, 113
- **출력:** 512×512 PNG. RGB는 전부 흰색이고 먹의 농담은 Alpha에만 있다. 게임에서 `TouchInkArt`의 먹 색을 곱해 쓴다.
- **가져오기:** Default 텍스처, Alpha Is Transparency, Mipmap 끔, Bilinear, Clamp, Non-Power of 2 None, 최대 256.

`Blob(seed, 512)`이 픽셀마다 0~1의 농도(Alpha)를 돌려준다.

```csharp
static float S01(float a, float b, float x) { float t = UnityEngine.Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
static float Fbm(float x, float y, int oct) { float a = .5f, s = 0f, n = 0f; for (int i = 0; i < oct; i++) { s += a * UnityEngine.Mathf.PerlinNoise(x, y); n += a; x = x * 2.03f + 17.1f; y = y * 2.03f + 9.7f; a *= .5f; } return s / n; }
static float[] Blob(int seed, int S) {
  var rng = new System.Random(seed); float ox = (float)rng.NextDouble() * 900f + 10f, oy = (float)rng.NextDouble() * 900f + 10f;
  float ang = (float)rng.NextDouble() * 6.2832f, dx = UnityEngine.Mathf.Cos(ang), dy = UnityEngine.Mathf.Sin(ang), R0 = .60f + (float)rng.NextDouble() * .07f;
  var a = new float[S * S];
  for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
    float u = (x + .5f) / S * 2f - 1f, v = (y + .5f) / S * 2f - 1f;
    float wx = Fbm(u * 1.2f + ox, v * 1.2f + oy, 3) - .5f, wy = Fbm(u * 1.2f + ox + 31f, v * 1.2f + oy + 57f, 3) - .5f;
    float px = u + wx * .42f, py = v + wy * .42f, d = UnityEngine.Mathf.Sqrt(px * px + py * py) / R0;
    float t = u * dx + v * dy; float side = S01(-.7f, .7f, t);
    float e1 = Fbm(u * 4.5f + ox + 11f, v * 4.5f + oy + 5f, 4) - .5f, e2 = Fbm(u * 15f + ox + 3f, v * 15f + oy + 71f, 3) - .5f;
    float rag = UnityEngine.Mathf.Lerp(1f, .30f, side);
    float f = d + e1 * rag * .95f + e2 * rag * .8f;
    float cov = 1f - S01(.965f, 1.015f, f);
    if (cov <= 0f) continue;
    float g = S01(-.5f, .8f, t + (Fbm(u * 1.15f + ox + 90f, v * 1.15f + oy + 13f, 3) - .5f) * 1.5f);
    float dens = UnityEngine.Mathf.Lerp(.40f, 1f, g * g * (3f - 2f * g));
    dens += (Fbm(u * 2.4f + ox + 200f, v * 2.4f + oy + 40f, 3) - .5f) * .26f;
    for (int L = 0; L < 3; L++) {
      float lx = u - dx * (.34f - L * .30f) + wy * .9f - (L == 1 ? dy * .28f : 0f), ly = v - dy * (.34f - L * .30f) - wx * .9f + (L == 1 ? dx * .28f : 0f);
      float n = Fbm(u * 1.9f + ox + 400f + L * 60f, v * 1.9f + oy + 410f, 3) - .5f;
      float dl = UnityEngine.Mathf.Sqrt(lx * lx + ly * ly) / (.46f + L * .07f) + n * .9f;
      float mask = 1f - S01(.975f, 1.025f, dl); float k = L == 0 ? .24f : .15f;
      dens = 1f - (1f - dens) * (1f - k * mask);
      float edge = (dl - 1f) / .032f; dens += UnityEngine.Mathf.Exp(-edge * edge) * .085f;
    }
    dens += (Fbm(u * 40f + ox, v * 40f + oy, 2) - .5f) * .14f;
    float rim = S01(.70f, .99f, f); dens *= UnityEngine.Mathf.Lerp(1f, .72f, rim * (1f - side));
    a[y * S + x] = cov * UnityEngine.Mathf.Clamp01(dens);
  }
  int dots = 45 + rng.Next(45);
  for (int i = 0; i < dots; i++) {
    float th = (float)rng.NextDouble() * 6.2832f; float cx = UnityEngine.Mathf.Cos(th), cy = UnityEngine.Mathf.Sin(th);
    if (cx * dx + cy * dy > .4f && rng.NextDouble() < .7) continue;
    float dist = R0 * (1.02f + (float)(rng.NextDouble() * rng.NextDouble()) * .62f);
    float rad = (1.2f + (float)System.Math.Pow(rng.NextDouble(), 3.0) * 9f) * S / 512f; float tone = .3f + (float)rng.NextDouble() * .55f;
    float mx = (cx * dist + 1f) * .5f * S, my = (cy * dist + 1f) * .5f * S; int r = (int)(rad * 1.7f) + 2;
    for (int y = System.Math.Max(0, (int)my - r); y < System.Math.Min(S, (int)my + r); y++) for (int x = System.Math.Max(0, (int)mx - r); x < System.Math.Min(S, (int)mx + r); x++) {
      float ddx = x + .5f - mx, ddy = y + .5f - my; float w = 1f + (UnityEngine.Mathf.PerlinNoise(x * .22f + i * 3.1f + ox, y * .22f + oy) - .5f) * .9f;
      float q = 1f - S01(rad * w - .8f, rad * w + .8f, UnityEngine.Mathf.Sqrt(ddx * ddx + ddy * ddy));
      if (q * tone > a[y * S + x]) a[y * S + x] = q * tone;
    }
  }
  return a;
}
```
