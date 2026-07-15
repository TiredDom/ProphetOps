# Product

## Register

product

## Users

ProphetOps is used by owners, admins, and travel operations staff at Renan-Tina Travels and Tours, a B2B travel agency. They use it in an internal operations context to review bookings, package capacity, expenses, sales analytics, reports, and the demand forecast without needing technical algorithm knowledge.

## Product Purpose

ProphetOps centralizes fragmented business records from tools such as Messenger, Google Sheets, Gmail, notebooks, and manual logs into an internal Decision Support System. It should help internal users quickly understand what is happening in the business, why it matters, what may happen next, and what action should be reviewed.

ProphetOps is a .NET (ASP.NET Core, C#) and Vue 3 application with saved records, cookie-based login, role-aware navigation, and polished DSS screens. The active research value is a Holt-Winters demand-forecasting layer (triple exponential smoothing) that projects monthly revenue and seasonal trends from a representative sample of monthly records, implemented as an explainable algorithm rather than a pre-built model. Owner-facing UI should describe the projected demand and what to review first, then identify Holt-Winters as the method in supporting labels or explanation panels. Real document exports and AI text generation are not yet implemented and should not be implied.

## Brand Personality

Premium, trustworthy, clear.

The interface should feel calm, serious, polished, and business-focused. It should borrow Stripe Dashboard's financial clarity and trustworthy reporting feel, plus Attio CRM's modern record management and clean object-detail layouts. The voice should use plain business language for non-technical internal users.

## Anti-references

ProphetOps should not look or behave like a public booking website, customer portal, travel marketplace, payment system, marketing landing page, or external API platform. It should avoid generic admin-homepage patterns, spreadsheet dumps, walls of equal-weight cards, decorative clutter, loud gradients, rough visible mockup disclaimers, analyst-heavy Prophet field names or formulas, black-box AI assistant claims, and any UI language implying real exports, real forecasting, or real AI exists before those phases are explicitly started.

## Design Principles

1. Make the DSS answer visible within five seconds: what happened, why it matters, and what needs review.
2. Connect every insight to a decision using observed data, business meaning, and suggested action.
3. Use progressive disclosure: summaries first, then details through tabs, drawers, focused modals, or dedicated pages.
4. Preserve internal credibility with realistic travel-operations data, stable role-aware navigation, and business-first wording.
5. Stay future-ready without overclaiming: align UI shape with the Holt-Winters demand-forecasting plan while documenting prototype limitations outside the main app screen.

## Accessibility & Inclusion

Target WCAG AA readability for text and controls. Keep focus states visible, navigation keyboard-accessible, modals and drawers dismissible, and status meaning available through labels instead of color alone. Respect reduced-motion preferences, keep the interface responsive across desktop, tablet, and mobile widths, and use concise non-technical copy so owners and operations staff can understand screens quickly.
