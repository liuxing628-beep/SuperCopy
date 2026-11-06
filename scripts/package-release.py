#!/usr/bin/env python3
"""Package build outputs for the shared GitHub release."""
from pathlib import Path
import argparse
import hashlib
import plistlib
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parent.parent
DIST = ROOT / "dist"
VERSION = (ROOT / "VERSION").read_text().strip()

def validate_version():
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", VERSION):
        raise SystemExit("VERSION must contain a three-part version.")
    assembly = (ROOT / "src" / "AssemblyInfo.cs").read_text()
    for attribute in ("AssemblyVersion", "AssemblyFileVersion"):
        if f'{attribute}("{VERSION}.0")' not in assembly:
            raise SystemExit(f"{attribute} must match VERSION ({VERSION}.0).")

def asset_name(platform):
    return f"SuperCopy-{platform}-v{VERSION}-English.zip"

def windows():
    exe = DIST / "SuperCopy.exe"
    with exe.open("rb") as binary:
        if binary.read(2) != b"MZ":
            raise SystemExit("Not a Windows executable.")
    path = DIST / asset_name("Windows")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
        for file in (exe, DIST / "SuperCopy.exe.config", ROOT / "README.md"):
            archive.write(file, f"SuperCopy/{file.name}")
    print(path)

def macos():
    path = DIST / asset_name("macOS")
    with zipfile.ZipFile(path) as archive:
        plist = plistlib.loads(archive.read("SuperCopy.app/Contents/Info.plist"))
        if plist["CFBundleShortVersionString"] != VERSION:
            raise SystemExit("macOS bundle version must match VERSION.")
        if archive.testzip() is not None or "SuperCopy.app/Contents/MacOS/SuperCopy" not in archive.namelist():
            raise SystemExit("Incomplete or corrupt macOS package.")
    print(path)


def source():
    path = DIST / asset_name("Source")
    subprocess.run(["git", "archive", "--format=zip", "--prefix=SuperCopy-Source/", "-o", str(path), "HEAD"], cwd=ROOT, check=True)
    print(path)

def checksums(directory):
    names = [asset_name(platform) for platform in ("Windows", "macOS", "Source")]
    expected = ["SuperCopy/SuperCopy.exe", "SuperCopy.app/Contents/MacOS/SuperCopy", "SuperCopy-Source/VERSION"]
    lines = []
    for name, member in zip(names, expected):
        path = directory / name
        with zipfile.ZipFile(path) as archive:
            if archive.testzip() is not None or member not in archive.namelist():
                raise SystemExit(f"Incomplete or corrupt release asset: {name}")
            if name == asset_name("Source") and archive.read(member).decode().strip() != VERSION:
                raise SystemExit("Source archive version mismatch.")
        lines.append(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {name}")
    (directory / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="ascii")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("step", choices=("validate", "windows", "macos", "source", "checksums"))
    parser.add_argument("--directory", type=Path, default=DIST)
    args = parser.parse_args()
    validate_version()
    DIST.mkdir(exist_ok=True)
    if args.step == "checksums":
        checksums(args.directory)
    elif args.step != "validate":
        globals()[args.step]()
