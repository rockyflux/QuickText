# QuickText marketing site + GitHub Pages

> **For agentic workers:** Implement task-by-task. Spec: jot-like static site under `site/`, content for QuickText, zh-first with EN switch placeholder, deploy via Actions Pages.

**Goal:** Ship `https://rockyflux.github.io/QuickText/` from `site/` on push to main.

**Files:**
- `site/index.html` — zh landing (jot section rhythm, QuickText copy + demo)
- `site/styles.css` — jot-like layout/theme (CDN Geist, no local font blobs)
- `site/app.js` — nav, stage scale, hero demo, FAQ, lang stub
- `site/favicon.svg`, `site/robots.txt`, `site/sitemap.xml`
- `site/assets/` — logo + og from `assets/branding/`
- `.github/workflows/pages.yml` — upload-pages-artifact + deploy-pages

**Out of scope:** English copy page, custom domain, product code, ci.yml merge.

## Task 1: Static site (zh)

- [ ] Create `site/` with HTML/CSS/JS + assets + robots/sitemap
- [ ] Hero demo: hotkey → search palette → paste at cursor
- [ ] Lang toggle: EN = “即将推出” (no en page yet)

## Task 2: Pages workflow

- [ ] Add `.github/workflows/pages.yml` (paths: `site/**`, workflow file)
- [ ] Optional: README zh/en one-line site link after structure exists

## Task 3: Verify

- [ ] Open `site/index.html` locally / lint HTML structure
- [ ] `git diff --check`
