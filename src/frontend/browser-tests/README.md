# Browser regressions

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

`map-theme.playwright` runs in an isolated browser context with fixed GeoServer
points and local tile fixtures. Run it with the same tool and a running frontend.
It checks light and dark tiles, palette tinting, unfiltered school-marker layers,
retained OpenStreetMap attribution, reduced motion, and theme changes without
rebuilding the map. It also checks wheel zoom while hovering with keyboard focus
outside the map, without a click, focus theft, or page scrolling.

`trend-interaction.playwright` uses fixed trend data to check the smooth line,
hover-only percentage labels, tooltip animation, keyboard focus and Escape,
touch selection on small screens, and reduced-motion preferences.

`overview-style.playwright` checks the Snow-inspired overview surfaces in light
and dark mode, palette and corner customization, school selection, full-school
links, collapsible panels, stable panel widths, and mobile overflow. It uses an
isolated browser context and fixed school, region, and map responses. Card-label
contrast is checked across all fifteen palettes in both display modes.
It also checks that Overview's shell, headers, and collapsed rails have no
decorative borders, while other pages retain their existing framing.
