#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
asset_dir="${1:-dist}"
version="$(tr -d '\r\n' < VERSION)"
tag="v$version"
target="$(git rev-parse HEAD)"
if [[ "${GITHUB_REF_TYPE:-}" == tag && "${GITHUB_REF_NAME:-}" != "$tag" ]]; then
    echo "The Git tag must match VERSION ($tag)." >&2; exit 1
fi
python3 scripts/package-release.py checksums --directory "$asset_dir"
# An existing tag must refer to exactly the sources used by both builds.
tag_lines="$(git ls-remote --tags origin "refs/tags/$tag" "refs/tags/$tag^{}")"
if [[ -n "$tag_lines" ]]; then
    tagged="$(printf '%s\n' "$tag_lines" | tail -1 | awk '{print $1}')"
    [[ "$tagged" == "$target" ]] || { echo "$tag points to a different commit." >&2; exit 1; }
fi
notes="docs/releases/$tag.md"
if [[ -f "$notes" ]]; then note_args=(--notes-file "$notes")
else note_args=(--notes "SuperCopy $version for Windows and macOS. See README for setup, shortcuts and permissions.")
fi
if draft="$(gh release view "$tag" --json isDraft --jq .isDraft 2>/dev/null)"; then
    [[ "$draft" == true ]] || { echo "$tag is already published." >&2; exit 1; }
    draft_target="$(gh release view "$tag" --json targetCommitish --jq .targetCommitish)"
    [[ "$draft_target" == "$target" ]] || { echo "Existing draft targets different sources." >&2; exit 1; }
else
    gh release create "$tag" --draft --target "$target" --title "SuperCopy $version — Windows & macOS" "${note_args[@]}"
fi
gh release upload "$tag" --clobber \
    "$asset_dir/SuperCopy-Windows-v$version-English.zip" \
    "$asset_dir/SuperCopy-macOS-v$version-English.zip" \
    "$asset_dir/SuperCopy-Source-v$version-English.zip" \
    "$asset_dir/SHA256SUMS.txt"
# Keep the release private until all expected assets exist with the correct sizes.
gh release view "$tag" --json assets | python3 -c '
import json, pathlib, sys
assets = {a["name"]: a for a in json.load(sys.stdin)["assets"]}
directory, version = pathlib.Path(sys.argv[1]), sys.argv[2]
expected = [f"SuperCopy-{platform}-v{version}-English.zip" for platform in ("Windows", "macOS", "Source")] + ["SHA256SUMS.txt"]
for name in expected:
    item = assets.get(name, {})
    if item.get("state") != "uploaded" or item.get("size") != (directory / name).stat().st_size:
        raise SystemExit(f"Release asset not ready: {name}")
' "$asset_dir" "$version"
gh release edit "$tag" --draft=false --latest
