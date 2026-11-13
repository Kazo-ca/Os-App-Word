#!/usr/bin/env bash
# Compiles WordCraft from the external/wordcraft submodule (our fork, decision 9 of doc/en/plan.md)
# the way its own release workflow builds the web zip, then vendors the result in
# src/Kazo.Word/wwwroot/wordcraft/ with tools/vendor-wordcraft.sh --dir (work item 4 of
# doc/en/integration-plan.md). The .github/workflows/build-wordcraft.yml workflow runs it; it
# also runs on a developer machine.
#
# Usage:
#   CRAFT_I18N_DIR=<checkout> CRAFT_FONTS_DIR=<checkout> tools/build-wordcraft.sh
#
# Needs: Rust stable with the wasm32-unknown-unknown target, Trunk at the version below, zip and
# unzip, and checkouts of storytold/craft-i18n and storytold/craft-fonts at the revisions the
# submodule pins (craft-i18n.rev, and craft-fonts-ref in its .github/workflows/release.yml). The
# script checks those revisions and refuses others, so our build embeds the same proofing data
# and fonts as the official zip of the same commit.
set -euo pipefail

# The Trunk version of WordCraft's release workflow (craft-common-actions, install-trunk).
trunk_version="0.21.14"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
wordcraft="$repo_root/external/wordcraft"

case "${1:-}" in
  "") ;;
  -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
  *) echo "Unknown argument: $1" >&2; exit 2 ;;
esac

fail() { echo "error: $*" >&2; exit 1; }

[[ -f "$wordcraft/Cargo.toml" ]] || fail "external/wordcraft is empty. Run: git submodule update --init external/wordcraft"
# A local change of WordCraft must go through a commit of the fork, so the provenance names it.
[[ -z "$(git -C "$wordcraft" status --porcelain)" ]] || fail "external/wordcraft has local changes; commit them in the fork first"

: "${CRAFT_I18N_DIR:?set CRAFT_I18N_DIR to a checkout of storytold/craft-i18n}"
: "${CRAFT_FONTS_DIR:?set CRAFT_FONTS_DIR to a checkout of storytold/craft-fonts}"

# The revisions WordCraft pins, read from the submodule so a version bump needs no edit here.
i18n_rev="$(tr -d '[:space:]' < "$wordcraft/craft-i18n.rev")"
fonts_rev="$(sed -n 's/^ *craft-fonts-ref: *\([0-9a-f]\{40\}\).*/\1/p' "$wordcraft/.github/workflows/release.yml" | head -n 1)"
[[ "$i18n_rev" =~ ^[0-9a-f]{40}$ ]] || fail "external/wordcraft/craft-i18n.rev holds no commit SHA"
[[ "$fonts_rev" =~ ^[0-9a-f]{40}$ ]] || fail "no craft-fonts-ref in external/wordcraft/.github/workflows/release.yml"

check_rev() {
  local name="$1" dir="$2" expected="$3" actual
  actual="$(git -C "$dir" rev-parse HEAD 2>/dev/null)" || fail "$dir is not a git checkout of $name"
  [[ "$actual" == "$expected" ]] || fail "$name is at $actual; WordCraft pins $expected (git -C $dir checkout $expected)"
}
check_rev craft-i18n "$CRAFT_I18N_DIR" "$i18n_rev"
check_rev craft-fonts "$CRAFT_FONTS_DIR" "$fonts_rev"

command -v trunk >/dev/null || fail "trunk not found; install Trunk $trunk_version"
actual_trunk="$(trunk --version | awk '{print $2}')"
[[ "$actual_trunk" == "$trunk_version" ]] || fail "trunk $actual_trunk found; WordCraft's release uses $trunk_version"
rustup target list --installed 2>/dev/null | grep -qx wasm32-unknown-unknown \
  || fail "the wasm32-unknown-unknown target is missing (rustup target add wasm32-unknown-unknown)"

commit="$(git -C "$wordcraft" rev-parse HEAD)"
rustc_version="$(rustc --version)"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# WordCraft's own packaging script: trunk build --release, then the licences, NOTICE and the font
# licences next to the site. The build date is the commit's (UTC), so building the same commit
# twice bakes the same date into the About window.
build_date="$(TZ=UTC git -C "$wordcraft" log -1 --format=%cd --date=format-local:%Y-%m-%d)"
echo "Building WordCraft $commit (rustc: $rustc_version, trunk $actual_trunk)"
(
  export CRAFT_I18N_DIR CRAFT_FONTS_DIR
  export CRAFT_I18N_REQUIRED=1 CRAFT_FONTS_REQUIRED=1
  export WORDCRAFT_BUILD_SHA="$commit" WORDCRAFT_BUILD_DATE="$build_date"
  export DIST="$work/dist"
  export CARGO_TARGET_DIR="${CARGO_TARGET_DIR:-$wordcraft/target}"
  cd "$wordcraft"
  packaging/web/package.sh
)

shopt -s nullglob
zips=("$work"/dist/wordcraft-web-*.zip)
shopt -u nullglob
[[ ${#zips[@]} -eq 1 ]] || fail "packaging/web/package.sh made no single wordcraft-web-*.zip"
unzip -q "${zips[0]}" -d "$work/site"
site=("$work"/site/wordcraft-web-*)
[[ ${#site[@]} -eq 1 && -d "${site[0]}" ]] || fail "the zip has no single wordcraft-web-*/ folder"

"$repo_root/tools/vendor-wordcraft.sh" --dir "${site[0]}" \
  --craft-i18n "$i18n_rev" --craft-fonts "$fonts_rev" \
  --rustc "$rustc_version" --trunk "$actual_trunk"
