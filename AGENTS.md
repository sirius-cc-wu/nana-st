# Repository Instructions

## Documentation Artifact Layout

Use a feature-iteration hybrid structure. Organize long-term documentation by feature once an idea has more than one document. Do not create empty directories in advance.

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

### Directory Guidelines
- **`docs/VISION.md`:** The single project-wide vision document.
- **`docs/SPEC-v<version>.md`:** Holds project-wide, versioned compiler or ABI specifications until multiple specifications justify their own subdirectories.
- **`docs/ideas/`:** Preserves the history of proposed candidate directions. Once an idea is accepted, it must link to active feature artifacts rather than tracking changing requirements.
- **`docs/features/<feature>/`:** Contains active requirements, architecture, and implementation designs for that specific feature.
- **`docs/verification/`, `docs/decisions/`, and `docs/iterations/`:** Create these directories only when you have a specific artifact that requires them.
- **No empty skill taxonomies:** Do not create folders simply because an agent skill or artifact type exists. Only split folders if navigating feature directories becomes unwieldy.
- **Preserve history:** Do not move existing documentation without an approved plan that updates links and preserves file history.
