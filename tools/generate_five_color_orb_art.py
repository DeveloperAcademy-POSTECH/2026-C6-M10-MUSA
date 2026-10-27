#!/usr/bin/env python3
"""섬광 파훼 보상 오색구슬 그림 만들기 (#329).

FiveColorOrb.png: 먹선을 두른 다섯 원소 색 조각 구슬 (256×256, 구슬 폭이 그림 폭의 약 0.92).
FiveColorSparkle.png: 반짝이는 네 갈래 흰 별 (128×128). 색은 코드에서 입힌다.

    python3 tools/generate_five_color_orb_art.py            # Assets/Lab/Resources/Yaguang에 덮어쓰기
    python3 tools/generate_five_color_orb_art.py out_dir

Pillow가 필요하다 (pip install pillow).
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

SS = 4
INK = (40, 30, 28, 255)


# LabFiveColorSparkle.FiveColors와 같은 원색에 가까운 다섯 색. 위에서 시계 방향으로 초록 → 빨강 → 갈색 → 노랑 → 파랑.
FIVE_COLORS = [(30, 178, 76), (232, 38, 38), (140, 82, 38), (255, 206, 0), (18, 98, 232)]


def five_color_orb():
    size = 256 * SS
    image = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    c, r = size / 2, size * .46
    box = (c - r, c - r, c + r, c + r)
    for i, color in enumerate(FIVE_COLORS):
        start = -90 + 72 * i  # PIL 각도는 시계 방향
        draw.pieslice(box, start, start + 72, fill=color + (255,))
    disc = Image.new('L', (size, size), 0)
    ImageDraw.Draw(disc).ellipse(box, fill=255)
    # 아래쪽 은은한 그늘과 왼쪽 위 하이라이트
    shade = Image.new('L', (size, size), 0)
    ImageDraw.Draw(shade).ellipse((c - r * .9, c - r * .1, c + r * 1.1, c + r * 1.25), fill=45)
    shade = Image.composite(shade.filter(ImageFilter.GaussianBlur(size * .05)), Image.new('L', (size, size), 0), disc)
    image.paste(Image.new('RGBA', (size, size), (20, 14, 12, 255)), (0, 0), shade)
    light = Image.new('L', (size, size), 0)
    ImageDraw.Draw(light).ellipse((c - r * .62, c - r * .7, c - r * .1, c - r * .3), fill=175)
    light = Image.composite(light.filter(ImageFilter.GaussianBlur(size * .025)), Image.new('L', (size, size), 0), disc)
    image.paste(Image.new('RGBA', (size, size), (255, 255, 255, 255)), (0, 0), light)
    draw.ellipse(box, outline=INK, width=int(r * .09))
    return image.resize((256, 256), Image.LANCZOS)


def sparkle():
    size = 128 * SS
    image = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    c = size / 2
    points = []
    for i in range(16):
        angle = i / 16 * 2 * math.pi
        radius = size * (.48 if i % 4 == 0 else .2 if i % 2 == 0 else .09)
        points.append((c + math.cos(angle) * radius, c + math.sin(angle) * radius))
    draw.polygon(points, fill=(255, 255, 255, 255))
    glow = image.filter(ImageFilter.GaussianBlur(size * .03))
    glow.alpha_composite(image)
    return glow.resize((128, 128), Image.LANCZOS)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    target = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, 'Assets', 'Lab', 'Resources', 'Yaguang')
    os.makedirs(target, exist_ok=True)
    five_color_orb().save(os.path.join(target, 'FiveColorOrb.png'))
    sparkle().save(os.path.join(target, 'FiveColorSparkle.png'))
    print('wrote five color orb art to', target)


if __name__ == '__main__':
    main()
