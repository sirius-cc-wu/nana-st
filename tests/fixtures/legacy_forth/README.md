# Legacy Mapacode Forth Sources

This directory contains a byte-for-byte source capture of every `.fs`, `.4th`,
and `.fth` file found in the default branches of the `mapacode` organization.
The search found 101 `.fs` files and no `.4th` or `.fth` files.

The capture includes PLC-style SFC applications, application support modules,
runtime libraries, examples, benchmarks, and device maintenance scripts. It is
reference material. NanaST does not compile these Forth files.

`MANIFEST.tsv` records the source repository, default branch, source path, Git
blob SHA, size, and retained local path for each file. The saved content hashes
to the recorded Git blob SHA.

The source tree preserves the repository and source-path hierarchy below this
directory. Use the legacy sources to identify behavior that a later NanaST and
BNC migration must replace. Keep new ST behavior fixtures in the parent
`tests/fixtures/` directory.
