# CodePart landing page

The route entry is `src/pages/LandingPage.tsx`. It composes sections and owns navigation to authentication; landing presentation stays in this feature.

```text
landing/
  components/       Section components and the shared SectionHeading
  data/content.ts   Navigation, hero slides, feature copy, learning paths, FAQ
  hooks/            Scroll reveal and active-section tracking
  styles/           Landing-only styles, loaded by the landing route
  types.ts          Types for navigation and learning paths
```

## Editing

- Change copy and learning-path filters in `data/content.ts`.
- Add a section as its own component and compose it in `LandingPage.tsx`.
- Keep section IDs and navigation anchors in sync with `navigation`.
- Add `data-reveal` to a static section element for a one-time entrance animation. Dynamic learning-path cards use CSS entrance animations instead.
- Shared colors use `--landing-*` variables in `styles/landing.css`. Global Inter configuration remains in `src/index.css`.
- Authentication buttons use route callbacks. Learning paths are an introduction to subjects, not live enrollment records. The page does not submit contact information or fabricate successful registrations.

## Motion and accessibility

The hero rotates every 4.5 seconds, with pause controls and keyboard-focus pausing. Scroll reveals use a single IntersectionObserver, with visible content as the default. Reduced-motion settings disable autoplay and animation. FAQ uses native keyboard-accessible `details` elements; filters expose their selection with `aria-pressed`.

## Verification

From `frontend`, run `npm run build`. Check the landing page at mobile and desktop widths, all learning filters, FAQ expansion, anchor navigation, registration/login links, and reduced-motion behavior after changes.
