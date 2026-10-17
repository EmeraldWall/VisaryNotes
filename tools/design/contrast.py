"""Checks every VisaryNotes theme against the contrast targets in docs/DESIGN.md.

Usage: python3 tools/design/contrast.py tools/design/themes.json
Exits with status 1 if any pair is below its minimum.
"""
import json
import sys


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


def check(name, t):
    c = {k: hex_to_rgb(v) for k, v in t.items() if isinstance(v, str) and v.startswith('#')}
    a = t['glassAlpha']
    # Worst case for a glass bar: paper, desk, ink (dark text scrolling under it) or accent behind it.
    glass = [over(c['glass'], a, b) for b in (c['paper'], c['desk'], c['ink'], c['accent'])]
    rows = [
        ('ink on paper', ratio(c['ink'], c['paper']), 7),
        ('soft on paper', ratio(c['soft'], c['paper']), 4.5),
        ('ink on desk', ratio(c['ink'], c['desk']), 7),
        ('soft on desk', ratio(c['soft'], c['desk']), 4.5),
        ('ink on glass, worst case', min(ratio(c['ink'], g) for g in glass), 7),
        ('soft on glass, worst case', min(ratio(c['soft'], g) for g in glass), 4.5),
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
        print(f"  {label:30s} {v:5.2f} (min {m}) {'ok' if v >= m else 'LOW'}")
    return ok


if __name__ == '__main__':
    themes = json.load(open(sys.argv[1] if len(sys.argv) > 1 else 'tools/design/themes.json'))
    results = [check(n, t) for n, t in themes.items()]
    sys.exit(0 if all(results) else 1)
