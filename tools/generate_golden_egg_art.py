#!/usr/bin/env python3
"""섬광 파훼 보상 황금알 그림 만들기 (#329).

GoldenEgg.png: 먹선을 두른 반짝이는 금빛 알 (256×256, 알 폭이 그림 폭의 약 0.87).
GoldenSparkle.png: 반짝이는 네 갈래 흰 별 (128×128). 색은 코드에서 입힌다.

    python3 tools/generate_golden_egg_art.py            # Assets/Lab/Resources/Yaguang에 덮어쓰기
    python3 tools/generate_golden_egg_art.py out_dir

Pillow가 필요하다 (pip install pillow).
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

SS = 4
INK = (40, 30, 28, 255)


def egg_points(cx, cy, w, h, n=200):
    points = []
    for i in range(n):
        t = i / n * 2 * math.pi
        x, y = math.cos(t), math.sin(t)
        # 위쪽(y<0)이 조금 좁은 알 모양
        squeeze = 1 - .16 * max(0, -y)
        points.append((cx + x * w * .5 * squeeze, cy + y * h * .5))
    return points


def golden_egg():
    size = 256 * SS
    image = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    cx, cy, w, h = size / 2, size * .52, size * .78, size * .9
    sil = Image.new('L', (size, size), 0)
    ImageDraw.Draw(sil).polygon(egg_points(cx, cy, w, h), fill=255)
    ink = sil.filter(ImageFilter.GaussianBlur(size * .012)).point(lambda v: 255 if v > 8 else 0)
    image.paste(Image.new('RGBA', (size, size), INK), (0, 0), ink.filter(ImageFilter.GaussianBlur(1.5)))
    # 금빛 몸통: 아래가 진하고 위가 밝은 세로 그러데이션
    body = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    px = body.load()
    for y in range(size):
        v = (y - (cy - h / 2)) / h
        r, g, b = 255, int(214 - 70 * v), int(70 - 40 * v)
        for x in range(size):
            px[x, y] = (r, max(0, g), max(0, b), 255)
    image.paste(body, (0, 0), sil)
    draw = ImageDraw.Draw(image)
    # 아래쪽 그늘과 왼쪽 위 하이라이트
    shade = Image.new('L', (size, size), 0)
    ImageDraw.Draw(shade).ellipse((cx - w * .42, cy + h * .02, cx + w * .5, cy + h * .52), fill=110)
    shade = Image.composite(shade.filter(ImageFilter.GaussianBlur(size * .04)), Image.new('L', (size, size), 0), sil)
    image.paste(Image.new('RGBA', (size, size), (196, 110, 18, 255)), (0, 0), shade)
    draw.ellipse((cx - w * .3, cy - h * .36, cx - w * .08, cy - h * .12), fill=(255, 251, 220, 235))
    draw.ellipse((cx - w * .02, cy - h * .3, cx + w * .06, cy - h * .22), fill=(255, 251, 220, 200))
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
    golden_egg().save(os.path.join(target, 'GoldenEgg.png'))
    sparkle().save(os.path.join(target, 'GoldenSparkle.png'))
    print('wrote golden egg art to', target)


if __name__ == '__main__':
    main()
