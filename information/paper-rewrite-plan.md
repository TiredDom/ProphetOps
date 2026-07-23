# ProphetOps — Capstone Paper Rewrite Plan (Chapters 1–3)

> Active working plan for the full rewrite of the ProphetOps capstone manuscript.
> Companion document: [dotnet-implementation-pivot.md](dotnet-implementation-pivot.md) (read that **after** the paper is done, to start the code migration).
> Last updated: 2026-07-08.

---

## 0. Non-negotiable decisions (locked)

| Area | Decision |
|---|---|
| Scope | Rewrite **Chapters 1–3 only**. No preliminaries (no title page/abstract/ack/ToC), no logo. A **References** list is required. |
| Degree | Program is **BSIT**. The PUP manual has no BSIT undergrad template, so the prof directed us to follow the **BS Computer Science** Chapter-3 structure (NOT the MSIT/master's one). Degree printed on the paper stays **Bachelor of Science in Information Technology**. |
| Citation style | **APA 7th edition** (prof override of the manual's older APA). |
| Formatting | **PUP manual mechanics**: 11-pt Arial, double-spaced, **1.5″ left margin / 1″ top-bottom-right**, 8.5×11. Figure number + title triple-spaced, caption flush-left; table captions single-spaced. Chapter headings ALL CAPS, centered, bold, triple-spaced from top. |
| Source recency | **Chapter 2 + key claims: aim 2023+** (oldest = 3 years). **Older/seminal cites are allowed** specifically for foundational works — theory origins (D&M 1992/2003), classic methods (Holt-Winters / exponential smoothing origin) — and generally for statements **outside** Chapter 2. Support seminal theory with 2023+ applications. |
| Citation count | **Chapter 2 ≥ 45 citations.** Chapters 1 and 3 add more on top. Target total ~55–65. Every DOI **verified** during writing — **no fabricated citations, ever.** Don't cap at the current list + planned additions; actively find further sources that strengthen the study. |
| Forecasting | **Holt-Winters** (additive triple exponential smoothing), described honestly. **Not** Meta/Facebook Prophet. |
| Insights | **Automated / rule-based** trajectory insights. **Not** "AI-Driven." |
| Emerging-tech framing | Satisfy the prof's Emerging Technologies expectation honestly with **Data Analytics + Predictive Analytics + Automation** — do NOT claim AI/ML. |
| System (as described) | **.NET (ASP.NET Core, C#) backend + Vue front-end** (Path B: REST API + Vue SPA). Private network (**LAN + secure VPN**), Windows-hosted, always-on box, runs as a Windows Service. |
| Theoretical framework | **DeLone & McLean IS Success Model** (primary) ± **TAM** (acceptance). Replaces Simon's Decision-Making Theory. |
| Deliverable | One **`.docx`** with figure/table placeholders + captions, ready for teammates to drop diagrams in. |
| Ch. 2 style | **Thematic**, written as a **storytelling arc** per theme: origins → evolution → current state → emerging/future. |

### Title
> **ProphetOps: A Decision Support System with Holt-Winters Forecasting and Automated Trajectory Insights for Renan-Tina Travels and Tours**
("ProphetOps" survives as the product brand; it no longer means Facebook *Prophet*.)

### OPEN — needs confirmation before/at Chapter 3
- **Respondent count — provisional 6** (1 owner + 3 staff + 2 IT experts, N=6). IT-expert count may change (0–2). **Team to confirm the final number with the prof** before Chapter 3 is finalized; Table 1 + weighted-mean N depend on it.
- Confirm **TAM** is included as a secondary lens or D&M alone.
- Confirm prof accepts seminal-theory citations (D&M 1992/2003) supported by 2023+ applications.

---

## 1. Theoretical framework (Figure 1)

**DeLone & McLean IS Success Model**, adapted to ProphetOps:

- **System Quality** ← ISO/IEC 25010 (functional suitability, performance efficiency, usability, reliability)
- **Information Quality** ← Holt-Winters forecast accuracy + the automated trajectory insights
- **Service Quality** ← support/validation, role-based access, security
- → **Use / Intention to Use** and **User Satisfaction** (5-pt Likert acceptability)
- → **Net Benefits** (centralized operations, reduced fragmentation, data-driven decisions)

*Figure 1* depicts this chain with ProphetOps components mapped onto each construct. Optionally overlay **TAM** (Perceived Usefulness + Perceived Ease of Use → adoption by non-technical staff).

---

## 2. Chapter 1 — The Problem and Its Setting

| Section | Content | Fig/Table · Sources |
|---|---|---|
| Background of the Study | MSME fragmentation; Renan-Tina's scattered tools (Messenger, Sheets, Gmail, paper); no forecasting; break-even mindset → sets up ProphetOps | 6–8 (MSME + PH tourism digitalization) |
| Theoretical Framework | DeLone & McLean IS Success Model mapped to ProphetOps | **Fig. 1** |
| Conceptual Framework | Input–Process–Output (IPO) | **Fig. 2 (IPO)** |
| Statement of the Problem | General + specific questions; reworded for Holt-Winters + honest analytics; keep ISO 25010 acceptability question | — |
| Research Objectives | General + specific objectives mirroring the SOP (prof-required section) | — |
| Scope and Delimitations | **Private network (LAN + secure VPN), not public internet**; internal users only; dummy-data dependency; no customer-facing/booking site; Windows/.NET deployment | — |
| Significance of the Study | Value delivered | — |
| Target Users and Beneficiaries | Distinct section (prof-required): Owner/Management, Staff, the Business, Future Researchers | — |
| Rationale for Developing the Proposed System | Direct "why build ProphetOps" | — |
| Definition of Terms | Holt-Winters, exponential smoothing, DSS, LAN, VPN, ISO/IEC 25010, weighted mean, Windows Service. **Remove "AI-Driven."** | — |

---

## 3. Chapter 2 — Review of Related Literature and Studies (full revamp, thematic + storytelling)

Each **Related Literature** theme is written as an evolution narrative (origins → how it evolved → where it stands → emerging/future), which also fixes the "hard to read" problem.

| Section | Theme (storytelling arc) | ~Citations |
|---|---|---|
| Related Literature 1 | **From Ledgers to Dashboards** — digitalization of MSME operations (paper → spreadsheets → web systems → integrated platforms) | ~7 |
| Related Literature 2 | **The Rise of Decision Support Systems** — DSS origins → model/data/knowledge-driven types → SME adoption → future | ~7 |
| Related Literature 3 | **Forecasting Through Time** — moving averages → exponential smoothing/**Holt-Winters** → ARIMA/SARIMA → ML/LSTM/Prophet → why classical smoothing still fits small data | ~8 |
| Related Literature 4 | **Data as the Foundation** — fragmented records → centralized data → analytics → future | ~6 |
| Related Literature 5 | **Travel & Tourism in the Data Age** — how travel ops adopted demand forecasting → B2B applications → future | ~6 |
| Related Studies | Forecasting studies · DSS studies · Tourism/travel demand-forecasting studies | ~12 |
| Existing Systems | Manual processes · spreadsheet systems · traditional/off-the-shelf forecasting tools — and their gaps | ~4 |
| Why Holt-Winters | Compare alternatives; justify HW (captures trend + seasonality, lightweight, explainable, small-data-friendly, no heavy infra) | ~5 |
| Research Gap | No localized, private, integrated DSS + forecasting for a B2B travel MSME | — |
| Synthesis | Ties themes together → justifies ProphetOps | — |

**Total ≥ 45 citations** in Chapter 2.

---

## 4. Chapter 3 — Methodology (BS-CS backbone + prof's A–D)

**A. Research Methodology**
- Research Design (Developmental + Descriptive-Evaluative)
- Sources of Data (operational time-series + ISO 25010 evaluation data)
- Respondents & Participants (**provisional: 6 = 1 owner + 3 staff + 2 IT experts, N=6; purposive; confirm with prof**) → **Table 1: Data Sources**
- Research Instrument (ISO/IEC 25010, 5-pt Likert) → **Table 2: Likert interpretation** — use the **0.8-interval set** (4.20–5.00, 3.40–4.19, 2.60–3.39, 1.80–2.59, 1.00–1.79); delete the conflicting table
- Validation Process (content validation by adviser + IT experts)
- Data Gathering (Phase 1 Authorization · Phase 2 Deployment · Phase 3 Evaluation)
- Statistical Treatment (Weighted Mean, formula fixed `WM = Σfx / N`) → **Table 3: Statistical limits**

**B. System Development Methodology (Agile Scrum)**
- Why Agile · Product Backlog · Sprint 1 (UI/DB/architecture) · Sprint 2 (forecasting module) · Sprint 3 (business logic/dashboard) · Sprint Review/Retrospective
- **Team roles** (5 members → PM / Scrum Master / QA Analyst / System Analyst / Lead Developer)

**C. Development Technologies** (honest, as-built + planned .NET — each with *why / how / behind-the-scenes / interactions / contribution*)
- Languages: **C#** (+ JS/TypeScript for Vue)
- Frameworks: **ASP.NET Core**, **Vue 3** (REST API layer)
- Database: **SQLite / SQL Server** via **EF Core**
- Forecasting: **in-house Holt-Winters** engine
- Tools: Vite, Git
- Hosting/Deployment: **Windows Service**, Kestrel/IIS, HTTPS (LAN), **VPN** for owner remote, always-on host (repurposed PC / mini-PC)

**D. System Diagrams** — all 8 of the prof's diagrams live here, in logical reading order (placeholder + caption + spec each):
- **Fig. 3** System Architecture (referenced in C, detailed here)
- **Fig. 4** Use Case Diagram
- **Fig. 5** Context Diagram (DFD level 0)
- **Fig. 6** Data Flow Diagram (DFD level 1)
- **Fig. 7** Entity Relationship Diagram (ERD)
- **Fig. 8** Database Design (physical schema; pairs with ERD)
- **Fig. 9** Activity Diagram
- **Fig. 10** Sequence Diagram
- *Optional* **Fig. B1** Agile Scrum sprint model → placed in **B. System Development Methodology** (not in the prof's required list, but strengthens B)

**No orphans:** the 2 framework figures live in Chapter 1; all 8 system diagrams live in Chapter 3 Section D (the paper has no appendices).

---

## 5. References
- Keep the solid existing DOIs, **reformatted to APA 7th**; drop the duplicate (Doğan 2026 ≡ Mahajan 2026); drop/deprioritize Prophet-only and pre-2023 items except seminal theory.
- Add ~15+ new **DOI-verified 2023+** sources (Holt-Winters/exponential smoothing, DSS architecture, ISO 25010, PH tourism-MSME, IS success model applications).
- **Heavy citation harvesting happens during writing** (web-search + DOI verification); Chapter 2 is where the ≥45 target is met.

## 6. Writing workflow
1. Confirm the OPEN items (respondent count, TAM, recency exception).
2. Draft **Chapter 1** → user review → iterate.
3. Draft **Chapter 2** (citation harvest + verify, thematic storytelling) → review.
4. Draft **Chapter 3** (+ diagram specs) → review.
5. Assemble References (APA 7th) → export **`.docx`**.
