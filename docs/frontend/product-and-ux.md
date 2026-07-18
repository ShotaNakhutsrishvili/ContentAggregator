# Product and UX

Status: proposed
Audience: product, design, frontend, API, and pipeline contributors

## 1. Product definition

The product is a searchable knowledge layer over selected YouTube videos. It helps a
reader find a relevant video, understand it quickly, jump to the exact section, and
verify the result against the original source.

"Repost" means curating and embedding the original YouTube upload with attribution. It
does not mean downloading and publishing a duplicate video. Re-uploading third-party
work creates copyright and platform-policy risk and removes traffic and attribution
from the original creator.

The likely initial corpus is multilingual and public-affairs/interview oriented, based
on the existing Georgian and English person names and subtitle-language model. Georgian
and English UI locales are part of the first-release route and metadata contract.

### Product principles

- **Source first:** channel, publish date, original link, and YouTube attribution are
  always visible.
- **Find the moment:** every section heading is a seek action, not decorative text.
- **AI is assistive:** generated summaries and tags are labeled, reviewable, versioned,
  and never presented as the creator's own words.
- **Discovery over consumption:** filtering, topic relationships, and people are the
  independent value beyond YouTube's normal browse flow.
- **Mobile is the baseline:** the smallest supported layout is designed first; desktop
  adds density rather than changing the workflow.

## 2. Users and core jobs

### Reader

- Find recent videos about a subject or person.
- Narrow results by topic, person, channel, language, date, duration, or format.
- Read a short neutral summary before committing to a long video.
- Jump to a relevant section and continue watching on YouTube if preferred.
- Share a stable video or taxonomy URL.

### Editor/operator

- See which videos are discovered, processed, failed, ready for review, or published.
- Correct summaries, section titles, timestamps, people, topics, and language.
- Resolve duplicate people/topics and add localized names or aliases.
- Approve publication to the site and separately approve external posts/comments.
- Retry failed pipeline stages without editing the database.

## 3. Scope

### Release 1

- Public discovery feed and global search.
- URL-backed faceted filters and sorting.
- Video detail, channel, person, and topic pages.
- YouTube embed with clickable structured sections.
- Concise summary, participant roles when known, topic tags, language, and duration.
- Empty, loading, partial-data, unavailable-video, and error states.
- Editorial review and publish/unpublish controls.
- Georgian/English-capable content fields and locale-safe typography.
- SSR, metadata, sitemap, `VideoObject` markup where valid, and social sharing cards.
- Privacy, methodology, corrections/contact, terms, and YouTube attribution links.

### Later, after usage evidence

- Saved filters, bookmarks, watch history, alerts, and user accounts.
- Semantic/vector search and natural-language questions over transcripts.
- Full transcript search and highlighted matching passages.
- Collections/playlists curated by editors.
- Personalized recommendations.
- Native applications or PWA offline support.
- Reader comments, ratings, or social features.

These later features should not drive the first schema except for stable IDs and clear
ownership boundaries.

## 4. Information architecture

### Public routes

| Route | Purpose | Render mode |
| --- | --- | --- |
| `/:locale` | Latest/relevant feed with quick discovery controls | SSR |
| `/:locale/search` | Search results and all facets | SSR for shared URLs, CSR transitions |
| `/:locale/videos/:videoId/:slug` | Player, summary, sections, people, topics, related videos | SSR |
| `/:locale/channels/:id/:slug` | Channel identity and filtered video feed | SSR |
| `/:locale/people/:id/:slug` | Localized identity, aliases, roles, and appearances | SSR |
| `/:locale/topics/:id/:slug` | Topic description, child topics, and filtered feed | SSR |
| `/:locale/explore` | Browse topics, people, and channels | Prerender/SSR |
| `/:locale/methodology` | How sources, AI summaries, review, and corrections work | Prerender |
| `/:locale/contact`, `/:locale/corrections` | Contact, correction, and takedown entry points | Prerender/SSR |
| `/:locale/privacy`, `/:locale/terms` | Required policies and YouTube/API disclosures | Prerender |
| `/:locale/not-found` | Public not-found experience | SSR with the correct status |

Supported UI locales are `ka` and `en`; every indexable route has a locale prefix. `/`
performs a temporary redirect based on explicit preference/`Accept-Language`, falling
back to `ka`. Content language remains an independent filter. Use immutable IDs as route
keys for all entities. Slugs are localized and descriptive; a slug-history record
permanently redirects renamed and merged entities without breaking shared URLs.

### Protected routes

| Route | Purpose |
| --- | --- |
| `/admin` | Queue counts, failures, recent pipeline activity |
| `/admin/videos` | Dense review queue with status and bulk filters |
| `/admin/videos/:id` | Source/summary/sections/taxonomy review and actions |
| `/admin/channels` | Channel discovery configuration |
| `/admin/taxonomy` | People/topics/aliases, merge and duplicate handling |
| `/admin/jobs` | Hangfire job health, retries, and error detail |
| `/sign-in` | Operator authentication |
| `/forbidden` | Authenticated but unauthorized state |

Admin pages are client-rendered behind authentication and are excluded from indexing.
They include explicit expired-session recovery. They must not be enabled until API
authorization exists.

## 5. Navigation

### Mobile

- Compact top bar: product mark/name, search action, locale menu.
- Persistent bottom navigation: Home, Search, Explore. Add Admin only for authorized
  operators; it should not occupy public navigation.
- Search opens a dedicated screen, preserving the previous query and filters.
- Filters open a full-height bottom sheet with Apply and Clear actions pinned at the
  bottom. Changing temporary values must not refetch behind the sheet.
- Bottom navigation, sheets, and pinned actions account for safe-area insets and the
  on-screen keyboard; content padding prevents controls from covering results.

### Tablet and desktop

- Top bar contains product identity, a central search field, Explore, and locale.
- Search results use a fixed-width left filter rail and a fluid result column.
- Video detail uses the player/content column plus an unframed section outline on wide
  screens. On smaller screens the outline follows the summary.
- Landscape mobile keeps the player usable without allowing fixed navigation to obscure
  player controls or section actions.
- Content width is constrained for readable summaries; the results grid may use two or
  three columns as space permits.

## 6. Page composition

### Home / Discover

This is the product, not a marketing landing page. The first viewport contains:

1. Search input.
2. A horizontally scrollable row of high-value topic/channel shortcuts.
3. Feed heading, result context, and sort control.
4. The first real video items, with enough of the next item visible to signal scrolling.

Optional editorial collections can appear as full-width bands between feed groups. Do
not add a hero, feature explanation, or decorative card section above the videos.

### Search results

- Search field with submitted query.
- Active-filter chips that each remove one value.
- Result count and sort: Relevance, Newest, Oldest, Shortest, Longest.
- Mobile Filter button shows the number of active facets.
- Result list/grid and a clear empty state that preserves easy filter removal.
- Pagination uses a Load more action or cursor-driven continuation; it must preserve
  scroll position on back navigation.

Filters:

| Facet | Control | Behavior |
| --- | --- | --- |
| Topic/subject | Searchable hierarchical multi-select | OR within facet, AND across facets |
| Person | Autocomplete multi-select | Match participants by default; optional "mentioned" later |
| Channel | Searchable multi-select | Show channel name and source identity |
| Language | Checkboxes | Transcript/summary language, explicitly labeled |
| Published date | Presets plus date range | Any time, week, month, year, custom |
| Duration | Presets or range | Under 10m, 10-30m, 30-60m, 60m+ |
| Format | Checkboxes | Interview, debate, speech, report, podcast, other |

All committed state is serialized into query parameters. Multi-select facets use repeated
keys such as `topic=12&topic=27`, never ambiguous comma-separated values. A filtered URL
must be shareable, reloadable, and compatible with browser back/forward navigation.

### Video result item

- Real 16:9 YouTube thumbnail with duration overlay.
- Title limited to three lines on mobile and two on wider grids.
- Channel, YouTube source mark, and relative/absolute publish date.
- Two-line summary excerpt when available.
- At most three high-signal tags, with an overflow count rather than a wrapping tag wall.
- Visible processing disclosure only when content is not yet fully available; internal
  errors and publishing flags never appear publicly.

Cards are for repeated video items only, with radius at or below 8px. Avoid nested cards.

### Video detail

Order on mobile:

1. Breadcrumb/back context.
2. 16:9 YouTube player, loaded on user intent when practical.
3. Title, channel/source, publish date, duration, language, original link.
4. AI disclosure and editorial-review state.
5. Summary.
6. Timestamped sections.
7. People and topics.
8. Related videos.

Section rows contain start time, concise heading, optional one- or two-sentence summary,
and a copy-link action. Activating a row seeks the embedded player without moving keyboard
focus into the iframe or causing autoplay surprises; a polite status message confirms the
new time. Chapter links use `?t=<seconds>#section-<id>`, while the canonical URL remains
the base video page. Current-section highlighting is enhancement, not a release blocker.

Unavailable or removed videos retain a useful source record and summary only when the
retention is policy-compliant; playback is replaced by a clear availability state.

### Entity pages

- Channel: name, source link, optional description, topics, and videos.
- Person: canonical localized name, aliases, brief editor-authored description if
  available, role filters, and appearances.
- Topic: editor-authored definition, parent/child topics, related people/channels, and
  videos.

Never generate unsupported biographies or factual descriptions from name matching alone.

### Admin video review

- Source metadata and embedded player.
- Side-by-side transcript timestamps and generated sections on desktop; sequential
  panels on mobile.
- Editable summary, sections, people with roles, topics, format, and language.
- Confidence/source indicators for machine suggestions.
- Immutable draft revision, change diff, public preview, optimistic-concurrency conflict
  recovery, and restore of a prior revision.
- Separate actions: Save draft, Approve for site, Publish external post, Publish YouTube
  comment. External writes show the exact final destination payload, require confirmation,
  and create an audit entry. The worker must not truncate or alter an approved payload.
- Retry controls identify the failed stage and keep prior approved content intact.
- Unpublish/takedown actions require a reason, preserve the audit trail, and enqueue any
  required metadata/transcript deletion work.

## 7. Responsive system

Design from 320px upward. Use content-driven breakpoints rather than device names:

- `0-599px`: one column, bottom navigation, bottom-sheet filters.
- `600-959px`: one or two result columns, expanded top navigation.
- `960-1279px`: filter rail plus two-column result grid; split detail where useful.
- `1280px+`: constrained shell, up to three result columns, stable detail outline.

Fixed-format media uses `aspect-ratio: 16 / 9`; toolbars and icon buttons have stable
dimensions. Text must wrap without changing control height unexpectedly. Font size does
not scale directly with viewport width.

## 8. Visual direction

The interface should feel editorial and research-oriented, not like a YouTube clone or
generic administration template.

- Neutral near-white/near-black surfaces, a restrained red source accent, and a distinct
  teal/green discovery accent. Avoid a one-hue palette.
- Use a Georgian-capable variable sans font with a system fallback. Verify every chosen
  weight contains Georgian glyphs before adoption.
- Dense metadata, clear hierarchy, strong focus states, and limited elevation.
- Use Lucide icons for common actions and Angular Material/CDK behavior for controls;
  theme them rather than accepting the stock Material visual language everywhere.
- Motion is short and functional, and honors `prefers-reduced-motion`.

## 9. Accessibility

Target WCAG 2.2 AA.

- Every control is keyboard reachable and has visible focus.
- Filter chips announce removal; result counts use an appropriate live region after
  explicit filter application.
- Bottom sheets/dialogs trap focus and restore it to their trigger.
- Thumbnails have useful alt text without repeating adjacent titles.
- Video embeds have titles, and section seek buttons expose timestamp plus heading.
- Color is never the only signal for AI/editorial/pipeline state.
- Tap targets are at least 44px where layout allows and never below platform guidance.
- Georgian and English language changes use correct `lang` attributes.
- Skeletons reserve layout space and do not create noisy screen-reader output.

## 10. SEO and sharing

- SSR public discovery and entity pages; never serve an empty app shell to crawlers.
- Unique title, description, canonical URL, Open Graph, and social image per video/entity.
- Emit valid `VideoObject` structured data on video pages and `Clip` data only when the
  required fields and accessible timestamps are present.
- Generate sitemap indexes for videos, channels, people, and topics; update them when
  publication state changes.
- Use semantic headings and crawlable links for related entities.
- Filter combinations are generally `noindex,follow`; canonicalize to the stable entity
  or base search route to avoid an unbounded URL index.
- Redirect stale title slugs while retaining immutable IDs.
- Emit localized `hreflang` alternates, vary SSR/CDN caches by route locale, and safely
  serialize untrusted titles and model text into JSON-LD.
- Serve a deliberate `robots.txt` and real SSR 404, 410, and 5xx responses. Remove
  unpublished/taken-down URLs from sitemaps, update `lastmod`, and retain policy-appropriate
  redirects or gone responses.

## 11. Trust, rights, and platform constraints

- Display YouTube as the source anywhere YouTube content or metadata appears.
- Include links to YouTube's Terms of Service, the Google Privacy Policy, and an accurate
  product privacy policy before public launch.
- Keep the embed controls and attribution intact, meet player-size requirements, and use
  a referrer policy compatible with current embedded-player identification requirements.
- Refresh or delete stored YouTube API metadata within the retention windows applicable
  to the data. Record `lastMetadataSyncedAt` and video availability.
- Do not scrape YouTube pages. The existing `yt-dlp` subtitle path cannot be assumed to be
  production-permitted. Accept only explicitly classified sources: owned content,
  creator-consented/licensed content, or a transcript obtained through an authorized
  acquisition path. Store transcript provenance, rights basis, consent/license reference,
  retention/revocation terms, and checksum. Unknown provenance blocks public publication.
- Ingest and refresh source availability and Made-for-Kids status where applicable. Do
  not collect or enable tracking/personalization that is incompatible with the video's
  audience designation. Define deletion/revocation propagation before launch.
- Do not imply that an AI summary is a transcript, quote, creator endorsement, or
  editorial fact check.
- Provide correction and takedown contact paths, with an audit trail for resulting
  changes.
- The current worker posts YouTube comments and Facebook posts automatically when tokens
  and row flags allow it. All destinations must be default-off. The initial product
  creates destination-specific previews and requires an authorized operator's explicit
  approval of an exact immutable revision. Each approved command has an idempotency key
  and audit record. Enable unattended publishing only after a documented platform-policy,
  rights, and account-consent review.

This document is product and engineering guidance, not legal advice.

## 12. Success measures

Instrument privacy-conscious aggregate events; do not collect data merely because it is
available.

- Search-to-video-detail click rate.
- Filter use and zero-result rate by facet combination.
- Section-seek use and click-through to original YouTube video.
- Median time from discovery to reviewed public item.
- Percentage of generated people/topics/sections edited before approval.
- Processing and publication failure rates.
- Core Web Vitals by mobile/desktop and route type.

Do not optimize for embedded watch time alone; that would pull the product toward being a
YouTube substitute rather than a high-value discovery layer.
