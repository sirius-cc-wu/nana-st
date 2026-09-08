---
type: "Architecture Decision"
title: "Decision: Retire the AMD64 rfopt Host API"
description: "Replace the AMD64 machine-stack host emitter with an STC-backed NanaST execution boundary on AMD64 and AArch64."
status: "accepted"
tags: [decision, rfopt, nanast, stc, amd64, aarch64]
---

# Decision: Retire the AMD64 rfopt Host API

## Status

Accepted by Sirius Wu on 2026-09-09.

## Context

`rfopt::host` was the first AMD64-only in-process boundary for the NanaST
Boolean pass-through gate. It had its own restricted parser and emitted AMD64
machine-stack instructions. rfopt now has native STC backends for AMD64 and
AArch64. Maintaining the separate emitter would duplicate backend work and
would not provide an AArch64 path.

NanaST still requires a safe boundary for loading generated source, invoking
lifecycle words, and reading or writing named cells. Removing `host` without a
replacement would remove its only current end-to-end execution evidence.

## Decision

Retire `rfopt::host` and replace it with `rfopt::nanast`.

`rfopt::nanast` preserves the restricted-source and opaque-handle contract. It
lowers validated definitions through rfopt's architecture-selected STC emitter.
The supported platform set is AMD64 Linux and AArch64 Linux.

## Consequences

- NanaST changes its integration import from `rfopt::host` to
  `rfopt::nanast`.
- `rfopt/src/host/` and its AMD64 instruction emitter are removed.
- The integration gate remains required and may run on either supported Linux
  architecture.
- Existing AMD64 host-gate documents remain as superseded history.
- AArch64 execution requires target or emulator evidence. Cross-compilation is
  not sufficient evidence.

## Rejected Alternatives

- **Keep `host` and add an AArch64 machine-stack emitter:** This duplicates the
  STC backends and leaves two native code generators for the same source.
- **Remove `host` without a replacement:** This breaks the NanaST runtime gate.
- **Expose `ForthEngine` directly:** It does not provide the restricted-source,
  opaque-cell, or lifecycle contract required by NanaST.

## Links

- [STC NanaST requirements](../features/stc-nanast-boundary/requirements.md)
- [STC NanaST architecture](../features/stc-nanast-boundary/architecture.md)
- [Superseded AMD64 host requirements](../features/rfopt-target/requirements.md)
