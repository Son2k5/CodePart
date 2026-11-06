---
kind: frontend_style
name: Tailwind CSS v4 + Scoped Feature CSS Design System
category: frontend_style
scope:
    - '**'
source_files:
    - frontend/package.json
    - frontend/vite.config.ts
    - frontend/src/index.css
    - frontend/src/features/workspace/tokens.css
    - frontend/src/features/workspace/workspace.css
    - frontend/src/features/ai-assistant/assistant.css
---

## Approach

The CodePart frontend (`frontend/`) is a React 19 + Vite application that uses **Tailwind CSS v4** (via `@tailwindcss/vite` plugin) as its primary styling system. Styling is applied inline via `className` strings using Tailwind utility classes throughout most feature modules. A secondary, legacy-style scoped CSS system coexists for the Workspace feature and some older landing-page components.

## Key Files and Packages

- `frontend/package.json` — declares dependencies: `tailwindcss ^4.3.3`, `@tailwindcss/vite ^4.3.3`, `lucide-react` (icons), `sonner` (toasts), `@radix-ui/react-dialog` + `@radix-ui/react-tabs` (headless primitives), `react-resizable-panels`, `@monaco-editor/react`, `@uiw/react-codemirror` (code editors).
- `frontend/src/index.css` — single entry point; imports Tailwind via `@import "tailwindcss"`, defines a global `@theme` block setting `--font-sans: 'Inter', sans-serif`, and a `@layer base` reset plus a `@layer utilities` block for a few custom utility classes.
- `frontend/vite.config.ts` — registers `tailwindcss()` plugin alongside `@vitejs/plugin-react`; configures Rollup `manualChunks` to split vendor bundles (react, react-router, react-query, monaco-editor, codemirror, lucide-react).
- `frontend/src/features/workspace/tokens.css` — scoped design-token file defining a `.workspace` CSS-custom-property palette (`--ws-bg`, `--ws-primary`, `--ws-mint`, `--ws-coral`, `--ws-butter`, `--ws-sky`, …) and importing fonts from `@fontsource/nunito`, `@fontsource/be-vietnam-pro`, `@fontsource/jetbrains-mono`.
- `frontend/src/features/workspace/workspace.css` — large single-file stylesheet scoped under `.workspace` / `.ws-*` class names covering layout, buttons, tabs, panels, markdown rendering, verdict badges, modals, focus mode, and responsive breakpoints (1600px, 1150px, 1023px, 480px).
- Per-feature CSS files: `ai-assistant/assistant.css` (empty, comment says "Converted to Tailwind utility classes"), `code-judge/judge.css`, `home/member-home.css`, `landing/styles/*.css`, `auth/styles/*.css`, `teacher/styles/*.css`, `admin/styles/*.css`.

## Architecture and Conventions

### Tailwind-first utility styling
Most feature directories under `src/features/*` style their components directly with Tailwind utility classes in JSX `className` props (e.g. `flex flex-col bg-white border border-slate-200 rounded-xl p-6 sm:p-7 hover:shadow-lg`). The landing page components under `src/components/` follow the same pattern, using Tailwind's `slate-*` color scale, `sm:`/`md:`/`lg:` breakpoints, and `hover:` variants.

### Global theme tokens
Global typography and base styles live in `index.css`:
- `@theme { --font-sans: 'Inter', sans-serif; }` sets the default font family.
- `@layer base` resets margins, forces inherited font on form controls, removes tap highlight, sets cursor pointer on buttons, applies a visible-focus ring (`#397c8c`), enables smooth scrolling, and adds scroll-margin for anchor targets.
- `@layer utilities` exposes `.font-poppins` and `.font-nunito` aliases.

### Scoped feature token system (Workspace)
The Workspace feature opts out of the global Tailwind theme and instead defines its own isolated design system inside `.workspace`:
- A green/sky/butter/coral/mint accent palette via CSS custom properties.
- Scoped class prefixes (`ws-*`) for every component class.
- Font overrides per element type: Nunito for headings/buttons, JetBrains Mono for code, Be Vietnam Pro for body text.
- A dedicated `DesignSystemPage.tsx` renders a token grid for visual inspection.

### Headless UI primitives
Interactive primitives come from Radix UI (`@radix-ui/react-dialog`, `@radix-ui/react-tabs`); components style them via Tailwind classes rather than relying on Radix defaults.

### Icons and assets
Icons are rendered via `lucide-react` SVG components passed as children or props. Static images live under `public/images/` (language logos, hero photos). Fonts are loaded through `@fontsource` packages imported in `tokens.css`.

### Responsive strategy
Responsive behavior is primarily achieved through Tailwind's breakpoint utilities (`sm:`, `md:`, `lg:`) in JSX. The Workspace feature additionally uses traditional `@media` queries in `workspace.css` for complex mobile panel switching, safe-area insets (`env(safe-area-inset-bottom)`), and reduced-motion support (`prefers-reduced-motion:reduce`).

### Build-time chunking for heavy UI deps
`vite.config.ts` splits large UI dependencies into separate chunks (`vendor-react`, `vendor-router`, `vendor-query`, `vendor-monaco`, `vendor-codemirror`, `vendor-icons`) to improve caching and initial load performance.

## Conventions and Constraints

- **Styling location**: Component styles are written as Tailwind utility classes directly in JSX `className` props; standalone `.css` files are reserved for feature-scoped token palettes and large layout sheets (notably `features/workspace/`).
- **Global vs. scoped**: Global base/theme lives only in `src/index.css`. Feature-specific tokens are defined in a `.css` file under the feature directory and imported by that feature's entry stylesheet.
- **Font loading**: Fonts are loaded via `@fontsource` package imports in CSS files (see `workspace/tokens.css`), not via `<link>` tags in HTML.
- **Iconography**: Icons are provided by `lucide-react` components, not by image assets or icon fonts.
- **Headless primitives**: Dialogs and tabs use `@radix-ui/*` components styled with Tailwind; no custom dialog/tab implementations exist.
- **Accessibility baseline**: Focus-visible outlines, `prefers-reduced-motion`, and semantic HTML elements are established in `index.css` and `workspace.css`.
- **No Tailwind config file**: Tailwind v4 is configured entirely through the `@theme` block in `index.css` and the Vite plugin registration in `vite.config.ts`; there is no `tailwind.config.js`/`tailwind.config.ts`.
- **Landing vs. app styling divergence**: Landing pages and marketing components use Tailwind's default `slate-*` palette and Inter font, while the Workspace feature uses its own green-toned palette and Nunito/Be Vietnam Pro/JetBrains Mono fonts — the two styles intentionally do not share a common token layer.