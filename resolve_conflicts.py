import re, os, sys

BASE = r"C:\Users\colin\dev\NerovaBookings"
def p(rel): return os.path.join(BASE, rel.replace("/", os.sep))
def rf(rel):
    with open(p(rel), "r", encoding="utf-8") as f: return f.read()
def wf(rel, content):
    with open(p(rel), "w", encoding="utf-8") as f: f.write(content)

CONFLICT = re.compile(r"<<<<<<< HEAD\r?\n(.*?)\r?\n=======\r?\n(.*?)\r?\n>>>>>>> [0-9a-f]+[^\r\n]*", re.DOTALL)

def take_head(c): return CONFLICT.sub(lambda m: m.group(1), c)
def take_merge(c): return CONFLICT.sub(lambda m: m.group(2), c)
def take_both(c): return CONFLICT.sub(lambda m: m.group(1) + "\n" + m.group(2), c)

def resolve_nth(c, n, fn):
    """Resolve only the nth conflict (0-based) using fn(head, merge)."""
    result = c
    ms = list(CONFLICT.finditer(result))
    if n >= len(ms):
        raise ValueError(f"No conflict #{n} in content (found {len(ms)})")
    m = ms[n]
    return result[:m.start()] + fn(m.group(1), m.group(2)) + result[m.end():]

def resolve_all(c, fn):
    """Resolve all conflicts using fn(head, merge)."""
    return CONFLICT.sub(lambda m: fn(m.group(1), m.group(2)), c)

def resolve_list(c, fns):
    """Apply fns[i] to the ith conflict. len(fns) must match conflict count."""
    ms = list(CONFLICT.finditer(c))
    if len(ms) != len(fns):
        raise ValueError(f"Expected {len(fns)} conflicts, found {len(ms)}")
    result = c
    for i in range(len(fns) - 1, -1, -1):
        m = list(CONFLICT.finditer(result))[i]
        result = result[:m.start()] + fns[i](m.group(1), m.group(2)) + result[m.end():]
    return result

# ─── 1. AddPbacDomain.cs — keep HEAD (uses permColumnTypes variable) ─────────
print("1. AddPbacDomain.cs"); sys.stdout.flush()
f = "application/account/Core/Database/Migrations/20260522100000_AddPbacDomain.cs"
wf(f, take_head(rf(f)))

# ─── 2. GetCancellationReasonsQuery.cs — keep MERGE (enum) ──────────────────
print("2. GetCancellationReasonsQuery.cs"); sys.stdout.flush()
f = "application/main/Core/Features/Insights/Queries/GetCancellationReasons/GetCancellationReasonsQuery.cs"
wf(f, take_merge(rf(f)))

# ─── 3. GetTopEventTypesQuery.cs — keep MERGE ───────────────────────────────
print("3. GetTopEventTypesQuery.cs"); sys.stdout.flush()
f = "application/main/Core/Features/Insights/Queries/GetTopEventTypes/GetTopEventTypesQuery.cs"
wf(f, take_merge(rf(f)))

# ─── 4. PublicSlotCalculator.cs — MERGE sig but pass [] for busyWindows ─────
print("4. PublicSlotCalculator.cs"); sys.stdout.flush()
f = "application/main/Core/Features/Scheduling/Shared/PublicSlotCalculator.cs"
c = rf(f)
n = len(CONFLICT.findall(c))
print(f"   conflicts found: {n}")
def _slotcalc(head, merge):
    return merge.replace(
        "GetSlots(eventType, schedule, bookings, startTime,",
        "GetSlots(eventType, schedule, bookings, [], startTime,"
    )
wf(f, resolve_all(c, _slotcalc))

# ─── 5. GetPublicSlots.cs — keep MERGE ───────────────────────────────────────
print("5. GetPublicSlots.cs"); sys.stdout.flush()
f = "application/main/Core/Features/Scheduling/Queries/GetPublicSlots.cs"
wf(f, take_merge(rf(f)))

# ─── 6. GetBookings.cs — keep BOTH constructor params ────────────────────────
print("6. GetBookings.cs"); sys.stdout.flush()
f = "application/main/Core/Features/Scheduling/Queries/GetBookings.cs"
c = rf(f)
print(f"   conflicts: {len(CONFLICT.findall(c))}")
wf(f, resolve_all(c, lambda h, m: h + ",\n" + m))

# ─── 7. CreatePublicBooking.cs ── both params + MERGE slot call ──────────────
print("7. CreatePublicBooking.cs"); sys.stdout.flush()
f = "application/main/Core/Features/Scheduling/Commands/CreatePublicBooking.cs"
c = rf(f)
n = len(CONFLICT.findall(c))
print(f"   conflicts: {n}")
if n == 2:
    wf(f, resolve_list(c, [
        lambda h, m: h + ",\n" + m,  # keep both constructor params
        lambda h, m: m               # keep MERGE slot call
    ]))
else:
    wf(f, take_merge(c))

# ─── 8. BookingActionAvailability.cs — keep MERGE ───────────────────────────
print("8. BookingActionAvailability.cs"); sys.stdout.flush()
f = "application/main/Core/Features/Scheduling/Shared/BookingActionAvailability.cs"
wf(f, take_merge(rf(f)))

# ─── 9. BookingEndpoints.cs — keep MERGE ────────────────────────────────────
print("9. BookingEndpoints.cs"); sys.stdout.flush()
f = "application/main/Api/Endpoints/BookingEndpoints.cs"
wf(f, take_merge(rf(f)))

# ─── 10. EventType.cs — keep BOTH method sets ──────────────────────────────
print("10. EventType.cs"); sys.stdout.flush()
f = "application/main/Core/Features/EventTypes/Domain/EventType.cs"
wf(f, take_both(rf(f)))

# ─── 11. Configuration.cs ───────────────────────────────────────────────────
print("11. Configuration.cs"); sys.stdout.flush()
f = "application/main/Core/Configuration.cs"
c = rf(f)
n = len(CONFLICT.findall(c))
print(f"   conflicts: {n}")

def cfg_usings(h, m):
    lines = set(m.splitlines())
    lines.add("using Main.Features.BookingSideEffects.Workers;")
    return "\n".join(sorted(lines))

def cfg_method(h, m):
    head_lines = h.splitlines()
    try_lines = [l for l in head_lines if "TryAddSingleton" in l]
    merge_body = m.strip()
    merge_body = merge_body.replace(
        ".AddScoped<IPermissionCheckService, PermissionCheckService>()",
        ".AddScoped<IPermissionCheckService, PermissionCheckService>()\n                .AddScoped<BookingSideEffectProcessor>()"
    )
    result = "\n".join(f"            {l.strip()}" for l in try_lines)
    result += "\n\n"
    result += "            " + merge_body.lstrip()
    return result

if n == 2:
    wf(f, resolve_list(c, [cfg_usings, cfg_method]))
else:
    print(f"   WARNING: expected 2 conflicts, got {n}")
    wf(f, take_merge(c))

print("Phase 1 complete!"); sys.stdout.flush()
