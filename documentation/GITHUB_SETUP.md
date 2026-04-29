# GitHub setup for Cageless

GitHub CLI (`gh`) is installed. **Creating repositories requires you to sign in once** on this machine.

## 1. Sign in to GitHub

```powershell
gh auth login
```

Choose GitHub.com, HTTPS or SSH as you prefer, and complete browser or token authentication.

Verify:

```powershell
gh auth status
```

## 2. Choose owner: user vs organization

Replace `OWNER` below with either **your GitHub username** or an **organization name** (for example `cageless` if you create that org on github.com first).

> **Note:** GitHub does not create an “organization” from the CLI. If you want all repos under `github.com/cageless/...`, create the organization in the browser: [github.com/organizations/new](https://github.com/organizations/new), then use `OWNER=cageless` in the commands.

## 3. Create the three repositories

From any directory (repos are created on GitHub only; you will add remotes in step 4):

```powershell
$OWNER = "YOUR_USER_OR_ORG"

gh repo create "$OWNER/cageless-client" --public --description "Cageless MMORPG — Godot 4 client"
gh repo create "$OWNER/cageless-server" --public --description "Cageless MMORPG — game server"
gh repo create "$OWNER/cageless-shared-code" --public --description "Cageless MMORPG — shared protocols and tooling"
```

Use `--private` instead of `--public` if you want private repositories.

## 4. Point local folders at GitHub and push

Paths match the folders created under `Projects`:

```powershell
$OWNER = "YOUR_USER_OR_ORG"

# Client
cd C:\Users\regan\Projects\cageless-client
git remote add origin "https://github.com/$OWNER/cageless-client.git"
git branch -M main
git push -u origin main

# Server
cd C:\Users\regan\Projects\cageless-server
git remote add origin "https://github.com/$OWNER/cageless-server.git"
git branch -M main
git push -u origin main

# Shared
cd C:\Users\regan\Projects\cageless-shared-code
git remote add origin "https://github.com/$OWNER/cageless-shared-code.git"
git branch -M main
git push -u origin main
```

If you use SSH remotes, use `git@github.com:$OWNER/cageless-client.git` (and the same pattern for the other two).

## 5. Optional: GitHub Project “Cageless”

A **Project** is a board for issues/PRs (separate from repositories). After `gh auth login`:

```powershell
# Under your personal account
gh project create --owner "@me" --title "Cageless"

# Under an organization (replace with the org login)
gh project create --owner cageless --title "Cageless"
```

You can also create a project from the GitHub website: **Your profile or org → Projects → New project**.

## 6. Update README links

Search and replace `REPLACE_ORG_USER` in each repo’s `README.md` with your real `OWNER` value.
