"""Check token constraints and contrast failure behavior without app styling."""

import contextlib
import copy
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    'check_contrast', ROOT / 'docs/research/check_contrast.py')
CONTRAST = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONTRAST)


class TokenTests(unittest.TestCase):
    def setUp(self):
        self.tokens = json.loads(CONTRAST.TOKEN_PATH.read_text(encoding='utf-8'))

    def test_palette_meets_contrast_contract(self):
        with contextlib.redirect_stdout(io.StringIO()):
            CONTRAST.main()

    def test_insufficient_contrast_fails(self):
        palettes = copy.deepcopy(self.tokens['color'])
        palettes['light']['text'] = palettes['light']['surface']
        with patch.object(CONTRAST, 'load_palettes', return_value=palettes):
            with contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaisesRegex(SystemExit, 'light/text'):
                    CONTRAST.main()

    def test_missing_role_theme_and_nonopaque_colors_fail(self):
        invalid = []
        missing_role = copy.deepcopy(self.tokens)
        del missing_role['color']['dark']['error']
        invalid.append(missing_role)
        missing_theme = copy.deepcopy(self.tokens)
        del missing_theme['color']['dark']
        invalid.append(missing_theme)
        for color in ('#FFF', '#80123456', '#GGGGGG', 'FFFFFFF', None):
            tokens = copy.deepcopy(self.tokens)
            tokens['color']['light']['text'] = color
            invalid.append(tokens)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'tokens.json'
            for tokens in invalid:
                with self.subTest(tokens=tokens):
                    path.write_text(json.dumps(tokens), encoding='utf-8')
                    with self.assertRaises(ValueError):
                        CONTRAST.load_palettes(path)

    def test_type_scale_and_touch_bounds_are_consistent(self):
        styles = self.tokens['typography']['styles']
        self.assertEqual(set(styles), {'caption', 'body', 'label', 'section', 'title'})
        for style in styles.values():
            self.assertGreater(style['fontSize'], 0)
            self.assertGreaterEqual(style['lineHeight'], style['fontSize'])
            self.assertIn(style['fontWeight'], (400, 500, 600))
        layout = self.tokens['layout']
        self.assertGreaterEqual(layout['touchTargetMin'], 48)
        self.assertGreaterEqual(layout['touchTargetMin'], self.tokens['icon']['size']['action'])
        self.assertGreaterEqual(layout['interactiveGapMin'], self.tokens['spacing']['sm'])
        self.assertGreaterEqual(layout['gutterWide'], layout['gutter'])
        self.assertEqual(sorted(self.tokens['spacing'].values()),
                         list(self.tokens['spacing'].values()))


if __name__ == '__main__':
    unittest.main()
