---
applyTo: "**"
---
# Testing conventions

- When asked to **test** a UI/web change, run the browser test in **headed (visible) mode**, not headless, so the person can watch it.
  - Playwright example: `chromium.launch({ channel: 'msedge', headless: false, slowMo: 400 })`.
  - Keep the browser window open for a few seconds at the end of the run.
- Prefer driving a real browser (Playwright/Edge) over asserting only on fetched HTML for anything that depends on client-side rendering (e.g. Blazor WebAssembly).
