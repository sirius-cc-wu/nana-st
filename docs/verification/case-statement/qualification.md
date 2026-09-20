---
type: Qualification Report
slice: "docs/features/case-statement/requirements.md"
governing_adr: "docs/decisions/case-statement.md"
verdict: "VERIFIED"
date: "2026-09-20"
---

# Qualification Report: Discrete Ordinal CASE Statement Design

## 1. Executive Summary
- **Governing Spec**: [`docs/features/case-statement/requirements.md`](../../features/case-statement/requirements.md)
- **Governing ADR**: [`docs/decisions/case-statement.md`](../../decisions/case-statement.md)
- **Lifecycle Design**: [`docs/features/case-statement/rust-lifecycle.md`](../../features/case-statement/rust-lifecycle.md)
- **Final Verdict**: **VERIFIED**

## 2. Behavioral Compliance Matrix (Example Mapping)
| Rule # | Rule Description | Specification Coverage | Audit Assessment |
| :--- | :--- | :--- | :--- |
| **R1** | Exact branch match executes body and exits construct. | Requirements Sec. Business Rules & Architecture Sec. 2 | VERIFIED: Lowering guarantees immediate exit after matched branch. |
| **R2** | Multi-value branch matching (`val1, val2:`). | Requirements Sec. Business Rules & Architecture Sec. 2 | VERIFIED: Lowering using `DUP ... = OVER ... = OR` specified. |
| **R3** | Fallback `ELSE` executes when no branch matches. | Requirements Sec. Business Rules & Architecture Sec. 2 | VERIFIED: Fallback execution path specified with stack balance. |
| **R4** | Omitted `ELSE` discards selector and leaves state untouched. | Requirements Sec. Business Rules & Architecture Sec. 2 | VERIFIED: Unconditional fallback `DROP` specified. |
| **R5** | Ordinal selector restriction (`INT`, `DINT`); `REAL`/`BOOL` rejected. | Requirements Sec. Business Rules R5 & Acceptance Criteria 2 | VERIFIED: Compile-time type check specified. |
| **R6** | Rejection of duplicate case match values. | Requirements Sec. Business Rules R6 & Acceptance Criteria 2 | VERIFIED: Compile-time uniqueness check specified. |
| **R7** | Cumulative nesting limit enforcement ($\le 64$ frames). | Requirements Sec. Business Rules R7 & Architecture Sec. 4 | VERIFIED: Tracks cumulative depth across enclosing blocks, case arms, and nested bodies. |
| **R8** | Single evaluation of selector expression. | Requirements Sec. Business Rules R8 & Architecture Sec. 3 | VERIFIED: Evaluated once upon entry onto data stack. |

## 3. Test Integrity & Specification Audit
- [x] All 8 Example Mapping rules have concrete positive and negative behavioral specifications.
- [x] Concrete negative examples (non-ordinal selector, duplicate match values) included.
- [x] Realistic BNC state machine sequence example (`AquaStepControl`) provided.

## 4. Architectural Invariant Audit (ADR Compliance)
- [x] Zero new words required in `rfopt`: leverages existing `DUP`, `=`, `IF`, `DROP`, `ELSE`, `THEN`, `OVER`, `OR`.
- [x] Data stack balance mathematically proven: net stack delta is identically 0 across all true, multi-value, fallback, and omitted-fallback paths.
- [x] Control-flow stack balance mathematically proven: exactly $N$ `THEN` words close $N$ open branch frames.
- [x] Adherence to NanaST `VISION.md` (no graphical runtime bloat, standard ST syntax, device neutrality).

## 5. Documentation Style & Quality Gate
- [x] Conforms to NanaST `AGENTS.md` documentation style: direct sentences, standard SVO order, no inverted grammar, unpacked noun stacks.
- [x] Conforms to NanaST directory structure conventions.

## 6. Conclusion & Next Steps
The Discrete Ordinal `CASE` Statement design slice is fully qualified (**VERIFIED**). It is ready for pull request submission and downstream worker implementation.
