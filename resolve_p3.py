"""
Resolves complex TypeScript/TSX file conflicts.
"""
import re, os

BASE = r"C:\Users\colin\dev\NerovaBookings"
def p(rel): return os.path.join(BASE, rel.replace("/", os.sep))
def rf(rel):
    with open(p(rel), "r", encoding="utf-8") as f: return f.read()
def wf(rel, content):
    with open(p(rel), "w", encoding="utf-8") as f: f.write(content)

# Regex that handles empty MERGE side (=======\n>>>>>>> sha with no content between)
CONFLICT = re.compile(
    r"<<<<<<< HEAD\r?\n(.*?)\r?\n=======\r?\n(.*?)\r?\n?>>>>>>> [0-9a-f]+[^\r\n]*",
    re.DOTALL
)
def take_head(c): return CONFLICT.sub(lambda m: m.group(1), c)
def take_merge(c): return CONFLICT.sub(lambda m: m.group(2), c)

# ─── EventTypeEditorTabs.tsx ─────────────────────────────────────────────────
print("EventTypeEditorTabs.tsx")
f = "application/main/WebApp/routes/-scheduling/event-types-shell/EventTypeEditorTabs.tsx"
c = rf(f)
ms = list(CONFLICT.finditer(c))
print(f"  conflicts: {len(ms)}")
for i, m in enumerate(ms):
    print(f"  [{i}] head: {m.group(1)[:80].replace(chr(10), '|')!r}")
    print(f"       merge: {m.group(2)[:80].replace(chr(10), '|')!r}")

def editor_tabs_imports(h, m):
    """Keep both sets of tab imports."""
    # HEAD has EventTypeAppsTab, EventTypeAvailabilityTab, etc. + EventTypeTabProps
    # MERGE has EventTypeAiVoiceAgentTab, EventTypeInstantMeetingTab, EventTypeTeamTab
    # Merge: HEAD imports + MERGE's new ones, sorted
    head_lines = set(h.splitlines())
    merge_lines = set(m.splitlines())
    combined = sorted(head_lines | merge_lines)
    return "\n".join(combined)

def editor_tabs_body(h, m):
    """Keep HEAD structure but add eventTypeId to tabProps."""
    # HEAD: tabProps = { value: draft, schedules, onChange, error }
    # MERGE: tabProps = { eventTypeId, value: draft, schedules, onChange, error }
    # Use HEAD's full body but inject eventTypeId into tabProps
    result = h.replace(
        "const tabProps = { value: draft, schedules, onChange, error };",
        "const tabProps = { eventTypeId, value: draft, schedules, onChange, error };"
    )
    return result

def editor_tabs_content(h, m):
    """Keep HEAD's renderEventTypeTab approach."""
    return h

if len(ms) == 3:
    result = c
    fns = [editor_tabs_imports, editor_tabs_body, editor_tabs_content]
    for i in range(len(fns) - 1, -1, -1):
        mlist = list(CONFLICT.finditer(result))
        mi = mlist[i]
        result = result[:mi.start()] + fns[i](mi.group(1), mi.group(2)) + result[mi.end():]
    wf(f, result)
    print("  Done!")
else:
    print(f"  UNEXPECTED: {len(ms)} conflicts, need 3")

# ─── eventTypeShellTypes.ts ──────────────────────────────────────────────────
print("eventTypeShellTypes.ts")
f = "application/main/WebApp/routes/-scheduling/event-types-shell/eventTypeShellTypes.ts"
c = rf(f)
ms = list(CONFLICT.finditer(c))
print(f"  conflicts: {len(ms)}")

if len(ms) == 3:
    # Conflict 0: type union — union of all tab names
    # HEAD: apps, workflows, webhooks, recurring, dependencies
    # MERGE: recurring, team, instant-meeting, ai-voice-agent, workflows, webhooks, apps
    def union_type(h, m):
        head_tabs = set(re.findall(r'"([^"]+)"', h))
        merge_tabs = set(re.findall(r'"([^"]+)"', m))
        all_tabs = sorted(head_tabs | merge_tabs)
        return "\n".join(f'  | "{t}"' for t in all_tabs) + ";"

    # Conflict 1: array literal — union ordered sensibly
    def union_array(h, m):
        # The setup/availability/limits/advanced are before the conflict
        # Add all from both: recurring, team, instant-meeting, ai-voice-agent, workflows, webhooks, apps, dependencies
        merge_items = [x.strip().strip('"') for x in m.split(",\n") if '"' in x]
        head_items = [x.strip().strip('"') for x in h.split(",\n") if '"' in x]
        seen = set()
        combined = []
        for item in (merge_items + head_items):
            if item and item not in seen:
                seen.add(item)
                combined.append(item)
        lines = [f'  "{t}"' for t in combined]
        return ",\n".join(lines)

    # Conflict 2: isEventTypeTabName function — use MERGE's concise form
    def use_merge_fn(h, m):
        return m

    fns = [union_type, union_array, use_merge_fn]
    result = c
    for i in range(len(fns) - 1, -1, -1):
        mlist = list(CONFLICT.finditer(result))
        mi = mlist[i]
        result = result[:mi.start()] + fns[i](mi.group(1), mi.group(2)) + result[mi.end():]
    wf(f, result)
    print("  Done!")
else:
    print(f"  UNEXPECTED: {len(ms)} conflicts, need 3")

# ─── schedulingTypes.ts ──────────────────────────────────────────────────────
print("schedulingTypes.ts")
f = "application/main/WebApp/routes/-scheduling/schedulingTypes.ts"
c = rf(f)
ms = list(CONFLICT.finditer(c))
print(f"  conflicts: {len(ms)}")
if len(ms) == 1:
    def merge_settings(h, m):
        # HEAD: selectedCalendars, destinationCalendar, defaultConferencing, metadata
        # MERGE: metadata, instantMeeting, aiVoiceAgent, teamAssignment, timezone, privacy, email, enablePerHostLocations
        # Keep ALL from HEAD (cal.com connector fields) + all from MERGE
        # The HEAD ends with `metadata: ...` and MERGE starts with `metadata: ...`
        # Use MERGE version of metadata (same syntax), then add HEAD's connector fields
        head_lines = [l for l in h.rstrip().rstrip(",").split("\n") if l.strip()]
        # head has: selectedCalendars, destinationCalendar, defaultConferencing, metadata (last)
        # MERGE already has metadata so just add the other 3 from HEAD before it
        connector_lines = [l for l in head_lines if "metadata" not in l]
        merge_trimmed = m.rstrip()
        # Insert connector fields just before enablePerHostLocations (the last MERGE item)
        result = merge_trimmed + ",\n" + "\n".join(connector_lines)
        return result
    mi = ms[0]
    result = c[:mi.start()] + merge_settings(mi.group(1), mi.group(2)) + c[mi.end():]
    wf(f, result)
    print("  Done!")
else:
    print(f"  UNEXPECTED: {len(ms)} conflicts, need 1")

# ─── EventTypeAdvancedTab.tsx ─────────────────────────────────────────────────
print("EventTypeAdvancedTab.tsx")
f = "application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAdvancedTab.tsx"
c = rf(f)
ms = list(CONFLICT.finditer(c))
print(f"  conflicts: {len(ms)} (expected 4, may show as 3 with empty MERGE)")
for i, m in enumerate(ms):
    print(f"  [{i}] head: {m.group(1)[:80].replace(chr(10), '|')!r}")
    print(f"       merge: {m.group(2)[:50].replace(chr(10), '|')!r}")

def adv_conflict0(h, m):
    """Keep BOTH: HEAD's connector helpers + MERGE's privacy/email/timezone helpers."""
    return h.rstrip() + "\n\n" + m.strip()

def adv_conflict1(h, m):
    """Keep MERGE: EventTypePrivateLinksSection component."""
    return m

def adv_conflict2(h, m):
    """Keep HEAD: Connected features section. MERGE is empty."""
    return h

def adv_conflict3(h, m):
    """Keep HEAD: type declarations + helper functions. MERGE is empty."""
    return h

if len(ms) == 4:
    fns = [adv_conflict0, adv_conflict1, adv_conflict2, adv_conflict3]
    result = c
    for i in range(len(fns) - 1, -1, -1):
        mlist = list(CONFLICT.finditer(result))
        mi = mlist[i]
        result = result[:mi.start()] + fns[i](mi.group(1), mi.group(2)) + result[mi.end():]
    wf(f, result)
    print("  Done!")
elif len(ms) == 3:
    # The updated regex SHOULD find 4, but if we still get 3, apply manually
    print("  Only 3 found, applying manually...")
    fns = [adv_conflict0, adv_conflict1]
    result = c
    for i in range(len(fns) - 1, -1, -1):
        mlist = list(CONFLICT.finditer(result))
        mi = mlist[i]
        result = result[:mi.start()] + fns[i](mi.group(1), mi.group(2)) + result[mi.end():]
    # Now handle conflicts 2 and 3 manually (empty MERGE side)
    result = result.replace(
        "=======\n>>>>>>> 8bbffd68b3d3daf7ae4eb45c2e9a08472699e0a6\n",
        "\n"
    ).replace("<<<<<<< HEAD\n", "")
    wf(f, result)
    print("  Done (manual fallback)!")
else:
    print(f"  UNEXPECTED: {len(ms)} conflicts!")

print("Phase 3 complete!")
