# Repository Instructions

## Documentation Writing Style

All documentation and Markdown files must be written in a simple, readable technical English style. Follow these guidelines:

- **Direct sentence structure:** Keep sentences concise and clear. Prefer active voice and standard Subject-Verb-Object (SVO) order.
- **No grammatical inversions:** Avoid inverted phrases such as *"Only then does the runner start..."*. Use direct phrasing: *"Then, the runner starts..."*.
- **Unpack dense noun stacks:** Avoid compressing multiple nouns together (e.g., *"in-process rfopt host boundary"*). Unpack them into descriptive phrases with clear prepositions.
- **Avoid anthropomorphic idioms:** Do not use abstract idioms like *"the requirements own the behavior"* or *"AArch64 waits for AMD64"*. Write *"the requirements define the behavior"* and *"AArch64 support begins after AMD64 passes"*.
- **Scannable formatting:** Use structured bullet points, numbered lists, and bold lead-ins for lists and tables to make information easy to find.
- **Preserve precision:** Keep all technical contracts, numerical limits, exact types, and decision histories rigorous while keeping the prose simple.

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
