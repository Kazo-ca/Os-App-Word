#!/usr/bin/env bash
# Puts a WordCraft web build in src/Kazo.Word/wwwroot/wordcraft/ (decision 13 of doc/en/plan.md,
# section 3 of doc/en/integration-plan.md). That folder is never edited by hand: this script
# replaces it whole and writes wordcraft.json, its provenance (version, source, name, size and
# SHA-256 of each file). tests/Kazo.Word.Tests checks the files still match it.
#
# Usage:
#   tools/vendor-wordcraft.sh                 download the pinned official zip and check it
#   tools/vendor-wordcraft.sh --zip <file>    use a zip already downloaded (still checked)
#
# Work item 4 adds our own build (a folder made by Trunk) as a second source.
set -euo pipefail

# The pinned release. Moving to another version changes these three lines, in a PR.
version="0.18.1"
zip_url="https://github.com/storytold/wordcraft/releases/download/v${version}/wordcraft-web-${version}.zip"
zip_sha256="3daa7c2d1f209a7eeb2527fcefa3fe31e875848fb1970d68f54edae55fe0bc9f"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target="$repo_root/src/Kazo.Word/wwwroot/wordcraft"

zip_file=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --zip) zip_file="$2"; shift 2 ;;
    -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [[ -z "$zip_file" ]]; then
  zip_file="$work/wordcraft-web-${version}.zip"
  echo "Downloading $zip_url"
  curl --fail --location --silent --show-error --retry 3 -o "$zip_file" "$zip_url"
fi

actual="$(sha256sum "$zip_file" | cut -d' ' -f1)"
if [[ "$actual" != "$zip_sha256" ]]; then
  echo "SHA-256 mismatch for $zip_file: expected $zip_sha256, got $actual" >&2
  exit 1
fi

unzip -q "$zip_file" -d "$work/zip"
source_dir="$work/zip/wordcraft-web-${version}"
[[ -d "$source_dir" ]] || { echo "The zip has no wordcraft-web-${version}/ folder" >&2; exit 1; }

# Exactly one glue module and one .wasm, named by Trunk with the same hash.
shopt -s nullglob
glues=("$source_dir"/wordcraft-web-*.js)
wasms=("$source_dir"/wordcraft-web-*_bg.wasm)
shopt -u nullglob
if [[ ${#glues[@]} -ne 1 || ${#wasms[@]} -ne 1 ]]; then
  echo "Expected one wordcraft-web-*.js and one wordcraft-web-*_bg.wasm in the zip" >&2
  exit 1
fi
glue="$(basename "${glues[0]}")"
wasm="$(basename "${wasms[0]}")"

# What the app needs: the glue, the .wasm, the snippets the glue imports, and every licence and
# notice. Left out: WordCraft's own page (host/index.html replaces it), its icon (the ArtCraft
# brand is not ours to show), and the hosting notes.
rm -rf "$target"
mkdir -p "$target"
cp "$source_dir/$glue" "$source_dir/$wasm" "$target/"
cp -R "$source_dir/snippets" "$target/"
cp "$source_dir"/LICENSE-* "$source_dir/NOTICE" "$source_dir/ATTRIBUTION.md" "$source_dir"/OFL-*.txt "$target/"

# The provenance file, files sorted by path so a new version gives a readable diff.
{
  printf '{\n'
  printf '  "version": "%s",\n' "$version"
  printf '  "source": {\n'
  printf '    "kind": "release-zip",\n'
  printf '    "url": "%s",\n' "$zip_url"
  printf '    "sha256": "%s"\n' "$zip_sha256"
  printf '  },\n'
  printf '  "glue": "%s",\n' "$glue"
  printf '  "wasm": "%s",\n' "$wasm"
  printf '  "files": [\n'
  first=1
  while IFS= read -r path; do
    size="$(stat -c %s "$target/$path")"
    sha="$(sha256sum "$target/$path" | cut -d' ' -f1)"
    [[ $first -eq 1 ]] || printf ',\n'
    first=0
    printf '    { "path": "%s", "size": %s, "sha256": "%s" }' "$path" "$size" "$sha"
  done < <(cd "$target" && find . -type f ! -path ./wordcraft.json | sed 's#^\./##' | LC_ALL=C sort)
  printf '\n  ]\n'
  printf '}\n'
} > "$target/wordcraft.json"

echo "WordCraft $version vendored in ${target#"$repo_root"/}: $glue, $wasm"
