# Content Aggregator Frontend Plan

Status: proposed
Last updated: 2026-07-18

This folder defines the intended product experience and the technical path from the
current ingestion pipeline to a public, mobile-first content discovery site.

## Documents

- [Product and UX](./product-and-ux.md): audience, scope, information architecture,
  responsive layouts, discovery behavior, accessibility, SEO, and policy constraints.
- [Technical architecture](./technical-architecture.md): repository findings, stack
  decision, API and data model changes, frontend structure, delivery plan, and quality
  gates.

## Decision summary

1. Build the public site with Angular 22, TypeScript, standalone components, Signals,
   Angular SSR/hybrid rendering, SCSS design tokens, Angular CDK/Material primitives,
   Vitest, and Playwright.
2. Treat the site as a curated discovery and analysis layer over original YouTube
   videos. Embed the source upload; do not mirror or re-upload videos without rights.
3. Separate the public reader experience from a protected editorial/operations
   console. They may share one Angular workspace, but they must not share public API
   contracts or authorization assumptions.
4. Add a purpose-built public read API. The current `GET /api/YoutubeContents` endpoint
   is an operational list and only supports channel filtering.
5. Model topics, people, and timestamped sections as structured data. Do not implement
   the requested filters by parsing summary strings in the browser.
6. Start search with PostgreSQL full-text search plus `pg_trgm`. Add a dedicated search
   service only when measured scale or relevance requirements justify it.
7. Prefer section-based chapters as the primary outline. Minute-by-minute notes may be
   generated as an optional artifact, but they are too noisy for the default reading
   experience.
8. Require editorial approval for generated summaries, tags, people, and YouTube
   comment drafts in the first public release.
9. Protect or disable every existing operational endpoint before public deployment, and
   default-disable all external writes. A configured Facebook/YouTube token is not
   permission to publish.
10. Use locale-prefixed public routes (`/ka/...` and `/en/...`) with immutable entity IDs
    and descriptive slugs.

## Proposed release boundary

The first release is useful without accounts, personalization, comments, or a native
mobile application. It includes:

- a responsive discovery feed;
- full-text search and faceted filters;
- video detail pages with an embedded YouTube player, AI disclosure, summary, and
  clickable timestamp sections;
- channel, person, and topic pages;
- a minimal authenticated review workflow for content before it becomes public;
- crawlable metadata, sitemap entries, canonical URLs, and social previews.

Coding the frontend before the public read model and editorial state exist would create
throwaway UI and expose internal pipeline fields. The implementation should therefore
begin with the API/data milestones in the technical plan, then scaffold Angular against
the agreed OpenAPI contract.
