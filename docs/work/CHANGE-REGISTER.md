# Change register

<!-- The delivery record. Newest entry at the top, under today's date.
     One entry per commit or coherent piece of work. Log uncommitted, in-flight work too. -->

## 2026-10-16

### Adopt Lancom engineering standard — AISDLC-12124
- **Who:** Jack Bates / Claude Code
- **Branch:** chore/AISDLC-12124-adopt-standard — **Commits:** uncommitted
- **Touched:** AGENTS.md, CLAUDE.md, docs/work/, docs/architecture/, .github/workflows/, tools/record-currency.sh
- **Why:** Set the repository up against the standard (AISDLC-12124).
- **Verified:** `git status` shows only the intended files; the commit-msg hook is installed and executable.
- **Not verified:** The CI workflows have not run on GitHub. No test project exists, so no tests run in CI. CODEOWNERS is absent and the fence test has not been run.
