"""Check proposed Folium token pairs. Run with Python 3, without dependencies."""

import json
from pathlib import Path

TOKEN_PATH = Path(__file__).resolve().parents[1] / 'design' / 'tokens.json'


def load_palettes(path=TOKEN_PATH):
    """Read both palettes from the design contract."""
    tokens = json.loads(path.read_text(encoding='utf-8'))
    palettes = tokens['color']
    required = {'background', 'surface', 'raised', 'text', 'muted', 'primary',
                'onPrimary', 'secondary', 'accent', 'border', 'success',
                'warning', 'error', 'info'}
    if set(palettes) != {'light', 'dark'}:
        raise ValueError('Define exactly the light and dark palettes.')
    for theme, palette in palettes.items():
        if set(palette) != required:
            raise ValueError(f'{theme}: define every semantic color token.')
        for name, color in palette.items():
            if (not isinstance(color, str) or len(color) != 7
                    or color[0] != '#'
                    or any(c not in '0123456789abcdefABCDEF' for c in color[1:])):
                raise ValueError(f'{theme}/{name}: use an opaque #RRGGBB color.')
    return palettes


def luminance(color):
    channels = [int(color[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    linear = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
              for c in channels]
    return sum(c * weight for c, weight in zip(linear, (0.2126, 0.7152, 0.0722)))


def ratio(first, second):
    values = sorted((luminance(first), luminance(second)))
    return (values[1] + 0.05) / (values[0] + 0.05)


def main():
    # Formula sanity checks catch regressions before checking palette tokens.
    assert abs(ratio('#000000', '#FFFFFF') - 21.0) < 1e-9
    assert abs(ratio('#315C43', '#315C43') - 1.0) < 1e-9
    failures = []
    print('| Theme | Foreground | Minimum across three surfaces | Required |')
    print('| --- | --- | ---: | ---: |')
    for theme, palette in load_palettes().items():
        for token in ('text', 'muted', 'primary', 'secondary', 'accent',
                      'border', 'success', 'warning', 'error', 'info'):
            value = min(ratio(palette[token], palette[surface])
                        for surface in ('background', 'surface', 'raised'))
            threshold = 3 if token == 'border' else 4.5
            print(f'| {theme} | {token} | {value:.2f}:1 | {threshold}:1 |')
            if value < threshold:
                failures.append(f'{theme}/{token}')
        value = ratio(palette['onPrimary'], palette['primary'])
        print(f'| {theme} | onPrimary on primary | {value:.2f}:1 | 4.5:1 |')
        if value < 4.5:
            failures.append(f'{theme}/onPrimary')
    if failures:
        raise SystemExit('Contrast failed: ' + ', '.join(failures))


if __name__ == '__main__':
    main()
