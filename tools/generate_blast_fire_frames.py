#!/usr/bin/env python3
"""폭발 불 그림 6장 만들기 (#326).

폭탄 충격파에 맞은 구슬 위에 붙는 먹선 만화 불을 그린다. 빨강·주황·노랑 세 겹의 평면 불길에 먹선을 두르고,
한 바퀴(6장) 동안 불길 높이·기울기·휘어짐이 조금씩 달라져 이어 보면 날름거린다.
캔버스 256×320px, 구슬(반지름 80px) 중심은 (128, 300) = 그림 기준 (0.5, 0.0625)이다.
LabOrbAppearance의 `Blast Fire Orb Centre`·`Blast Fire Width`(3.2)가 이 값을 쓴다.

    python3 tools/generate_blast_fire_frames.py            # Assets/Lab/Resources/Bomb/BlastFire에 덮어쓰기
    python3 tools/generate_blast_fire_frames.py out_dir     # 다른 폴더에

Pillow가 필요하다 (pip install pillow).
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

FRAMES = 6
SS = 4  # 4배로 그려 줄여서 가장자리를 부드럽게
W, H = 256, 320
R = 80
CX, CY = 128, 300
RED = (229, 52, 38, 255)
ORANGE = (248, 128, 34, 255)
YELLOW = (255, 208, 76, 255)
INK = (40, 30, 28, 255)
# 불길: 구슬 중심에서의 x, y(반지름 배, 위가 +), 폭, 높이, 기울기(도), 끝 휘어짐
TONGUES = [(0, .35, 1.5, 2.6, 0, .35), (-.62, .25, 1.0, 1.8, 18, -.45), (.62, .25, .95, 1.7, -18, .45),
           (-.25, .4, .7, 1.3, 8, .3), (.3, .4, .65, 1.2, -8, -.3)]


def flame(cx, base, w, h, lean, curl, seed):
    """둥근 밑동에서 휘어진 끝으로 가는 불길 하나의 외곽선."""
    points = []
    count = 120
    for i in range(count):
        th = i / count * 2 * math.pi
        y = (1 + math.cos(th)) / 2
        half = math.sqrt(max(0, 1 - ((y - .38) / .38) ** 2)) if y < .38 else (1 - (y - .38) / .62) ** 1.15
        half *= .5 * (1 + .05 * math.sin(y * 14 + seed))
        x = (1 if th < math.pi else -1) * half + curl * math.sin(y * math.pi * 1.2) * y ** 1.5
        px, py = x * w, -y * h
        a = math.radians(lean)
        points.append((cx + px * math.cos(a) - py * math.sin(a), base + px * math.sin(a) + py * math.cos(a)))
    return points


def layer(phi, scale, color, shift):
    image = Image.new('RGBA', (W * SS, H * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    r = R * SS
    for k, (x, y, w, h, lean, curl) in enumerate(TONGUES):
        grow = 1 + .17 * math.sin(phi + k * 2.1)
        sway = 9 * math.sin(phi + k * 2.7 + 1)
        bend = curl * (1 + .25 * math.sin(phi + k))
        draw.polygon(flame(CX * SS + x * r, CY * SS - (y + shift) * r, w * r * scale, h * r * scale * grow,
                           lean + sway, bend, k), fill=color)
    return image


def frame(index):
    phi = 2 * math.pi * index / FRAMES
    red = layer(phi, 1, RED, 0)
    out = Image.new('RGBA', red.size, (0, 0, 0, 0))
    # 먹선: 바깥 불길을 둥글게 키운 자리
    outline = red.split()[3].filter(ImageFilter.GaussianBlur(R * SS * .03)).point(lambda v: 255 if v > 8 else 0)
    out.paste(Image.new('RGBA', red.size, INK), (0, 0), outline.filter(ImageFilter.GaussianBlur(1.5)))
    out.alpha_composite(red)
    out.alpha_composite(layer(phi, .66, ORANGE, .02))
    out.alpha_composite(layer(phi, .38, YELLOW, .04))
    left, top, right, bottom = out.getbbox()
    assert left > 0 and top > 0 and right < W * SS and bottom < H * SS, 'the fire leaves the canvas'
    return out.resize((W, H), Image.LANCZOS)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    target = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, 'Assets', 'Lab', 'Resources', 'Bomb', 'BlastFire')
    os.makedirs(target, exist_ok=True)
    for i in range(FRAMES):
        frame(i).save(os.path.join(target, 'BlastFire%d.png' % i))
    print('wrote %d frames to %s' % (FRAMES, target))


if __name__ == '__main__':
    main()
