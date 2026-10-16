#!/bin/sh
# Fails when a changeset touches anything outside docs/work/ and does NOT update the
# delivery register. The rule the standard has always stated, finally executable:
# "maintain CHANGE-REGISTER.md as you work" drifted for as long as nothing checked it.
#
# Two modes:
#   record-currency.sh --staged          the index (local commit-msg hook)
#   record-currency.sh --range <base>    <base>..HEAD (CI, pull requests)
#
# Exit 0 = compliant or exempt. Exit 1 = the register was not touched. The CALLER
# decides whether that blocks: the hook warns by default, CI fails.
#
# LOCATION-AWARE. Standard v1.2 moved the register to docs/work/; repositories that have
# not migrated still have it at the root. Both are accepted, so this gate can ship before
# the migrations rather than after them.
set -u
REG_NEW="docs/work/CHANGE-REGISTER.md"
REG_OLD="CHANGE-REGISTER.md"

mode=${1:-}; arg=${2:-}
case "$mode" in
  --staged) files=$(git diff --cached --name-only 2>/dev/null) ;;
  --range)  [ -n "$arg" ] || { echo "record-currency: --range needs a base ref" >&2; exit 0; }
            git rev-parse --verify -q "$arg" >/dev/null 2>&1 || {
              echo "record-currency: base ref '$arg' not found - cannot compare, not failing" >&2; exit 0; }
            files=$(git diff --name-only "$arg"...HEAD 2>/dev/null) ;;
  *) echo "usage: record-currency.sh --staged | --range <base>" >&2; exit 0 ;;
esac

[ -n "$files" ] || exit 0          # nothing to check

# A merge commit carries someone else's work; it is not a piece of work of its own.
if [ "$mode" = --range ] && git rev-parse -q --verify HEAD^2 >/dev/null 2>&1; then
  echo "record-currency: merge commit at HEAD - exempt"; exit 0
fi

# Documented escape, deliberately one that leaves a trace in history. An escape hatch
# nobody can see is how a gate quietly stops gating.
#
# --range ONLY. In --staged mode HEAD is the PREVIOUS commit, whose message has nothing
# to do with what is being committed now - honouring it there would let one exempted
# commit silently exempt every commit after it. Staged mode only warns in any case.
if [ "$mode" = --range ] \
   && git log -1 --pretty=%B HEAD 2>/dev/null | grep -qiE '^Record-exempt:[[:space:]]*[^[:space:]]'; then
  echo "record-currency: Record-exempt trailer present on HEAD - exempt, and visible in history"
  exit 0
fi

touched_register=no
touched_other=no
for f in $files; do
  case "$f" in
    "$REG_NEW"|"$REG_OLD") touched_register=yes ;;
    docs/work/*)           : ;;   # sibling records: neither triggers nor satisfies
    *)                     touched_other=yes ;;
  esac
done

[ "$touched_other" = yes ] || exit 0
[ "$touched_register" = no ] || exit 0

if [ -f "$REG_NEW" ]; then reg="$REG_NEW"; elif [ -f "$REG_OLD" ]; then reg="$REG_OLD"; else reg="$REG_NEW"; fi
cat >&2 <<MSG
RECORD NOT CURRENT: this change touches code but does not update $reg.

  The register is the delivery record: who, what, why, Verified, and the mandatory
  Not verified line. Written after the fact it records what you remember, which is
  how an entry ends up saying the tests passed when they passed on an earlier file.

  Add the entry, or state a reason in the commit message:
      Record-exempt: <why this change needs no register entry>
MSG
exit 1
