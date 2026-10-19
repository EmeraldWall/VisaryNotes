"""Rejects spacing, radius and font size values that are not design tokens.

Usage: python3 tools/design/lint_layout.py <files or folders>
Checks .xaml, .dc.html and .html files. Exits with status 1 on any violation.

Rules (see docs/DESIGN.md section 4.7):
* XAML Padding, Margin, Spacing, RowSpacing, ColumnSpacing and CornerRadius must
  use {StaticResource ...} or {DynamicResource ...}, never literal numbers.
* HTML and CSS padding, margin, gap, border-radius and font-size must use
  var(--sp-*), var(--h-*), var(--r-*), var(--fs-*) or 0.
* An element reset such as `.app button{padding:0}` outranks component classes
  like `.row{padding:...}` and silently removes their padding. Resets that touch
  padding or margin must be wrapped in :where() so they carry no specificity.
* Content that reproduces a document's own formatting (the Original view mock)
  is excluded with <!-- lint:off --> and <!-- lint:on --> markers, as is the
  block that defines the tokens themselves.
"""
import os
import re
import sys

XAML_ATTR = re.compile(r'\b(Padding|Margin|Spacing|RowSpacing|ColumnSpacing|CornerRadius)="([^"]*)"')
CSS_PROP = re.compile(r'(?<![\w-])(padding(?:-[a-z]+)?|margin(?:-[a-z]+)?|gap|row-gap|column-gap|border-radius|font-size)\s*:\s*([^;"}]+)')
RESET_RULE = re.compile(r'(^|\})\s*(\.[\w-]+\s+(button|a|input|label|span|div)[^{]*)\{[^}]*\b(padding|margin)\s*:')
ALLOWED_CSS = re.compile(r'^(0|auto|inherit|var\(--(sp|h|r|fs|lh)-[a-z0-9-]+\)|calc\(.*var\(--(sp|h|r|fs|lh)-[a-z0-9-]+\).*\))$')


def css_values(raw):
    raw = raw.strip()
    parts, depth, cur = [], 0, ''
    for ch in raw:
        if ch == '(':
            depth += 1
        elif ch == ')':
            depth -= 1
        if ch == ' ' and depth == 0:
            if cur:
                parts.append(cur)
            cur = ''
        else:
            cur += ch
    if cur:
        parts.append(cur)
    return parts


IMPLICIT_STYLE = re.compile(r'<Style\s+(?![^>]*x:Key)[^>]*TargetType="([^"]+)"')


def lint_file(path):
    problems = []
    off = False
    implicit = {}
    with open(path, encoding='utf-8') as f:
        for n, line in enumerate(f, 1):
            # Two implicit styles for one type in a dictionary crash the app at startup.
            for target in IMPLICIT_STYLE.findall(line):
                if target in implicit:
                    problems.append((n, f'second implicit style for {target}, first on line {implicit[target]}'))
                implicit.setdefault(target, n)
            if 'lint:off' in line:
                off = True
            if 'lint:on' in line:
                off = False
                continue
            if off:
                continue
            if path.endswith('.xaml'):
                for name, value in XAML_ATTR.findall(line):
                    if not value.startswith('{'):
                        problems.append((n, f'{name}="{value}" must use a token resource'))
            else:
                for m in RESET_RULE.finditer(line):
                    problems.append((n, f'reset "{m.group(2).strip()}" sets padding or margin with class specificity, wrap it in :where()'))
                for prop, value in CSS_PROP.findall(line):
                    for v in css_values(value):
                        if not ALLOWED_CSS.match(v):
                            problems.append((n, f'{prop}: {v} is not a token'))
    return problems


def files(args):
    for a in args:
        if os.path.isdir(a):
            for root, _, names in os.walk(a):
                for name in names:
                    if name.endswith(('.xaml', '.dc.html', '.html')):
                        yield os.path.join(root, name)
        else:
            yield a


if __name__ == '__main__':
    total = 0
    for path in files(sys.argv[1:] or ['src']):
        for n, msg in lint_file(path):
            print(f'{path}:{n}: {msg}')
            total += 1
    print(f'{total} violation(s)')
    sys.exit(1 if total else 0)
