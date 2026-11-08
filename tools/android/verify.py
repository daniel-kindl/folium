#!/usr/bin/env python3
"""Exercise Folium through Android accessibility. Use an isolated verification APK."""
import argparse
import hashlib
import json
import re
import shlex
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZipFile

PACKAGE = 'dev.danielkindl.folium'


class Device:
    def __init__(self, serial, output):
        self.serial = serial
        self.output = output
        self.output.mkdir(parents=True, exist_ok=True)
        self.events = []
        self.pids = set()

    def adb(self, *args, binary=False):
        result = subprocess.run(['adb', '-s', self.serial, *args], check=True,
                                capture_output=True, timeout=20)
        return result.stdout if binary else result.stdout.decode().strip()

    def shell(self, *args):
        return self.adb('shell', shlex.join(args))

    def tree(self):
        self.shell('uiautomator', 'dump', '/data/local/tmp/folium-verification.xml')
        return ET.fromstring(self.shell('cat', '/data/local/tmp/folium-verification.xml'))

    def nodes(self, tree, text, cls=None):
        nodes = [n for n in tree.iter('node') if n.get('package') == PACKAGE]
        matches = [n for n in nodes
                   if (n.get('text') == text or n.get('content-desc') == text)
                   and (cls is None or n.get('class') == cls)]
        if not matches and cls == 'TextBox':
            labels = [n for n in nodes if n.get('class') == 'TextBlock' and n.get('text') == text]
            if labels:
                bottom = list(map(int, re.findall(r'\d+', labels[0].get('bounds'))))[3]
                candidates = [n for n in nodes if n.get('class') == 'TextBox'
                              and int(re.findall(r'\d+', n.get('bounds'))[1]) >= bottom]
                matches = sorted(candidates, key=lambda n: int(re.findall(r'\d+', n.get('bounds'))[1]))[:1]
        if not matches and cls == 'TextBox' and text == 'Book title':
            matches = [n for n in nodes if n.get('class') == 'TextBox'][:1]
        return matches

    def wait(self, text, cls=None, timeout=25):
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            tree = self.tree()
            found = self.nodes(tree, text, cls)
            if found:
                return found[0]
            time.sleep(.3)
        self.capture('failure')
        raise AssertionError(f'Android UI does not contain {text!r} ({cls}).')

    def tap(self, text, cls='Button'):
        node = self.wait(text, cls)
        assert node.get('enabled') == 'true', f'{text!r} is disabled.'
        x1, y1, x2, y2 = map(int, re.findall(r'\d+', node.get('bounds')))
        assert x2 > x1 and y2 > y1, f'{text!r} has no visible bounds.'
        self.shell('input', 'tap', str((x1+x2)//2), str((y1+y2)//2))

    def hide_keyboard(self):
        self.shell('input', 'keyevent', '111')
        time.sleep(.4)

    def enter(self, label, text, replace=False):
        self.tap(label, 'TextBox')
        if replace:
            self.shell('input', 'keycombination', '113', '29')
            self.shell('input', 'keyevent', '67')
        self.shell('input', 'text', text.replace(' ', '%s'))

    def scroll_to(self, text, cls='Button'):
        for _ in range(12):
            tree = self.tree()
            found = self.nodes(tree, text, cls)
            if found and found[0].get('enabled') == 'true':
                return found[0]
            if found and found[0].get('enabled') != 'true':
                pass
            self.scroll()
        raise AssertionError(f'{text!r} is not reachable by scrolling.')

    def mode(self, mode):
        self.shell('run-as', PACKAGE, 'sh', '-c', 'printf %s ' + shlex.quote(mode) + ' > files/verification-mode')

    def assert_saved_cover(self):
        tree = self.tree()
        assert not self.nodes(tree, 'No saved cover', 'TextBlock'), 'Native decoder displayed the fallback.'
        images = [n for n in tree.iter('node') if n.get('resource-id') == 'SavedCover'
                  or n.get('class') in ('Image', 'LocalCover')]
        assert images, 'No native cover image node.'
        assert any((lambda b: b[2] > b[0] and b[3] > b[1])(list(map(int, re.findall(r'\d+', n.get('bounds')))))
                   for n in images), 'Saved cover has no visible bounds.'

    def save_logs(self):
        for pid in self.pids:
            log = self.adb('logcat', '-d', '--pid='+pid)
            self.output.joinpath('app-'+pid+'.log').write_text(log)
            assert not re.search(r'FATAL EXCEPTION|Fatal signal|ANR in ' + PACKAGE, log), 'App crash or ANR in process logs.'

    def search(self, title='Dune'):
        self.tap('Find a book online')
        self.enter('Book title', title, replace=True)
        self.back()
        self.tap('Search Open Library')
        self.wait('Choose a book to see its editions.', 'TextBlock')

    def preview(self, edition='Dune'):
        self.tap('Dune')
        self.wait('Choose an edition to preview its metadata.', 'TextBlock')
        self.scroll_to(edition)
        self.tap(edition)
        self.wait('Metadata from Open Library. Nothing has been saved.', 'TextBlock')
        self.scroll_to('Review and save')

    def import_save(self, title, keep_cover):
        self.tap('Review and save')
        self.wait('Add a book', 'TextBlock')
        self.enter('Title', title, replace=True)
        self.back()
        self.enter('Publisher', 'Device publisher', replace=True)
        self.capture('import-keyboard')
        self.hide_keyboard()
        # The Save control is below the keyboard-fold scroll viewport. Tap its fixed portrait position after dismissing the IME.
        self.shell('input', 'tap', '540', '2200')
        time.sleep(.5)
        boxes = [n for n in self.tree().iter('node') if n.get('class') == 'CheckBox']
        assert len(boxes) == 1 and boxes[0].get('checked') == 'false', 'Cover permission must start unchecked.'
        if keep_cover:
            self.tap('I have permission to save this cover on my device', 'CheckBox')
        self.capture('import-permission-'+str(keep_cover).lower())
        self.shell('input', 'tap', '540', '2200')
        self.wait(title, 'Button')


    def back(self):
        self.shell('input', 'keyevent', '4')

    def capture(self, name):
        focus = self.shell('dumpsys', 'window')
        current = next((line for line in focus.splitlines() if 'mCurrentFocus=' in line), '')
        if PACKAGE not in current:
            self.output.joinpath(name+'-unavailable.txt').write_text('Folium is not in the foreground. No screenshot captured.\n')
            return
        self.output.joinpath(name+'.png').write_bytes(self.adb('exec-out', 'screencap', '-p', binary=True))
        self.output.joinpath(name+'.xml').write_text(ET.tostring(self.tree(), encoding='unicode'))

    def record(self, name):
        self.events.append(name)
        self.output.joinpath('steps.json').write_text(json.dumps(self.events, indent=2))
        print('PASS:', name, flush=True)

    def start(self):
        self.shell('am', 'force-stop', PACKAGE)
        self.shell('monkey', '-p', PACKAGE, '-c', 'android.intent.category.LAUNCHER', '1')
        self.wait('Library', 'TextBlock')
        self.pids.add(self.shell('pidof', PACKAGE).split()[0])

    def library(self):
        return json.loads(self.shell('run-as', PACKAGE, 'sh', '-c',
                                     '''if [ -f files/verification-library.json ]; then cat files/verification-library.json; else printf '%s' '{"version":1,"books":[]}'; fi'''))

    def scroll(self):
        size = list(map(int, re.findall(r'\d+', self.shell('wm', 'size').splitlines()[-1])))
        w, h = size[-2:]
        tree = self.tree()
        viewers = [n for n in tree.iter('node') if n.get('class') == 'ScrollViewer' and n.get('scrollable') == 'true']
        if viewers:
            bounds = list(map(int, re.findall(r'\d+', viewers[-1].get('bounds'))))
            x = (bounds[0]+bounds[2])//2
            self.shell('input', 'swipe', str(x), str(bounds[3]-80), str(x), str(bounds[1]+100), '450')
        else:
            self.shell('input', 'swipe', str(w//2), str(h*3//4), str(w//2), str(h//3), '450')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--flow', choices=['all', 'search'], default='all')
    parser.add_argument('--serial', required=True)
    parser.add_argument('--apk', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--restore-apk', type=Path, help='Required when Folium is already installed.')
    args = parser.parse_args()
    if not __debug__:
        raise RuntimeError('Run verification without Python optimization.')
    with ZipFile(args.apk) as apk:
        if apk.read('assets/folium-verification.txt') != b'isolated-library-v1\n':
            raise ValueError('Use an isolated verification APK.')
    d = Device(args.serial, args.output)
    def library_digest():
        if PACKAGE not in d.shell('pm', 'list', 'packages', PACKAGE):
            return 'absent'
        return d.shell('run-as', PACKAGE, 'sh', '-c', 'if [ -f files/library.json ]; then sha256sum files/library.json; else echo absent; fi')

    installed = PACKAGE in d.shell('pm', 'list', 'packages', PACKAGE)
    if installed and args.restore_apk is None:
        raise ValueError('Supply --restore-apk for an existing Folium installation.')
    if args.restore_apk is not None:
        with ZipFile(args.restore_apk) as apk:
            if 'assets/folium-verification.txt' in apk.namelist():
                raise ValueError('The restoration APK must be a normal build.')
    original = library_digest()
    metadata = {'serial': args.serial, 'api': d.shell('getprop', 'ro.build.version.sdk'),
                'model': d.shell('getprop', 'ro.product.model'),
                'abi': d.shell('getprop', 'ro.product.cpu.abi'),
                'commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                'apk_sha256': hashlib.sha256(args.apk.read_bytes()).hexdigest()}
    args.output.joinpath('environment.json').write_text(json.dumps(metadata, indent=2))
    try:
        d.adb('install', '-r', str(args.apk))
        d.shell('am', 'force-stop', PACKAGE)
        d.shell('run-as', PACKAGE, 'rm', '-f', 'files/verification-library.json', 'files/verification-mode')
        d.start()
        d.wait('No books yet.', 'TextBlock')
        d.capture('01-empty-library')
        d.record('Launch isolated empty library')
        if args.flow == 'all':
            d.tap('Add a book')
            d.tap('Save')
            d.wait('Enter a title and an author.', 'TextBlock')
            d.enter('Title', 'Device test')
            d.enter('Author', 'Fixture author')
            d.back()
            d.tap('Save')
            d.wait('Device test', 'Button')
            d.capture('02-populated-library')
            d.tap('Device test')
            d.wait('Book', 'TextBlock')
            d.tap('Reading')
            d.capture('03-details-reading')
            d.record('Create book and change reading status')
            d.tap('Edit')
            d.enter('Title', 'Edited device test', replace=True)
            d.back()
            d.tap('Save')
            d.wait('Edited device test', 'TextBlock')
            d.tap('Remove')
            d.tap('Keep book')
            d.wait('Edited device test', 'TextBlock')
            d.tap('Remove')
            d.back()
            d.wait('Book', 'TextBlock')
            d.tap('Remove')
            d.tap('Remove book')
            d.wait('No books yet.', 'TextBlock')
            d.tap('Add a book')
            d.enter('Title', 'Discarded draft')
            d.back()
            d.tap('Cancel')
            d.wait('No books yet.', 'TextBlock')
            d.record('Edit, remove, Keep book, Back and cancel draft')
        d.search()
        d.capture('04-search')
        d.tap('Next page')
        d.wait('Last fixture work', 'Button')
        d.tap('Previous page')
        d.wait('Dune', 'Button')
        d.preview()
        d.capture('05-preview-valid-cover')
        assert not d.library()['books'], 'Search preview wrote the library.'
        d.tap('Review and save')
        d.wait('Add a book', 'TextBlock')
        d.back()
        d.wait('Edition preview', 'TextBlock')
        d.scroll_to('Review and save')
        d.import_save('Import without cover', False)
        book = d.library()['books'][0]
        assert book['title'] == 'Import without cover' and book['metadata']['publisher'] == 'Device publisher'
        assert not book['metadata'].get('cover'), 'Unchecked cover was saved.'
        d.tap('Import without cover')
        d.wait('No saved cover', 'TextBlock')
        d.back()
        d.record('Search pages, edition preview, Back and import without cover')
        d.search()
        d.preview()
        d.import_save('Import with cover', True)
        book = next(b for b in d.library()['books'] if b['title'] == 'Import with cover')
        assert book['metadata']['cover'], 'Opted-in cover was not saved.'
        d.tap('Import with cover')
        d.assert_saved_cover()
        d.capture('06-saved-valid-cover')
        d.scroll_to('Reading')
        d.tap('Reading')
        d.back()
        d.record('Import with opted-in native cover and reading status')
        d.mode('offline')
        d.save_logs()
        d.start()
        d.tap('Import with cover')
        d.assert_saved_cover()
        d.wait('Device publisher', 'TextBlock')
        d.capture('07-offline-restart')
        d.tap('Edit')
        d.enter('Author', 'Offline author', replace=True)
        d.back()
        d.scroll_to('Save')
        d.tap('Save')
        d.back()
        book = next(b for b in d.library()['books'] if b['title'] == 'Import with cover')
        assert book['author'] == 'Offline author' and book['readingStatus'] == 'reading' and book['metadata']['cover']
        d.record('Provider offline restart retains metadata, status and cover; offline edits save')
        d.tap('Find a book online')
        d.enter('Book title', 'Offline query', replace=True)
        d.hide_keyboard()
        d.tap('Search Open Library')
        d.wait('Could not reach Open Library. Check your connection and try again.', 'TextBlock')
        d.capture('08-offline-search')
        d.mode('')
        d.tap('Try again')
        d.wait('Choose a book to see its editions.', 'TextBlock')
        d.back()
        d.mode('slow')
        d.tap('Find a book online')
        d.enter('Book title', 'Cancel request', replace=True)
        d.hide_keyboard()
        d.tap('Search Open Library')
        d.tap('Cancel search')
        d.wait('Search canceled.', 'TextBlock')
        d.back()
        d.mode('')
        d.record('Provider failure, retry and request cancellation')
        d.search('Invalid decoder fixture')
        d.preview('Invalid cover')
        d.capture('09-preview-invalid-cover')
        d.tap('Review and save')
        d.wait('Add a book', 'TextBlock')
        d.scroll_to('Save')
        d.tap('I have permission to save this cover on my device', 'CheckBox')
        d.tap('Save')
        d.wait('Invalid cover', 'Button')
        d.tap('Invalid cover')
        d.wait('No saved cover', 'TextBlock')
        d.capture('10-native-invalid-cover-fallback')
        d.back()
        d.record('Native invalid bitmap falls back without a crash')
        d.search('Missing metadata fixture')
        d.preview('Missing fields')
        d.tap('Review and save')
        d.wait('Add a book', 'TextBlock')
        d.scroll_to('Save')
        d.tap('Save')
        d.wait('Enter an author.', 'TextBlock')
        d.capture('11-missing-metadata-validation')
        d.back()
        d.wait('Edition preview', 'TextBlock')
        d.tap('Back')
        d.wait('Choose an edition to preview its metadata.', 'TextBlock')
        d.tap('Back')
        d.tap('Back')
        d.wait('Library', 'TextBlock')
        d.record('Missing cover, empty author validation and Back to preview')
        d.shell('input', 'keyevent', '3')
        d.shell('monkey', '-p', PACKAGE, '-c', 'android.intent.category.LAUNCHER', '1')
        d.wait('Library', 'TextBlock')
        d.capture('12-home-resume')
        rotation = d.shell('wm', 'user-rotation').split()
        try:
            d.shell('wm', 'user-rotation', 'lock', '1')
            time.sleep(1)
            d.wait('Library', 'TextBlock')
            d.capture('13-landscape')
            d.shell('wm', 'user-rotation', 'lock', '0')
            time.sleep(1)
            d.wait('Library', 'TextBlock')
            d.capture('14-portrait')
        finally:
            d.shell('wm', 'user-rotation', *rotation)
        d.record('Home/resume and portrait/landscape lifecycle')


    finally:
        try:
            d.save_logs()
        finally:
            d.shell('am', 'force-stop', PACKAGE)
            if args.restore_apk is not None:
                d.adb('install', '-r', str(args.restore_apk))
            after = library_digest()
            assert after == original, 'The original library changed.'
            d.output.joinpath('original-library-preserved.txt').write_text('SHA-256 unchanged\n')
        # Capture this process only. Never publish device-wide logs or personal UI.
        d.output.joinpath('result.json').write_text(json.dumps({'passed_steps': d.events}, indent=2))


if __name__ == '__main__':
    main()
