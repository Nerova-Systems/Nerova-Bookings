# Cal.com Port Implementation

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Status

Spec namespace rewrite complete. Cal.com inventory regeneration is required before implementation work starts.

## Completed

- Renamed the planning source from Cal.diy port to Cal.com port.
- Locked Cal.com as the primary source of truth.
- Preserved Cal.diy as secondary simplified reference only.
- Recorded feature-sliced rewrite as the implementation strategy.
- Recorded strict core scheduling parity as the first implementation priority.
- Recorded Solo-first product direction with Teams and Organizations seams preserved.
- Recorded WhatsApp Flow as the Solo public booking replacement.
- Recorded future Nerova business systems as deferred boundaries.

## Superseded

The prior Cal.diy source inventory and test traceability tables are obsolete for implementation planning. They remain useful only as historical context until regenerated from Cal.com.

## Next Steps

1. Regenerate source inventory from `cal.com`.
2. Regenerate test traceability from `cal.com`.
3. Add a Cal.diy delta appendix only after Cal.com inventory is complete.
4. Rewrite Linear roadmap issues from verified Cal.com source facts.
5. Start implementation planning with the scheduling foundation only after inventory and traceability are current.

## Implementation Gate

No Cal.com port implementation issue may start until it has exact Cal.com source paths, target Nerova files, tests, UI parity notes when applicable, feature flag or plan-gating rules, and Guardian validation gates.

## Session Notes

### 2026-05-15

- Verified Aspire MCP for the current workspace AppHost.
- Verified Cal.com local checkout at `cf2a55c42363ab79982eef11610e1de8151b45ce` on `fix/team-attributes-for-org-admins`.
- Verified Cal.diy local checkout at `180ede28f0bddf2738933a6e60a8e80f6116d7da` on `main`.
- Verified Cal.com has 10,070 files visible through `rg` and 1,080 visible test/spec/e2e matches by filename scan.
