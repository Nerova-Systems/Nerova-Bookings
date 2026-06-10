# Nerova UX Design Review — "We have it from here"

**Scope:** App store → Extensions Hub, Channels (WhatsApp now, Calls later), Services (event types), Bookings, Clients, Availability, Welcome/setup.
**Grounding:** Current code in `main/WebApp/routes`, the design system in `shared-webapp/ui`, `DESIGN.md` (Linear token extraction), and `Nerova_v5.pptx` (vision).

---

## 1. The design thesis

The pitch deck says it plainly: *"We don't just sell software. We run your front desk."* The UI must say the same thing without words.

Linear's craft is the right quality bar, but Linear's audience is engineers — ours is a salon owner between clients with a phone in one hand. So we take Linear's **discipline** (one accent, surface ladder, restraint, speed, coherence) and pair it with **consumer-grade language**. The result is a third thing, not a Linear clone:

> **Every screen answers, in this order: (1) Is everything okay? (2) What did Nerova handle for me? (3) What needs me?**

That ordering *is* the "your data is safe and organized, we have it from here" story. A settings-first UI says "you operate this machine." A status-first UI says "the machine runs; here's the report." Meta Business Platform is the former — a sprawling console of objects (WABAs, templates, assets, webhooks). We win by being the latter.

### The four principles (apply to every page)

**P1 — Speak salon, not software.** No noun the owner wouldn't use with a client. The jargon kill-list (§3) is enforceable in review: if a string appears in the UI and isn't on the approved vocabulary, it doesn't ship.

**P2 — Status before settings.** Every page opens with a calm health statement ("Your WhatsApp front desk is live · 14 bookings this week"), then evidence, then — last — controls. Configuration is reached through "Fine-tune", never the landing state.

**P3 — Nerova is a character, not a feature.** The AI front desk speaks in first person plural, in outcomes: "We confirmed Thandi's 2pm and collected the R150 deposit." Never mechanisms: ~~"Webhook processed"~~, ~~"Flow refreshed"~~. Every automated act leaves a visible, human-readable trail — that trail is what makes "your data is safe and organized" believable rather than asserted.

**P4 — The UI grows only with what's ON.** Extensions gate surfaces. A fresh account shows ~6 nav items and short pages. Turn on Loyalty and loyalty appears in the client profile, the booking sheet, and WhatsApp flows — announced as one sentence, not a settings tour. Simplicity is the default state, not a mode.

### Craft standards (Linear-grade, non-negotiable)

Single accent (`--primary` lavender / tenant `--brand-primary`) plus semantic success/warning/destructive only — no emerald/teal/rose literals (currently violated in `channels/`, `apps/`, `insights/`). Inter only, with the DESIGN.md negative-tracking ladder implemented as tokens. Motion: 120–180ms ease-out on real state changes only — no `animate-pulse`, no `animate-bounce`, no gradient blobs. Skeletons for every async surface (ClientTableSkeleton is the model). Optimistic updates on toggles and quick actions. Empty states are onboarding moments with one verb, not apologies.

---

## 2. Information architecture

Current nav: Dashboard · Bookings · Event types · Availability · Clients · Insights · Apps (App store / Installed apps) · Channels (WhatsApp).

Proposed:

| Now | Becomes | Why |
|---|---|---|
| Dashboard | **Today** | "Dashboard" is software. "Today" is a front desk's unit of work: today's appointments, money collected, what Nerova handled overnight, what needs you. |
| Event types | **Services** | Calendly-ism. A salon sells services. (Deck already says "services".) |
| Availability | **Hours** | "When can clients book you?" is about hours, per team member. |
| Bookings | **Bookings** | Keep. |
| Clients | **Clients** | Keep. |
| Apps → App store + Installed | **Extensions** (one page) | §4.1. The store/installed split is marketplace thinking; we have ~6–10 first-party capabilities you switch on. |
| Channels | **Channels** | Keep — it scales to Calls, SMS, Email with one card pattern. |
| Insights | **Insights** | Keep; becomes more valuable as the AI's reporting surface. |
| Webhooks (top-level route) | Settings → "For developers" | Webhooks as a top-level concept is the single worst jargon leak in the current IA. |

Nav stays ≤ 8 items forever. Extensions never add top-level nav; they add sections inside existing pages (P4).

---

## 3. The jargon kill-list

Found in current user-facing strings. This becomes the vocabulary review gate:

| Current (in code today) | Replacement |
|---|---|
| "Webhook activity", "No webhooks received" (WhatsApp page) | Remove from owner UI. Internally: "Activity". Failures surface as "1 message needs attention", not "Failed". |
| "Refresh flows" / "Your WhatsApp booking and sign-in flows have been refreshed" | "Fix booking experience" under troubleshooting, or run it silently. "Flows" is Meta's word — never show it. |
| "Inbound / Outbound" (WhatsApp stats) | "From clients / From you" |
| "Event type", "Create a private setup event type…" | "Service" everywhere |
| "Embed" (event type action) | "Add to your website" |
| "Slug", "Booking ID" (bookings search) | Hide IDs; search by client name/phone. Reference numbers only on receipts. |
| "Round robin" (host picker) | "Share bookings across the team" |
| "Destination calendar", "No destination calendar selected" | "Where appointments are saved" — and pick a sane default so it never asks. |
| "Mandatory for attendee only / host only / both" (4-way enum) | "Ask clients why they cancel" — one toggle. The 4-way matrix is cal.com parity debt. |
| "Exactly one default schedule exists", "Schedule checks: Passed" (availability troubleshoot) | "We checked your hours — clients can book every service." Failures: "Fridays have no hours yet — add them?" |
| "Explore and connect powerful app extensions and calendar sync options to automate bookings" (App store subtitle) | "Turn on what your business needs." (the deck's own line — it's better) |
| "Webhook secret", "Event subscriptions" | Developer settings only, behind "For developers". |

Rule of thumb: if Fresha's receptionist-facing UI wouldn't say it, neither do we.

---

## 4. Page-by-page review

### 4.1 App Store → Extensions Hub

**Current state:** `routes/apps/` is a genuine marketplace — search bar, category filters, featured carousels ("Most popular", "Newly added"), screenshot carousels, permissions lists, install/uninstall dialogs, an "Installed apps" page with counts. It's well-built (good shells, skeletons) and strategically wrong. A marketplace says "here's an ecosystem to evaluate" — that's Shopify/Meta posture, and it transfers integration burden to the user. The deck says the opposite: **"Turn on what your business needs"** with big ON toggles and vertical-aware defaults.

**Direction:**
- One page, `Extensions`. No search, no categories, no carousels — a curated grid of ~6–10 first-party capability cards: Loyalty & Rewards, Packages & Memberships, Shopping, Reviews & Reputation, Gift Cards, Staff Management (per deck slide 8).
- Each card: icon, name, one outcome sentence ("Keep clients coming back automatically"), and a **switch** — not an "Install" button. Installing is operating software; switching on is delegating.
- Vertical-aware ordering: a salon tenant sees Loyalty/Reviews/Shopping first; a tutor sees Packages first. Recommended-for-you chips ("Most salons turn this on").
- Turning ON is one optimistic toggle + at most one sheet of choices with strong defaults ("Earn 1 point per R10 — change anytime"), then a single confirmation listing **where it now lives**: "Loyalty is on. You'll see it on client profiles, on each booking, and clients can check points on WhatsApp." That sentence is the whole P4 contract.
- Card flips to a live state when ON: "324 points awarded this month" — evidence, not configuration.
- Third-party calendar sync (Google/Outlook) is not an "extension" the owner shops for; it moves to Settings → Connected calendars, one-click.
- **Reuse:** AppsPageShell, card grid, and dialogs are salvageable; the filter/search/carousel layer is deletable complexity.

### 4.2 Channels — WhatsApp now, Calls later

**Current state:** `channels/index.tsx` has the right shape already — channel cards (WhatsApp Business / SMS / Email "Coming soon") with Connect/Manage states. But `channels/whatsapp.tsx` is a **debug console**: connection card, message stats (Inbound/Outbound/Delivered/Failed), a *Webhook activity panel*, raw conversation panels. Plus emerald gradients off the token system. This page is where "we have it from here" most needs to live, and currently it reads "you are the sysadmin of a messaging integration."

**Direction — the channel page template** (build once for WhatsApp, reuse for Calls):
1. **Hero status:** "Your WhatsApp front desk is live" + green dot + the number + this week in three human stats: bookings made, clients helped, needs-you count. (Keep the existing health check plumbing; rename everything.)
2. **What clients see:** a static phone-frame preview of the booking conversation. Owners trust what they can see; this also demos the product to every visitor of the page. (Replaces the raw conversation panel as the default view.)
3. **Handled by Nerova:** a feed in outcome language — "Confirmed Thandi · 2pm Gel set · deposit paid", "Rescheduled Bongi to Friday". This is the webhook activity panel, translated. Failures become a single amber "2 conversations need you" block at the top — never a red FAILED table.
4. **Conversations:** one level deeper (a "View conversations" link), kept for the owner who wants to read threads — not the landing state.
5. **Connect state** (not yet connected) is a 3-step embedded-signup card: "Connect the number clients already message." One button. The existing Meta embedded signup flow stays; the framing changes.
6. **Calls later:** same template — hero status ("Nerova answers your calls"), what-callers-hear preview, handled-feed, escalations. Designing WhatsApp this way means Calls is a content swap, not a new page.

### 4.3 Services (event types)

**Current state:** Full cal.com parity editor — Setup / Availability / Limits / Advanced / Recurring / Apps / Webhooks / Instant-meeting / AI-voice tabs; the Advanced tab alone is 1,422 lines of destination calendars, layout pickers, 4-way cancellation-reason enums. The list page has good bones (cards, duplicate/hide/preview actions, empty state) but speaks Calendly ("event type", "Embed", "Copy public link").

This is the platform's engine and the deck's "deterministic core" — keep the capability, hide the cockpit.

**Direction:**
- Rename throughout: **Services**. List page = service cards showing the four things a salon owner thinks in: name, duration, price, deposit. Plus per-card: who performs it (avatars) and ON/OFF for bookability.
- **Create/edit = one form, five fields** (name, duration, price, deposit, who). Everything else inherits defaults from business hours + vertical template. One quiet link: "Fine-tune" → opens the existing tab editor as a drawer for power users. We don't delete the cal.com parity work; we demote it. (The tabs already exist as components — this is a re-shelling, not a rebuild.)
- Webhooks tab disappears from the editor (→ developer settings). "AI voice agent" and "Instant meeting" tabs stay hidden until their extensions/channels are ON (P4).
- Buyer-facing wording on actions: "Copy booking link", "Add to your website", "Preview as a client".

### 4.4 Bookings

**Current state:** Table with status routes (`bookings/$status.tsx`), filter sheet (event type, attendee, date range, *Booking ID*), details side-sheet, update dialogs. Solid machinery; the framing is a database query screen ("Search by attendee email or booking ID").

**Direction:**
- Default view = **timeline, not table**: Today / Upcoming / Past grouped by day. A front desk thinks in days.
- Each row: time, client, service, staff, and **two stateful chips**: payment ("Deposit paid R150" / amber "Unpaid") and confirmation ("Confirmed on WhatsApp"). Money state visible at list level is core to the safe-and-organized story — it's the deck's "payment nightmare" slide, answered.
- The details sheet becomes **the story of the booking**: vertical timeline — "Booked via WhatsApp Tue 14:02 → Deposit R150 (Paystack) → Reminder sent → Confirmed". Every entry is the AI's paper trail (P3). Actions (reschedule, cancel, mark paid) live at the bottom; the history is the point.
- Search by name/phone only. Keyboard: `j/k` rows, `enter` opens sheet — the one Linear signature worth shipping early because staff use this page all day.

### 4.5 Clients

**Current state:** The most mature page — table with skeletons, empty states ("Clients will appear here once bookings are made"), bulk select/delete, profile side-pane, querying hooks. Closest to the bar already.

**Direction:**
- Profile pane becomes a **relationship record**, not a contact card: visits count, total spent, no-show count, next appointment, preferred services — auto-derived, never hand-entered. Tagline under the header: "Built automatically from bookings."
- Extension-aware blocks (P4): Loyalty ON → points balance + redeem; Packages ON → remaining sessions; Reviews ON → last rating.
- Migration trust moment: clients imported via the AI CSV migration carry a quiet "Imported from your old system · 3 visits of history" note. That single line converts "is my data safe?" into shown evidence.
- An "At-risk" smart filter (no visit in 6+ weeks) previews the AI-insights value inside an existing page before Insights matures.
- Soften bulk delete: archive as the primary verb, delete behind it. A business that's told "we have it from here" should never feel one checkbox from data loss.

### 4.6 Hours (availability)

**Current state:** Schedules list, weekly editor (`$scheduleId.tsx`), and — notably — a `troubleshoot.tsx` running invariant checks ("Exactly one default schedule exists", "Every schedule has weekly availability", Passed/Needs attention). The *instinct* is excellent: the system self-checks. The language is a CI pipeline.

**Direction:**
- Rename **Hours**. Lead with the week grid (the visual answer to "when can I be booked?"), schedules-as-list second. Most businesses need exactly one schedule + exceptions; multi-schedule is the fine-tune path.
- First-class concepts in plain words: "Days off", "Public holidays (SA)" preloaded, "Lunch breaks".
- Recast troubleshoot as the **guardian**: green line on the Hours page itself — "We checked your hours: every service is bookable." Failures arrive as Nerova speech: "Fridays have no hours yet, so Friday bookings are off. Add Friday hours?" with the fix inline. Same checks, inverted voice — from "you debug it" to "we watch it."

### 4.7 Welcome / setup — where the whole thesis lands

**Current state:** `welcome/$.tsx` is a bridge to the account SCS (generic signup/tenant flow). Nothing Nerova-specific yet — which is the opportunity, and per your note this is where template configs will live.

**Direction — the 4-step front-desk setup** (deck slide 12, productized):
1. **"What kind of business?"** — visual vertical picker (Salon / Barber / Nails / Trainer / Tutor / Other). This single answer drives the template: services with typical durations/prices/deposits, hours, extension defaults, WhatsApp tone.
2. **"Bring your data"** — drop a CSV/export from Fresha/Calendly/spreadsheets, or skip. AI maps it; show the mapping as a *receipt*, not a wizard: "Found 214 clients, 18 services, 2 years of history. All imported." (Safe-and-organized, proven in step 2.)
3. **"Connect WhatsApp"** — embedded signup, one button, "use the number clients already message."
4. **"Your front desk is ready"** — a review screen listing what was set up FOR them (services, hours, extensions ON, deposit policy) with inline edit affordances, then one CTA: **"Open Today"**. Land on the Today page already populated from the import — never an empty dashboard.

Each step is skippable; nothing blocks reaching the app. The template system means the answer to step 1 configures ~40 decisions the owner never sees — that's the "few steps, entire system" promise, and it's exactly what Meta Business Platform structurally cannot do.

---

## 5. How extensions reshape the UI (the mechanics of P4)

One tenant-level capability registry (extensions ON/OFF + vertical) drives declared **surface contributions**:

| Surface | Contributed by extensions |
|---|---|
| Client profile pane | Loyalty balance, package sessions, last review |
| Booking details sheet | Points awarded, package deduction, review request status |
| Today page | Extension tiles (gift card sales, reviews this week) |
| WhatsApp flows | "Check my points", package booking, review ask — listed on the channel page as "What clients can do on WhatsApp" with checkmarks per ON extension |
| Services editor | Product attach (Shopping ON), package eligibility (Packages ON) |

Implementation note: this is the existing feature-flag pattern (`featureFlags/` in shared-webapp, the `isWhatsAppSignupEnabled` gate) promoted into a first-class extension registry — per-tenant, server-driven, typed contribution points. Build the registry before building extension #2, or every extension becomes bespoke if-statements.

---

## 6. Competitive frame: why this beats Meta Business Platform

Meta's model: you manage *objects* (WABA, templates, flows, webhooks, assets) and assemble outcomes yourself. Nerova's model: you state your business type and the outcomes assemble themselves. Concretely, the same job on each platform:

| Job | Meta Business Platform | Nerova |
|---|---|---|
| "Clients book on WhatsApp" | Create WABA → verify business → design Flow → template approval → webhook endpoint → build backend | Welcome step 3: one button |
| "Remind clients" | Template messages + approval + scheduling logic you build | ON by default with every service |
| "Is it working?" | Webhook logs, delivery metrics | "Your front desk is live · 14 bookings this week" |

Every jargon word we remove and every status-first page we ship widens this gap. The danger to Meta isn't features — they have more — it's that we collapse their entire console into four welcome steps and a green dot.

---

## 7. Sequence (MVP lens)

1. **Foundations (days):** vocabulary sweep per §3 (string changes only, Lingui makes this cheap) · nav rename per §2 · token purge on channels/apps/insights · font/tracking fix from the prior review. Cheapest, app-wide perceived-quality jump.
2. **Channel page template** on WhatsApp (§4.2): hero status + handled-feed + needs-you. This is the demo-killer page for sales conversations.
3. **Extensions Hub reshape** (§4.1) + capability registry (§5): registry first, then convert the apps grid to toggle cards. Loyalty as the first end-to-end extension proving the P4 contract.
4. **Services simplification** (§4.3): re-shell to 5-field form + Fine-tune drawer (existing tabs preserved underneath).
5. **Bookings timeline + story sheet** (§4.4), then Clients relationship blocks (§4.5), then Hours guardian reframe (§4.6).
6. **Welcome templates** (§4.7) last — it configures the systems above, so it lands once they exist. The CSV-migration receipt is the highest-value piece if displacement (Fresha/Calendly users) is the go-to-market wedge.

---

*Companion docs: `DESIGN.md` (token system) · `docs/agentic-system-spec.md` (AI front desk behavior). The voice rules in §1/P3 should govern the agent's WhatsApp copy too — one character, both sides of the glass.*
