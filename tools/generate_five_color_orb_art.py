#!/usr/bin/env python3
"""섬광 파훼 보상 오색구슬 그림 만들기 (#329).

FiveColorOrb.png: 오색구슬의 먹선 테두리·그늘·하이라이트 (256×256, 원 폭이 그림 폭의 약 0.92). 안은 비어 있고,
    색은 코드(LabFiveColorSparkle)가 다섯 색 사이를 건너가며 아래에 칠한다.
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


def five_color_orb():
    """먹선 테두리 + 아래 그늘 + 왼쪽 위 하이라이트. 안은 비어 있고, 다섯 색으로 바뀌는 몸통 위에 얹는다."""
    size = 256 * SS
    c, r = size / 2, size * .46
    box = (c - r, c - r, c + r, c + r)
    disc = Image.new('L', (size, size), 0)
    ImageDraw.Draw(disc).ellipse(box, fill=255)
    zero = Image.new('L', (size, size), 0)
    # 아래쪽 은은한 그늘 (검은색, 반투명)
    shade = Image.new('L', (size, size), 0)
    ImageDraw.Draw(shade).ellipse((c - r * .9, c - r * .05, c + r * 1.1, c + r * 1.3), fill=50)
    shade = Image.composite(shade.filter(ImageFilter.GaussianBlur(size * .05)), zero, disc)
    image = Image.merge('RGBA', (*Image.new('RGB', (size, size), (10, 6, 6)).split(), shade))
    # 왼쪽 위 하이라이트 (흰색) + 작은 점
    light = Image.new('L', (size, size), 0)
    ld = ImageDraw.Draw(light)
    ld.ellipse((c - r * .62, c - r * .7, c - r * .1, c - r * .3), fill=200)
    light = Image.composite(light.filter(ImageFilter.GaussianBlur(size * .022)), zero, disc)
    image = Image.alpha_composite(image, Image.merge('RGBA', (*Image.new('RGB', (size, size), (255, 255, 255)).split(), light)))
    dot = Image.new('L', (size, size), 0)
    ImageDraw.Draw(dot).ellipse((c + r * .18, c - r * .62, c + r * .3, c - r * .5), fill=190)
    image = Image.alpha_composite(image, Image.merge('RGBA', (*Image.new('RGB', (size, size), (255, 255, 255)).split(),
                                                             dot.filter(ImageFilter.GaussianBlur(size * .006)))))
    ImageDraw.Draw(image).ellipse(box, outline=INK, width=int(r * .09))
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
