# Cageless — Project charter

This document is the **shared contract** for what we are building: an MMORPG named **Cageless**, using **Godot 4**. Update it as decisions change so design, code, and tooling stay aligned.

---

## 1. Identity

| Field | Value |
|--------|--------|
| **Working title** | Cageless |
| **Engine** | Godot 4.x |
| **Genre** | MMORPG focused heavily on crafting |
| **Elevator pitch** | A skill based mmorpg that doesnt feel like a job where you have to grind for hours |

---

## 2. Design pillars

List 3–5 non-negotiable qualities. Everything we add should reinforce at least one pillar.

1. All systems connect and encourage competitve cooperation
2. Game is economy driven
3. Competition not combat
4. *(e.g. performance on modest hardware)*
5. *(optional)*

---

## 3. Scope rules (what we are / are not building)

### In scope (initial phases)

- *(e.g. one starting zone, core loop, networking prototype)*
- Godot 4 project structure, naming, and conventions we agree on here

### Out of scope (for now)

- *(e.g. full quest VO, console ports, NFTs)*
- Features that require backend scale before we have a stable client loop

### “Cageless” naming rule

Use **Cageless** consistently for the product. Internal codenames are fine only if documented here.

---

## 4. Player experience guardrails

- **Target session length:** 20mins
- **Solo vs group:** Solo in the early game, transistioning into group content and game becomes more difficult and complicated
- **Monetization:** subscription
- **Content rating / tone:** unmoderated, content of game itself teen

---

## 5. Technical rules (Godot 4)

### Versioning

- Pin a **minimum Godot 4.x** version here once chosen *(e.g. 4.3+)*.

### Project layout *(edit to match repo)*

- `scenes/` — gameplay scenes
- `scripts/` — GDScript / C# as chosen
- `assets/` — art, audio, data
- `addons/` — third-party plugins only when necessary

### Networking *(high level)*

- **Authoritative server**
- Document chosen stack: dedicated server binary

### Performance

- Targets: max players per shard
- Prefer: deterministic

---

## 6. Collaboration rules (human + assistant)

- **Source of truth:** This charter + committed design notes beat chat memory.
- **When adding systems:** Name which pillar they serve and whether they need server authority.
- **Commits:** Small, focused changes; no drive-by refactors unrelated to the task.
- **Secrets:** No API keys or production credentials in the repo; use env / local config (document pattern here).

---

## 7. Open questions

Track decisions we still owe:

- [ ] Sub-genre and core loop (first playable)
- [ ] Server architecture and hosting plan
- [ ] GDScript vs C# for gameplay
- [ ] Art style / camera (2D, 3D, hybrid)
- [ ] Target platforms (Windows only first, etc.)

---

## 8. Repositories

Source is split across three Git repositories (see `GITHUB_SETUP.md` in this folder for creating remotes and pushing).

| Repository | Purpose |
|------------|---------|
| **cageless-client** | Godot 4 client |
| **cageless-server** | Authoritative server |
| **cageless-shared-code** | Shared protocols, schemas, tooling |

**GitHub URLs** (account **reganross**):

- Client: https://github.com/reganross/cageless-client
- Server: https://github.com/reganross/cageless-server
- Shared: https://github.com/reganross/cageless-shared-code

---

## 9. Revision log

| Date | Change |
|------|--------|
| 2026-03-28 | Initial charter created |
| 2026-03-28 | Added repository layout (three repos) |
| 2026-03-28 | GitHub remotes created under reganross; URLs recorded |

---

*Next step after filling sections 1–5: create the Godot 4 project in **cageless-client** and link the engine version in section 5.*
