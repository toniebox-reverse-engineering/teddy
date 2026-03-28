#!/bin/bash

# Generate changelog from git history
# Usage: ./generate-changelog.sh [from-tag] [to-tag/branch]

FROM_REF="${1:-}"
TO_REF="${2:-HEAD}"

if [ -z "$FROM_REF" ]; then
  # If no from-ref provided, show all commits
  echo "## What's Changed"
  echo ""

  # Group commits by category
  echo "### Features"
  git log --oneline --no-merges --grep="^Add\|^Implement\|^feat" --grep="^Revert" --invert-grep "$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true

  echo ""
  echo "### Improvements"
  git log --oneline --no-merges --grep="^Improve\|^Update\|^Enhance\|^Refactor" --grep="^Revert" --invert-grep "$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true

  echo ""
  echo "### Bug Fixes"
  git log --oneline --no-merges --grep="^Fix\|^fix" --grep="^Revert" --invert-grep "$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true

  echo ""
  echo "### Other Changes"
  git log --oneline --no-merges --grep="^Add\|^Implement\|^feat\|^Improve\|^Update\|^Enhance\|^Refactor\|^Fix\|^fix" --invert-grep --grep="^Revert" --invert-grep "$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true
else
  echo "## What's Changed"
  echo ""

  # Group commits by category
  echo "### Features"
  git log --oneline --no-merges --grep="^Add\|^Implement\|^feat" --grep="^Revert" --invert-grep "$FROM_REF..$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true

  echo ""
  echo "### Improvements"
  git log --oneline --no-merges --grep="^Improve\|^Update\|^Enhance\|^Refactor" --grep="^Revert" --invert-grep "$FROM_REF..$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true

  echo ""
  echo "### Bug Fixes"
  git log --oneline --no-merges --grep="^Fix\|^fix" --grep="^Revert" --invert-grep "$FROM_REF..$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true

  echo ""
  echo "### Other Changes"
  git log --oneline --no-merges --grep="^Add\|^Implement\|^feat\|^Improve\|^Update\|^Enhance\|^Refactor\|^Fix\|^fix" --invert-grep --grep="^Revert" --invert-grep "$FROM_REF..$TO_REF" | \
    sed 's/^[a-f0-9]* /- /' || true
fi

echo ""
echo "**Full Changelog**: https://github.com/$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')/compare/${FROM_REF}...${TO_REF}"
