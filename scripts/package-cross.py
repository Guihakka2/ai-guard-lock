"""Package a cross-published .NET folder with Unix permissions preserved."""
import argparse
import hashlib
import json
import plistlib
import shutil
import struct
import tarfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def executable(path: Path) -> bool:
    return path.name == 'AiGuardLock.Portable' or path.suffix in {'.sh', '.dylib', '.so'} or '.so.' in path.name or path.name == 'createdump'


def icns(destination: Path) -> None:
    data = (ROOT / 'src/AiGuardLock/Assets/AppIcon.ico').read_bytes()
    count = struct.unpack_from('<H', data, 4)[0]
    chunks = []
    types = {16: b'icp4', 32: b'icp5', 64: b'icp6', 128: b'ic07', 256: b'ic08'}
    for index in range(count):
        width, _, _, _, _, _, size, offset = struct.unpack_from('<BBBBHHII', data, 6 + index * 16)
        width = width or 256
        if width in types:
            frame = data[offset:offset + size]
            chunks.append(types[width] + struct.pack('>I', len(frame) + 8) + frame)
    frame = (ROOT / 'src/AiGuardLock/Assets/AppIcon.png').read_bytes()
    chunks.append(b'ic09' + struct.pack('>I', len(frame) + 8) + frame)
    payload = b''.join(chunks)
    destination.write_bytes(b'icns' + struct.pack('>I', len(payload) + 8) + payload)


def notices(destination: Path, runtime: str) -> None:
    target = destination / 'THIRD-PARTY'
    target.mkdir()
    assets = json.loads((ROOT / 'src/AiGuardLock.CrossPlatform/obj/project.assets.json').read_text(encoding='utf-8-sig'))
    package_folders = [Path(p) for p in assets['packageFolders']]
    for item in (ROOT / 'docs/third-party').glob('*.txt'):
        shutil.copy2(item, target / item.name)
    found_runtime = False
    for folder in package_folders:
        for package in (folder / f'microsoft.netcore.app.runtime.{runtime}').glob('*'):
            if not package.is_dir():
                continue
            for item in package.iterdir():
                if item.is_file() and any(word in item.name.upper() for word in ('LICENSE', 'NOTICE')):
                    shutil.copy2(item, target / f'dotnet-{runtime}-{package.name}-{item.name}')
                    found_runtime = True
    if not found_runtime:
        raise SystemExit(f'Missing .NET runtime license files for {runtime}')
    info = []
    for identity, library in assets['libraries'].items():
        if library['type'] != 'package':
            continue
        name, version = identity.split('/')
        info.append(f'{name} {version}\nhttps://www.nuget.org/packages/{name}/{version}')
        for folder in package_folders:
            package = folder / library['path']
            if not package.is_dir():
                continue
            for item in package.iterdir():
                if item.is_file() and any(word in item.name.upper() for word in ('LICENSE', 'NOTICE', 'COPYING')):
                    shutil.copy2(item, target / f'{name}-{version}-{item.name}')
            for item in package.glob('*.nuspec'):
                shutil.copy2(item, target / f'{name}-{version}.nuspec')
    (target / 'DEPENDENCIES.txt').write_text('\n\n'.join(info) + '\n', encoding='utf-8')


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('--runtime', choices=['linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64'], required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--publish', type=Path, required=True)
    args = parser.parse_args()
    name = f'AI-Guard-Lock-{args.version}-{args.runtime}-preview'
    destination = ROOT / 'dist' / name
    suffix = '.zip' if args.runtime.startswith('osx-') else '.tar.gz'
    archive = ROOT / 'dist' / (name + suffix)
    if destination.exists() or archive.exists():
        raise SystemExit('Release already exists; choose a new version.')
    destination.mkdir(parents=True)
    if args.runtime.startswith('osx-'):
        contents = destination / 'AI Guard Lock.app' / 'Contents'
        output = contents / 'MacOS'
        resources = contents / 'Resources'
        resources.mkdir(parents=True)
        shutil.copytree(args.publish, output)
        manifest = {
            'CFBundleName': 'AI Guard Lock', 'CFBundleDisplayName': 'AI Guard Lock',
            'CFBundleIdentifier': 'local.aiguardlock.portable', 'CFBundlePackageType': 'APPL',
            'CFBundleExecutable': 'AiGuardLock.Portable', 'CFBundleIconFile': 'AppIcon.icns',
            'CFBundleVersion': args.version, 'CFBundleShortVersionString': args.version,
            'CFBundleInfoDictionaryVersion': '6.0', 'LSMinimumSystemVersion': '14.0',
            'NSHighResolutionCapable': True,
        }
        (contents / 'Info.plist').write_bytes(plistlib.dumps(manifest))
        icns(resources / 'AppIcon.icns')
        (destination / 'entitlements.plist').write_bytes(plistlib.dumps({'com.apple.security.cs.allow-jit': True}))
        script = (ROOT / 'scripts/sign-macos.sh').read_text(encoding='utf-8-sig')
        (destination / 'sign-macos.sh').write_text(script, encoding='utf-8', newline='\n')
    else:
        output = destination / 'app'
        shutil.copytree(args.publish, output)
        shutil.copy2(ROOT / 'src/AiGuardLock/Assets/AppIcon.png', destination / 'AppIcon.png')
        launcher = '#!/bin/sh\nset -eu\nbase=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\nexec "$base/app/AiGuardLock.Portable" "$@"\n'
        (destination / 'launch.sh').write_text(launcher, encoding='utf-8', newline='\n')
    shutil.copy2(ROOT / 'docs/CROSS-PLATFORM-JA.txt', destination / 'README-JA.txt')
    notices(destination, args.runtime)
    sums = []
    for path in sorted(destination.rglob('*')):
        if path.is_file():
            sums.append(f'{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.relative_to(destination).as_posix()}')
    (destination / 'SHA256SUMS.txt').write_text('\n'.join(sums) + '\n', encoding='utf-8')
    if suffix == '.zip':
        with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
            for path in sorted(destination.rglob('*')):
                if not path.is_file():
                    continue
                entry = zipfile.ZipInfo(path.relative_to(destination.parent).as_posix())
                entry.create_system = 3
                entry.external_attr = ((0o100755 if executable(path) else 0o100644) << 16)
                entry.compress_type = zipfile.ZIP_DEFLATED
                bundle.writestr(entry, path.read_bytes())
    else:
        with tarfile.open(archive, 'w:gz', compresslevel=6) as bundle:
            for path in sorted(destination.rglob('*')):
                entry = bundle.gettarinfo(str(path), arcname=path.relative_to(destination.parent).as_posix())
                entry.uid = entry.gid = 0
                entry.uname = entry.gname = ''
                entry.mode = 0o755 if path.is_dir() or executable(path) else 0o644
                if path.is_file():
                    with path.open('rb') as stream:
                        bundle.addfile(entry, stream)
                else:
                    bundle.addfile(entry)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    Path(str(archive) + '.sha256').write_text(f'{digest}  {archive.name}\n', encoding='ascii')
    print(f'{archive} ({archive.stat().st_size / 1024 / 1024:.1f} MiB)')


if __name__ == '__main__':
    main()
