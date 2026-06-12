#!/usr/bin/env bash
# Stop hook: keep CLAUDE.md's context in sync with the code.
#
# If any code file is newer than CLAUDE.md, exit 2 to block the stop and re-wake
# the model with an instruction to refresh CLAUDE.md. Once CLAUDE.md is the most
# recently written file, this passes (exit 0) and the turn ends normally — so
# there is no loop: updating CLAUDE.md is exactly what clears the condition.

set -euo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
md="$root/CLAUDE.md"

# Nothing to keep in sync if CLAUDE.md doesn't exist yet.
[ -f "$md" ] || exit 0

# First code file modified more recently than CLAUDE.md (build output and tooling
# dirs excluded). -quit stops at the first hit so this stays cheap.
newer="$(find "$root" -type f \
  \( -name '*.cs'  -o -name '*.csproj' -o -name '*.sln'  -o -name '*.slnx' \
     -o -name '*.json' -o -name '*.yml' -o -name '*.yaml' -o -name '*.sh' \
     -o -name '*.http' -o -name 'Dockerfile' \) \
  -newer "$md" \
  -not -path '*/bin/*' -not -path '*/obj/*' \
  -not -path '*/.git/*' -not -path '*/.claude/*' \
  -print -quit 2>/dev/null || true)"

# No code newer than CLAUDE.md → context is current → allow stop.
[ -z "$newer" ] && exit 0

# Re-wake the model. stderr on a Stop hook exit-2 is fed back as the reason.
rel="${newer#"$root"/}"
echo "Code changed since CLAUDE.md was last updated (e.g. ${rel}). Update the 'Current context' section of CLAUDE.md so it reflects the current code, then finish. If nothing about the project's shape, features, or conventions actually changed, just re-save CLAUDE.md to acknowledge it is current." >&2
exit 2
