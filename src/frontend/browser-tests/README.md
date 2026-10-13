# Overview browser regression

`overview-navigation.playwright` is a callable Playwright tool snippet. Open a running
frontend in the browser, then run `playwright_browser_run_code_unsafe` with `filename`
set to this file's absolute path.

It creates and closes an isolated browser context, intercepts school API responses
with fixed fixtures, and stubs map requests. It exercises the actual SvelteKit router
and mounted components, without changing application data or the current browser's
cookies and local storage.

Assertions cover:

- Sidebar Overview retains selection, expanded branches, and local storage.
- Header search for the current school clears an incompatible tree filter, reveals
  and scrolls its row even when the URL is unchanged, and allows subsequent collapse.
- Back restores an explicit empty history snapshot instead of the latest saved
  workspace; Forward restores the later school selection.
