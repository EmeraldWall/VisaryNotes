"""Checks every VisaryNotes theme against the contrast targets in docs/DESIGN.md.

Usage: python3 tools/design/contrast.py [themes.json] [tokens.json]
Exits with status 1 if any pair is below its minimum.

Glass is checked three ways, matching DESIGN.md section 4.2:
* at the base tint over what normally scrolls under a bar once blurred
  (paper, text averaged into paper, highlight, selection, accent, desk),
* at the maximum tint over any image (pure white and pure black), which is
  what the adaptive glass handler raises the tint to,
* for home screen widgets, at the widget tint over any wallpaper.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def hex_to_rgb(h):
    h = h.lstrip('#')
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def luminance(rgb):
    r, g, b = [linear(x) for x in rgb]
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def ratio(a, b):
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def over(fg, alpha, bg):
    return [alpha * f + (1 - alpha) * b for f, b in zip(fg, bg)]


WHITE, BLACK = [1, 1, 1], [0, 0, 0]


def check(name, t, glass):
    c = {k: hex_to_rgb(v) for k, v in t.items() if isinstance(v, str) and v.startswith('#')}
    typical = [c['paper'], over(c['ink'], 0.15, c['paper']), c['hl'], c['sel'],
               over(c['accent'], 0.5, c['paper']), c['desk']]

    def worst(col, alpha, backs):
        return min(ratio(col, over(c['glass'], alpha, b)) for b in backs)

    dark = luminance(c['paper']) < 0.2
    base, top = glass['alpha-base'], glass['alpha-max']
    widget = glass['widget-alpha-dark'] if dark else glass['widget-alpha-light']
    if c['glass'] == c['paper'] and name.startswith('High Contrast'):
        base = top = widget = 1.0  # glass is switched off in high contrast themes
    rows = [
        ('ink on paper', ratio(c['ink'], c['paper']), 7),
        ('soft on paper', ratio(c['soft'], c['paper']), 4.5),
        ('ink on desk', ratio(c['ink'], c['desk']), 7),
        ('soft on desk', ratio(c['soft'], c['desk']), 4.5),
        (f'ink on glass {base:.2f}, typical', worst(c['ink'], base, typical), 7),
        (f'soft on glass {base:.2f}, typical', worst(c['soft'], base, typical), 4.5),
        (f'ink on glass {top:.2f}, any image', worst(c['ink'], top, [WHITE, BLACK]), 7),
        (f'soft on glass {top:.2f}, any image', worst(c['soft'], top, [WHITE, BLACK]), 4.5),
        (f'widget ink {widget:.2f}, any wallpaper', worst(c['ink'], widget, [WHITE, BLACK]), 7),
        (f'widget label {widget:.2f}, any wallpaper', worst(c['soft'], widget, [WHITE, BLACK]), 4.5),
        ('link text on paper', ratio(c['accentText'], c['paper']), 4.5),
        ('on accent text', ratio(c['onAccent'], c['accent']), 4.5),
        ('accent as UI on paper', ratio(c['accent'], c['paper']), 3),
        ('ink on highlight', ratio(c['ink'], c['hl']), 7),
        ('ink on selection', ratio(c['ink'], c['sel']), 7),
        ('divider and border on paper', ratio(c['line'], c['paper']), 3),
    ]
    ok = all(v >= m for _, v, m in rows)
    print(f"\n{name}: {'PASS' if ok else 'FAIL'}")
    for label, v, m in rows:
        print(f"  {label:36s} {v:5.2f} (min {m}) {'ok' if v >= m else 'LOW'}")
    return ok


if __name__ == '__main__':
    themes = json.load(open(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'themes.json')))
    tokens = json.load(open(sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, 'tokens.json')))
    results = [check(n, t, tokens['glass']) for n, t in themes.items()]
    sys.exit(0 if all(results) else 1)
