# Coverage Scan Results

Date: 2026-05-15

Purpose: record the mechanical scan used to verify that Cal.diy source areas are explicitly mentioned and classified in this spec pack.

## Scope

The scan compared current local Cal.diy source names against the combined text of this spec pack.

Scanned source units:

- Top-level apps.
- API v2 modules.
- API v2 platform modules.
- Web top-level directories.
- Web app route directory names.
- Web modules.
- Docs content directories and files.
- Package directories.
- Feature package directories.
- App-store directories.
- Platform atom directories.
- Platform library files.
- tRPC viewer router directories.
- Coss UI component files.
- Cal UI component directories.
- Prisma model and enum names.
- Test root areas across `apps` and `packages`.

## Results

| Scan group | Total | Missing |
| --- | ---: | ---: |
| Top-level apps | 3 | 0 |
| API v2 modules | 30 | 0 |
| API v2 platform modules | 8 | 0 |
| Web top-level directories | 13 | 0 |
| Unique web app route directory names | 138 | 0 |
| Web modules | 32 | 0 |
| Docs content directories | 2 | 0 |
| Docs content files | 26 | 0 |
| Package directories | 20 | 0 |
| Feature package directories | 61 | 0 |
| App-store directories | 111 | 0 |
| Platform atom directories | 22 | 0 |
| Platform library files | 14 | 0 |
| tRPC viewer routers | 24 | 0 |
| Coss UI components | 50 | 0 |
| Cal UI components | 48 | 0 |
| Prisma models/enums | 146 | 0 |
| Test root areas | 15 | 0 |

The scan found 688 Cal.diy files with test-like paths under `apps` and `packages`; their root areas are represented in the spec pack and the test traceability document requires per-test mapping during implementation planning.

## Interpretation

This result proves source-name coverage at the inventory level. It does not prove behavior has already been implemented. Implementation still must trace each task to exact Cal.diy source files and tests, then classify each source file as port, replace, reference, defer, reject, or not applicable.

## Repeat Requirement

After pulling new Cal.diy changes, rerun the commands in `06-test-and-traceability.md`. If any count or missing value changes, update this spec pack before writing production code.

