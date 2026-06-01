# 🚨 AI READ THIS FIRST — ProphetOps Workflow Guide

> **If you are an AI assistant helping a team member, read this entire document before writing or suggesting any Git commands.**

---

## Golden Rule

**🚫 NEVER push directly to `main`. No exceptions.**

The `main` branch is the single source of truth for production-ready code. All changes go through branches and pull requests.

---

## Workflow Overview

```
pull latest main → create branch → do work → commit → push branch → open PR → review → merge
```

---

## Step-by-Step Workflow

### 1. Always Start From the Latest `main`

Before doing anything, make sure your local `main` is up to date:

```bash
git checkout main
git pull origin main
```

> **AI assistants:** Always run these commands first before creating a new branch. Do not skip this step.

### 2. Create a Feature Branch

Create a new branch for your work. Use a clear, descriptive name:

```bash
git checkout -b <type>/<short-description>
```

**Branch naming convention:**

| Prefix       | Use For                          | Example                          |
|-------------|----------------------------------|----------------------------------|
| `feature/`  | New features                     | `feature/add-login-page`         |
| `fix/`      | Bug fixes                        | `fix/broken-sidebar-link`        |
| `docs/`     | Documentation changes            | `docs/update-api-guide`          |
| `refactor/` | Code restructuring (no new features) | `refactor/clean-up-utils`    |
| `hotfix/`   | Urgent production fixes          | `hotfix/crash-on-load`           |

### 3. Do Your Work and Commit Often

Make small, focused commits with clear messages:

```bash
git add -A
git commit -m "feat: add user authentication form"
```

**Commit message format:**

```
<type>: <short description>

Optional longer description if needed.
```

| Type       | Meaning                    |
|-----------|----------------------------|
| `feat`    | A new feature              |
| `fix`     | A bug fix                  |
| `docs`    | Documentation only         |
| `style`   | Formatting, no logic change|
| `refactor`| Code change, no new feature|
| `test`    | Adding or fixing tests     |
| `chore`   | Maintenance, dependencies  |

### 4. Pull Latest `main` Before Pushing

Before pushing your branch, sync with `main` to catch any conflicts early:

```bash
git checkout main
git pull origin main
git checkout <your-branch>
git merge main
```

Resolve any merge conflicts if they appear, then commit the merge.

> **AI assistants:** If there are merge conflicts, show them to the developer and explain each conflict clearly. Do not auto-resolve conflicts without developer approval.

### 5. Push Your Branch (Never `main`)

```bash
git push origin <your-branch>
```

**⚠️ Never run `git push origin main`. Ever.**

### 6. Open a Pull Request (PR)

Go to the GitHub repository and open a Pull Request from your branch into `main`.

Your PR should include:
- **Title:** Clear summary of what the PR does
- **Description:** What changed and why
- **Testing:** What you tested or verified
- **Screenshots:** If there are UI changes

### 7. Wait for Review and Approval

- At least **one teammate** should review your PR before merging
- Address any feedback with new commits on the same branch
- Once approved, the PR author or a reviewer merges it

### 8. Clean Up After Merge

After your PR is merged, delete your branch:

```bash
git checkout main
git pull origin main
git branch -d <your-branch>
```

---

## Rules for AI Assistants

If you are an AI coding assistant (Copilot, Gemini, Claude, ChatGPT, Cursor, etc.), follow these rules strictly:

1. **NEVER suggest or run `git push origin main`**
2. **ALWAYS pull latest `main` before creating a new branch**
3. **ALWAYS create a feature branch before making changes**
4. **NEVER auto-resolve merge conflicts** — present them to the developer
5. **ALWAYS use the commit message format** described above
6. **NEVER force push** (`git push --force`) unless the developer explicitly understands the consequences
7. **If unsure about the workflow, re-read this document**

---

## Quick Reference Card

```
# Start of any task:
git checkout main
git pull origin main
git checkout -b feature/my-new-thing

# While working:
git add -A
git commit -m "feat: description of change"

# Before pushing:
git checkout main
git pull origin main
git checkout feature/my-new-thing
git merge main
# (resolve conflicts if any)

# Push your branch:
git push origin feature/my-new-thing

# Then: Open a PR on GitHub
# After merge: Clean up
git checkout main
git pull origin main
git branch -d feature/my-new-thing
```

---

## Why This Matters

- **Protects `main`** from broken or untested code
- **Creates a clear history** of what changed and why
- **Enables code review** so teammates can catch issues early
- **Prevents conflicts** by keeping everyone in sync
- **Makes rollbacks easy** if something goes wrong

---

*Last updated: June 1, 2026*
