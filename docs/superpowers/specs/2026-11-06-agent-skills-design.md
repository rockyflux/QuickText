# Agent Skills browser & import

## Goal

Browse local Agent Skills under `%USERPROFILE%\.agents\skills\` (Windows), favorite them into JSON, optionally alias for display, and batch-import favorites into the Manager as snippets.

## Behavior

- **Scan**: recursive `SKILL.md` under the user skills root; TreeView by relative folders. A directory with `SKILL.md` is a skill node and may still have child skills.
- **Display name**: `name` + truncated `description` from YAML frontmatter (folder name if missing). Alias overrides display only.
- **Favorite JSON** (`skills-favorites.json` next to `settings.json`): each item has `name`, `body` (absolute `SKILL.md` path), optional `alias`.
- **Import**: Manager “Skills” (left of snippet +) opens the same window in import mode; multi-select favorites → create snippets with stored `name` / `body`.
- **Entry**: tray menu and floating-ball menu.

## Out of scope

`.claude/skills`, abbreviation changes, cloud sync of favorites beyond existing machine-state paths.
