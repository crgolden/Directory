# Design System

This document describes the visual design system for the Experience Angular 21 frontend. Implementation lives in `experience.client/src/styles.scss`.

---

## Technology

| Tool | Version | Role |
|------|---------|------|
| Bootstrap | `^5.3.8` | CSS framework (installed via npm, imported via SCSS — no CDN) |
| Bootstrap Icons | `^1.11.3` | Icon library (npm, imported via SCSS) |
| Sass | `^1.77.0` | SCSS compiler |
| Inter | Google Fonts (400/500/600) | Body and heading typeface (`<link>` in `index.html`) |

### `@import` vs `@use`

Bootstrap's Sass variable overrides (`$primary`, `$body-bg`, etc.) **require `@import`**, not `@use`. Bootstrap 5.x declares all its `!default` variables inside partials (`_variables.scss`), not at the `bootstrap.scss` entry point. Dart Sass's `@use` module system creates a scope boundary — variables declared in the caller file before `@use` are in the caller's namespace and are never seen by Bootstrap's module. The `with (...)` clause on `@use` only passes variables declared at the entry-point level, so it also cannot reach `_variables.scss` declarations.

`@import` shares a flat global scope, which is exactly what Bootstrap's `!default` variable pattern expects. Two Dart Sass deprecation warnings appear in `npm run build` output as a result; they are non-fatal and unavoidable with Bootstrap 5's current SCSS architecture. They will be resolved when Bootstrap migrates to the `@use` module system (planned for Bootstrap 6).

### Silent-login iframe

`AppComponent` renders a hidden `<iframe id="bff-silent-login">` when the user is not authenticated. This iframe performs an OIDC `prompt=none` silent auth check against `/bff/login`. Without CSS it renders at the browser default 300×150px, creating visible whitespace. `app.component.css` positions it off-screen while it loads:

```css
#bff-silent-login {
  position: fixed;
  top: -9999px;
  left: -9999px;
  width: 0;
  height: 0;
  border: none;
}
```

The iframe is removed from the DOM once it posts `{ source: 'bff-silent-login', isLoggedIn: … }` back to the parent window.

---

## Colour Palette

All tokens are Bootstrap Sass variable overrides declared in `styles.scss` **before** `@use 'bootstrap/scss/bootstrap'`.

| Variable | Value | Usage |
|---|---|---|
| `$primary` | `#334155` (Slate 700) | Buttons, links, focus rings |
| `$secondary` | `#64748b` (Slate 500) | Secondary buttons, muted text |
| `$body-bg` | `#f8fafc` (Slate 50) | Page background |
| `$body-color` | `#0f172a` (Slate 900) | Default text |
| `$border-color` | `#e2e8f0` (Slate 200) | Borders, dividers |

All Bootstrap semantic colours (danger `#dc3545`, warning `#ffc107`, success `#198754`) are **unchanged**.

### Navbar

The navbar uses a separate, darker token not mapped to `$primary` — this keeps the navbar independent so buttons and the navbar don't have to be the same shade.

| Element | Value |
|---|---|
| Navbar background | `#1e293b` (Slate 800) |
| Nav-link colour (default) | `rgba(255, 255, 255, 0.9)` |
| Nav-link colour (hover/focus) | `#ffffff` |
| Active route underline | `2px solid rgba(255, 255, 255, 0.7)` |

The navbar uses Bootstrap classes `navbar-dark navbar-app`. `navbar-dark` sets the white hamburger toggler icon on mobile; `.navbar-app` overrides link colours (declared after Bootstrap so specificity wins).

### Manual Chat Panel colours

| Element | Token |
|---|---|
| User message bubble | `#334155` (`$primary`) |
| User bubble text | `#ffffff` |
| Assistant bubble border | `#e2e8f0` (`$border-color`) |
| Message list background | `#f8fafc` (`$body-bg`) |
| Panel header background | `#f8fafc` (`$body-bg`) |
| Panel left border | `#e2e8f0` (`$border-color`) |
| Markdown links | `#334155` (`$primary`) |
| Code / pre background | `#f1f5f9` (Slate 100) |

---

## Typography

```scss
$font-family-sans-serif: 'Inter', system-ui, -apple-system, sans-serif;
$headings-font-weight:   600;
$font-size-base:         0.9375rem; // 15px at browser default 16px rem base
```

Inter is loaded via Google Fonts in `index.html` (weights 400, 500, 600). No `@font-face` declaration is needed in SCSS.

---

## Spacing & Shape

```scss
$border-radius:    0.5rem;    // cards, inputs, buttons
$border-radius-lg: 0.625rem;  // modals, larger containers
$border-radius-sm: 0.375rem;  // badges, small chips
```

---

## Shadows

```scss
$box-shadow-sm: 0 1px 3px rgba(15, 23, 42, 0.08), 0 1px 2px rgba(15, 23, 42, 0.06);
$box-shadow:    0 4px 6px -1px rgba(15, 23, 42, 0.10), 0 2px 4px -2px rgba(15, 23, 42, 0.10);
```

The `shadow-sm` utility is used on cards (`shadow-sm`). The navbar uses a local `.box-shadow` class in `nav-menu.component.css` (a legacy pre-Bootstrap class retained for compatibility).

---

## Icon System

Bootstrap Icons 1.11+ via npm. All icons are webfont glyphs (`<i class="bi bi-{name}">`) — no SVG sprites, no inline SVG.

### Home page benefit icon mapping

| Benefit | Class |
|---|---|
| Insurance Claims | `bi-shield-check` |
| Maintenance Scheduling | `bi-tools` |
| Estate & Will Preparation | `bi-file-earmark-text` |
| User Manual Access | `bi-book` |
| Warranty Tracking | `bi-calendar2-check` |
| Resale Value | `bi-currency-dollar` |

Icons render inside a circular **icon well** defined in `home.component.css`:

```css
.icon-well {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 3rem;
  height: 3rem;
  border-radius: 50%;
  background: rgba(51, 65, 85, 0.08);  /* $primary at 8% opacity */
  color: #334155;
}
```

---

## Empty State Pattern

Used in `product-list`, `catalog-list` when the list is empty or a search returns no results. Pattern: large centered icon + heading + optional action link.

```html
<div class="empty-state text-center py-5">
  <i class="bi bi-{icon} fs-1 text-secondary mb-3 d-block"></i>
  <h5>Heading text</h5>
  <p class="text-muted small">Supporting text or action link.</p>
</div>
```

| Page | No-data icon | No-match icon |
|---|---|---|
| My Products | `bi-box-seam` | `bi-search` |
| Catalog | `bi-grid` | `bi-search` |

---

## Not-Found Page Pattern

Used in `product-not-found` and `catalog-not-found`. Large icon above the heading:

```html
<i class="bi bi-exclamation-circle text-secondary mb-3 d-block" style="font-size: 3.5rem;"></i>
```

---

## Hero Section

`home.component.css` defines a neutral slate gradient:

```css
.hero-section {
  background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
  border-bottom: 1px solid #e2e8f0;
}
```

---

## Accessibility

- `$primary` (`#334155`) on white: contrast ratio **9.5:1** (WCAG AAA)
- White on `#1e293b` navbar: contrast ratio **15.3:1** (WCAG AAA)
- All Bootstrap semantic colours (danger, warning, success, info) are unchanged and meet WCAG AA

---

## Manual Chat Panel Layout (preserved, not changed)

The manual chat panel uses a fixed-position flex architecture. These rules are intentionally frozen — do not change them without verifying the chat scroll behaviour across all breakpoints.

| Rule | File | Purpose |
|---|---|---|
| `:host { display: flex; flex: 1 1 auto; min-height: 0 }` | `manual-chat.component.css` | Scroll containment |
| `.manual-chat { min-height: 0 }` | `manual-chat.component.css` | Same |
| `.panel-body { min-height: 0; overflow: hidden }` | `manual-chat-panel.component.css` | Same |
| `.manual-chat-toggle` (all positioning) | `manual-chat-panel.component.css` | Fixed tab anchor |
| `.manual-chat-panel` (all positioning/sizing) | `manual-chat-panel.component.css` | Fixed side drawer |
| `.panel-narrow` (mobile full-screen) | `manual-chat-panel.component.css` | Mobile overlay (<768px) |
| `z-index: 1040 / 1050` | `manual-chat-panel.component.css` | Stacking order |
| `writing-mode: vertical-rl` | `manual-chat-panel.component.css` | Vertical toggle label |
| `@keyframes blink` + `.typing-cursor` | `manual-chat.component.css` | Typing animation |

The panel switches to full-screen overlay at `< 768px` via `window.matchMedia('(max-width: 767px)')` in `ManualChatPanelComponent.ngOnInit()`. The `.panel-narrow` class is applied via `[class.panel-narrow]="isNarrow()"` in the template.
