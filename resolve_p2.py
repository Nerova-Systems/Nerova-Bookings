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

def nc(rel):
    c = rf(rel)
    return len(CONFLICT.findall(c))

# ─── Test files (all take MERGE except BookingEndpointsTests) ────────────────

for f in [
    "application/main/Tests/Insights/GetBookingKpisQueryTests.cs",
    "application/main/Tests/Insights/GetCancellationReasonsQueryTests.cs",
    "application/main/Tests/Insights/GetTopEventTypesQueryTests.cs",
    "application/main/Tests/Insights/GetBookingFunnelQueryTests.cs",
    "application/main/Tests/RoundRobin/RoundRobinBookingFlowTests.cs",
    "application/main/Tests/RoundRobin/RoundRobinEndpointTests.cs",
    "application/main/Tests/RoundRobin/RoundRobinSlotCalculatorTests.cs",
    "application/main/Tests/Collective/CollectiveSlotCalculatorTests.cs",
]:
    print(f"MERGE: {f.split('/')[-1]} ({nc(f)} conflicts)")
    wf(f, take_merge(rf(f)))

# BookingEndpointsTests.cs: custom — Reschedule→false (MERGE) + RequestReschedule→true (HEAD)
print("CUSTOM: BookingEndpointsTests.cs")
f = "application/main/Tests/Scheduling/BookingEndpointsTests.cs"
c = rf(f)
print(f"  conflicts: {len(CONFLICT.findall(c))}")
def booking_endpoints_test(head, merge):
    # MERGE has: Reschedule.Enabled.Should().BeFalse()
    # HEAD has: RequestReschedule.Enabled.Should().BeTrue()
    # Keep both
    m_line = merge.strip()
    h_line = head.strip()
    return m_line + "\n            " + h_line
wf(f, CONFLICT.sub(lambda m: booking_endpoints_test(m.group(1), m.group(2)), c))

# ─── Frontend: take MERGE ────────────────────────────────────────────────────
for f in [
    "application/main/WebApp/routes/-bookings/BookingActionDialogs.tsx",
    "application/main/WebApp/routes/-bookings/BookingActionsDropdown.tsx",
    "application/main/WebApp/routes/-bookings/BookingDetailsSheet.tsx",
    "application/main/WebApp/routes/-bookings/BookingListContainer.tsx",
    "application/main/WebApp/routes/-bookings/BookingsList.tsx",
    "application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAppsTab.tsx",
]:
    print(f"MERGE: {f.split('/')[-1]} ({nc(f)} conflicts)")
    wf(f, take_merge(rf(f)))

# ─── Frontend: take HEAD (more complete implementations) ─────────────────────
for f in [
    "application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeWebhooksTab.tsx",
    "application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeWorkflowsTab.tsx",
]:
    print(f"HEAD: {f.split('/')[-1]} ({nc(f)} conflicts)")
    wf(f, take_head(rf(f)))

print("Phase 2 complete!")
