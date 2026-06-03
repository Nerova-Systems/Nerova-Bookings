from __future__ import annotations
import sys, socket, urllib3, requests
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# Patch socket.getaddrinfo so app.dev.localhost -> 127.0.0.1 (bypasses Windows .localhost subdomain quirk)
_orig_getaddrinfo = socket.getaddrinfo
def _patched(host, port, *a, **kw):
    if host == "app.dev.localhost":
        host = "127.0.0.1"
    return _orig_getaddrinfo(host, port, *a, **kw)
socket.getaddrinfo = _patched

BASE = "https://app.dev.localhost:9000"
OTP = "UNLOCK"
LOCALE = "en-US"
TZ = "Africa/Johannesburg"

def hm(h, m=0): return h*60+m
MONFRI=[1,2,3,4,5]; TUESAT=[2,3,4,5,6]; SAT=[6]

PERSONAS = [
    {"label":"Solo Doctor","email":"demo.doctor@nerova.dev","business":"Okafor Family Practice","owner":"Dr. Amara Okafor","handle":"dr-okafor","vertical":"Health","schedule_name":"Clinic Hours","windows":[{"days":MONFRI,"startMinute":hm(8),"endMinute":hm(16)}],"services":[("New Patient Consult","new-patient-consult",30),("Follow-Up Visit","follow-up-visit",15),("Telehealth Consult","telehealth-consult",20)]},
    {"label":"Tutor","email":"demo.tutor@nerova.dev","business":"BrightMinds Tutoring","owner":"James Pillay","handle":"brightminds","vertical":"Education","schedule_name":"Lesson Hours","windows":[{"days":MONFRI,"startMinute":hm(14),"endMinute":hm(19)},{"days":SAT,"startMinute":hm(9),"endMinute":hm(13)}],"services":[("Free Intro Call","free-intro-call",15),("Maths Lesson","maths-lesson",60),("Exam Prep Intensive","exam-prep-intensive",90)]},
    {"label":"Life Coach","email":"demo.coach@nerova.dev","business":"Thandi Nkosi Coaching","owner":"Thandi Nkosi","handle":"thandi-coaching","vertical":"ProfessionalServices","schedule_name":"Coaching Hours","windows":[{"days":MONFRI,"startMinute":hm(9),"endMinute":hm(17)}],"services":[("Discovery Call","discovery-call",30),("1-on-1 Coaching","1-on-1-coaching",60),("Deep-Dive Intensive","deep-dive-intensive",120)]},
    {"label":"Hair Stylist","email":"demo.hair@nerova.dev","business":"Lerato Hair Studio","owner":"Lerato Molefe","handle":"lerato-hair","vertical":"Beauty","schedule_name":"Salon Hours","windows":[{"days":TUESAT,"startMinute":hm(9),"endMinute":hm(18)}],"services":[("Cut and Blow-Dry","cut-and-blow-dry",45),("Colour and Highlights","colour-and-highlights",150),("Box Braids","box-braids",240)]},
    {"label":"Nail Specialist","email":"demo.nails@nerova.dev","business":"Polished by Zinhle","owner":"Zinhle Dube","handle":"polished-by-zinhle","vertical":"Beauty","schedule_name":"Studio Hours","windows":[{"days":TUESAT,"startMinute":hm(9),"endMinute":hm(17)}],"services":[("Classic Manicure","classic-manicure",45),("Gel Overlay","gel-overlay",60),("Full Acrylic Set","full-acrylic-set",90)]},
]

class ApiError(Exception): pass

def req(s, method, path, token=None, **kw):
    h = kw.pop("headers", {})
    if token: h["Authorization"] = f"Bearer {token}"
    return s.request(method, BASE+path, headers=h, verify=False, timeout=60, **kw)

def ok(r, what):
    if not r.ok: raise ApiError(f"{what} HTTP {r.status_code}: {r.text[:300]}")
    return r

def auth(s, email):
    r = req(s,"POST","/api/account/authentication/email/signup/start",json={"email":email})
    if r.ok:
        lid = r.json()["emailLoginId"]
        c = req(s,"POST",f"/api/account/authentication/email/signup/{lid}/complete",json={"oneTimePassword":OTP,"preferredLocale":LOCALE})
        if c.ok:
            t = c.headers.get("x-access-token")
            if t: return t,"signup"
    r = ok(req(s,"POST","/api/account/authentication/email/login/start",json={"email":email}),"login/start")
    lid = r.json()["emailLoginId"]
    c = ok(req(s,"POST",f"/api/account/authentication/email/login/{lid}/complete",json={"oneTimePassword":OTP}),"login/complete")
    t = c.headers.get("x-access-token")
    if not t: raise ApiError("no x-access-token in login response")
    return t,"login"

def seed(p):
    res={"persona":p["label"],"email":p["email"],"handle":p["handle"],"svcs_ok":0,"svcs_total":len(p["services"]),"pub_ok":0,"notes":[]}
    with requests.Session() as s:
        token,mode = auth(s, p["email"])
        res["mode"]=mode
        r=req(s,"PUT","/api/account/tenants/current",token=token,json={"name":p["business"]})
        if r.ok and r.headers.get("x-access-token"): token=r.headers["x-access-token"]
        elif not r.ok: res["notes"].append(f"tenant-name {r.status_code}")
        r=req(s,"PUT","/api/account/tenants/current/brand-profile",token=token,json={"businessDisplayName":p["business"],"brandVertical":p["vertical"]})
        if not r.ok: res["notes"].append(f"brand-profile {r.status_code} {r.text[:80]}")
        ok(req(s,"PUT","/api/scheduling/profile",token=token,json={"handle":p["handle"],"displayName":p["owner"],"avatarUrl":None}),"sched-profile")
        sched=ok(req(s,"POST","/api/schedules",token=token,json={"name":p["schedule_name"],"timeZone":TZ,"isDefault":True,"availabilityWindows":p["windows"],"dateOverrides":[]}),"schedules").json()
        sid=sched["id"]
        for title,slug,dur in p["services"]:
            r=req(s,"POST","/api/event-types",token=token,json={"title":title,"slug":slug,"description":f"{title} - {p['owner']}","durationMinutes":dur,"hidden":False,"scheduleId":sid,"beforeEventBufferMinutes":0,"afterEventBufferMinutes":0,"slotIntervalMinutes":dur,"minimumBookingNoticeMinutes":0,"locationType":None,"locationValue":None})
            if r.ok: res["svcs_ok"]+=1
            else: res["notes"].append(f"event-type {slug} {r.status_code} {r.text[:80]}")
        for _,slug,_ in p["services"]:
            r=req(s,"GET",f"/api/public/event-types/{p['handle']}/{slug}")
            if r.ok: res["pub_ok"]+=1
            else: res["notes"].append(f"public {slug} {r.status_code}")
    return res

def main():
    print(f"Seeding {len(PERSONAS)} tenants against {BASE}\n")
    results=[]
    for p in PERSONAS:
        print(f"  {p['label']:<16} {p['business']}")
        try: results.append(seed(p))
        except Exception as e:
            print(f"    FAILED: {e}")
            results.append({"persona":p["label"],"email":p["email"],"handle":p["handle"],"error":str(e),"notes":[]})
    print("\n"+"="*72)
    all_ok=True
    for r in results:
        if r.get("error"):
            all_ok=False
            print(f"{r['persona']:<16} ERROR: {r['error'][:60]}")
            continue
        svc=f"{r['svcs_ok']}/{r['svcs_total']}"; pub=f"{r['pub_ok']}/{r['svcs_total']}"
        if r["svcs_ok"]!=r["svcs_total"] or r["pub_ok"]!=r["svcs_total"]: all_ok=False
        print(f"{r['persona']:<16} {r['email']:<30} svcs={svc} pub={pub}  {BASE}/{r['handle']}")
        for n in r["notes"]: print(f"  NOTE: {n}")
    print("\nOTP=UNLOCK for all accounts (dev only)")
    print("STATUS:", "ALL OPERATIONAL" if all_ok else "ISSUES - see above")
    return 0 if all_ok else 1

if __name__=="__main__": sys.exit(main())