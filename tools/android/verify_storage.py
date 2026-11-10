#!/usr/bin/env python3
"""Verify cover migration and offline restart with isolated Android fixture data."""
import argparse
import base64
import hashlib
import json
import re
import time
import subprocess
from pathlib import Path
from zipfile import ZipFile
from verify import Device, PACKAGE


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--apk', type=Path, required=True)
    parser.add_argument('--restore-apk', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not __debug__:
        raise RuntimeError('Run verification without Python optimization.')
    with ZipFile(args.apk) as apk:
        assert apk.read('assets/folium-verification.txt') == b'isolated-library-v1\n'
    with ZipFile(args.restore_apk) as apk:
        assert 'assets/folium-verification.txt' not in apk.namelist()
    d = Device(args.serial, args.output)

    def digest():
        return d.shell('run-as', PACKAGE, 'sh', '-c',
                       'if [ -f files/library.json ]; then sha256sum files/library.json; else echo absent; fi')

    original = digest()
    cover = Path(__file__).with_name('fixtures').joinpath('cover.png').read_bytes()
    metadata = dict(workKey='/works/OL1W', editionKey='/books/OL1M', publisher='Fixture publisher',
                    publicationDate='2001', isbn='123', edition='First', language='eng',
                    description='Storage fixture description', coverId=1,
                    cover=base64.b64encode(cover).decode())
    book = dict(id='528922e1-3829-46bd-bbc3-dda7cae8d77c', title='Storage fixture',
                author='Fixture author', readingStatus='to-read', metadata=metadata)
    legacy = json.dumps(dict(version=1, books=[book])).encode()
    fixture = args.output / 'legacy.json'
    fixture.write_bytes(legacy)
    args.output.joinpath('environment.json').write_text(json.dumps(dict(
        model=d.shell('getprop', 'ro.product.model'), api=d.shell('getprop', 'ro.build.version.sdk'),
        apk_sha256=hashlib.sha256(args.apk.read_bytes()).hexdigest(),
        commit=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()), indent=2))
    try:
        d.adb('install', '-r', str(args.apk))
        d.shell('am', 'force-stop', PACKAGE)
        d.shell('run-as', PACKAGE, 'rm', '-rf', 'files/verification-library.json.covers',
                'files/verification-library.json.v1.bak', 'files/verification-library.json.tmp',
                'files/verification-library.json.v1.bak.tmp')
        d.adb('push', str(fixture), '/data/local/tmp/folium-storage-fixture.json')
        d.shell('run-as', PACKAGE, 'cp', '/data/local/tmp/folium-storage-fixture.json', 'files/verification-library.json')
        d.mode('offline')
        d.start()
        d.tap('Storage fixture')
        d.assert_saved_cover()
        d.capture('01-legacy-cover')
        d.scroll_to('Reading')
        d.tap('Reading')
        document = d.library()
        assert document['version'] == 2
        saved = document['books'][0]
        assert saved['id'] == book['id'] and saved['readingStatus'] == 'reading'
        assert not saved['metadata']['cover']
        assert saved['coverFile'] == hashlib.sha256(cover).hexdigest()
        rollback = d.adb('exec-out', 'run-as', PACKAGE, 'cat', 'files/verification-library.json.v1.bak', binary=True)
        assert rollback == legacy
        blob = 'files/verification-library.json.covers/' + saved['coverFile']
        before = d.adb('exec-out', 'run-as', PACKAGE, 'cat', blob, binary=True)
        assert before == cover
        d.record('Legacy cover decoded; first status save migrated with exact rollback copy')
        d.start()
        d.tap('Storage fixture')
        d.assert_saved_cover()
        d.wait('Fixture publisher', 'TextBlock')
        d.capture('02-offline-restart')
        d.scroll_to('Finished')
        d.tap('Finished')
        assert d.library()['books'][0]['readingStatus'] == 'finished'
        assert d.adb('exec-out', 'run-as', PACKAGE, 'cat', blob, binary=True) == cover
        d.start()
        d.tap('Storage fixture')
        d.assert_saved_cover()
        d.capture('03-second-offline-restart')
        assert d.library()['books'][0]['readingStatus'] == 'finished'
        d.record('Two process restarts retained offline cover, metadata, source IDs, and status edits')
        backup = args.output / 'portable-backup.json'
        book['readingStatus'] = 'finished'
        backup.write_text(json.dumps(dict(kind='personal-library-backup', version=1, books=[book])))
        d.adb('push', str(backup), '/sdcard/Download/folium-storage-verification-backup.json')
        d.shell('run-as', PACKAGE, 'rm', blob)
        d.start()
        d.wait('Folium could not open your library. The saved file is unchanged.', 'TextBlock')
        d.capture('04-missing-cover-recovery')
        d.tap('Restore backup')

        def system_tap(text):
            for _ in range(15):
                tree = d.tree()
                found = [n for n in tree.iter('node')
                         if n.get('package') in ('com.google.android.documentsui', 'com.android.documentsui')
                         and (n.get('text') == text or n.get('content-desc') == text)]
                if found:
                    x1, y1, x2, y2 = map(int, re.findall(r'\d+', found[0].get('bounds')))
                    d.shell('input', 'tap', str((x1+x2)//2), str((y1+y2)//2))
                    return
                time.sleep(.3)
            raise AssertionError('The document picker does not contain the required fixture action: ' + text)

        system_tap('Show roots')
        system_tap('Downloads')
        system_tap('folium-storage-verification-backup.json')
        d.wait('Replace your library?', 'TextBlock')
        d.capture('05-recovery-confirmation')
        d.tap('Replace library')
        d.wait('Storage fixture', 'Button')
        d.tap('Storage fixture')
        d.assert_saved_cover()
        d.capture('06-recovered-cover')
        d.record('Missing cover recovered through Android picker and explicit backup confirmation')

    finally:
        d.shell('am', 'force-stop', PACKAGE)
        d.adb('install', '-r', str(args.restore_apk))
        assert digest() == original, 'The original library changed.'
        d.record('Original APK restored; original library hash unchanged')
        d.shell('rm', '-f', '/data/local/tmp/folium-storage-fixture.json',
                '/sdcard/Download/folium-storage-verification-backup.json')


if __name__ == '__main__':
    main()
