"""Check proposed Folium token pairs. Run with Python 3, without dependencies."""

PALETTES = {
    'light': {
        'background': '#FAF8F3', 'surface': '#FFFFFF', 'raised': '#F1EFE8',
        'text': '#202922', 'muted': '#536157', 'primary': '#315C43',
        'onPrimary': '#FFFFFF', 'secondary': '#5B5269', 'accent': '#835A27',
        'border': '#6B786E', 'success': '#28613C', 'warning': '#805600',
        'error': '#A52F35', 'info': '#285E86',
    },
    'dark': {
        'background': '#171C18', 'surface': '#202922', 'raised': '#29352D',
        'text': '#F1F3EC', 'muted': '#B8C4B8', 'primary': '#AED4B3',
        'onPrimary': '#173823', 'secondary': '#CEC0DF', 'accent': '#E8BE82',
        'border': '#87998A', 'success': '#A2D5AF', 'warning': '#E8C36B',
        'error': '#FFB3B6', 'info': '#A9CEE9',
    },
}


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
    for theme, palette in PALETTES.items():
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
