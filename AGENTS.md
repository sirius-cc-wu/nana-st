# Repository Instructions

## Documentation Artifact Layout

Use a feature-iteration hybrid. Organize durable documentation by its current
feature once a candidate direction has more than one maintained artifact; do
not create empty taxonomy directories in anticipation of work.

```text
docs/
  VISION.md
  SPEC-v<version>.md
  ideas/<feature>.md
  features/<feature>/
    requirements.md
    architecture.md
    rust-lifecycle.md
  verification/<feature>/
  decisions/
  iterations/
```

- `docs/VISION.md` is the single project-level vision.
- `docs/SPEC-v<version>.md` holds a project-wide, versioned compiler or ABI
  specification until several independently maintained specifications justify
  their own directory.
- `docs/ideas/` retains candidate-direction history. An accepted idea must link
  to the current feature artifacts; it is not the owner of evolving approved
  requirements.
- `docs/features/<feature>/` owns active requirements, architecture, and
  implementation-facing design for that feature.
- Create `docs/verification/`, `docs/decisions/`, and `docs/iterations/` only
  when an artifact of that kind is justified.
- Do not create a directory per skill or artifact type merely because a skill
  exists. Use an artifact-specific layout only after several independently
  maintained artifacts make feature-local navigation inadequate.
- Do not relocate existing documentation without an approved migration that
  updates its links and preserves history.
