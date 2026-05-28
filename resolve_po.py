"""
Resolves en-US.po and da-DK.po conflicts.
Strategy: merge all msgid/msgstr pairs from both sides; for duplicates use MERGE.
da-DK: same but keep existing translations; new MERGE-only strings get empty msgstr if MERGE has empty.
"""
import re, os

BASE = r"C:\Users\colin\dev\NerovaBookings"
def p(rel): return os.path.join(BASE, rel.replace("/", os.sep))
def rf(rel):
    with open(p(rel), "r", encoding="utf-8") as f: return f.read()
def wf(rel, content):
    with open(p(rel), "w", encoding="utf-8") as f: f.write(content)

CONFLICT = re.compile(
    r"<<<<<<< HEAD\r?\n(.*?)\r?\n=======\r?\n(.*?)\r?\n?>>>>>>> [0-9a-f]+[^\r\n]*",
    re.DOTALL
)

def parse_po_entries(block):
    """Extract list of (msgid, full_entry_text) from a po block."""
    # Split on blank lines to get individual entries
    entries = re.split(r"\n\n", block.strip())
    result = []
    for entry in entries:
        entry = entry.strip()
        if not entry:
            continue
        m = re.search(r'^msgid "([^"]*(?:\\"[^"]*)*)"', entry, re.MULTILINE)
        if m:
            result.append((m.group(1), entry))
    return result

def merge_po_block(head, merge, prefer_merge=True):
    """Merge two conflict sides, keeping all unique msgids. prefer_merge wins for duplicates."""
    head_entries = parse_po_entries(head)
    merge_entries = parse_po_entries(merge)
    head_map = {msgid: text for msgid, text in head_entries}
    merge_map = {msgid: text for msgid, text in merge_entries}
    all_msgids = list(dict.fromkeys(
        [msgid for msgid, _ in head_entries] + [msgid for msgid, _ in merge_entries]
    ))
    result_entries = []
    for msgid in all_msgids:
        if msgid in merge_map and msgid in head_map:
            result_entries.append(merge_map[msgid] if prefer_merge else head_map[msgid])
        elif msgid in merge_map:
            result_entries.append(merge_map[msgid])
        else:
            result_entries.append(head_map[msgid])
    return "\n\n".join(result_entries)

def resolve_po_file(rel, prefer_merge=True):
    c = rf(rel)
    count = len(CONFLICT.findall(c))
    print(f"  {rel.split('/')[-1]}: {count} conflicts")
    result = CONFLICT.sub(lambda m: merge_po_block(m.group(1), m.group(2), prefer_merge), c)
    remaining = len(CONFLICT.findall(result))
    print(f"  remaining markers: {remaining}")
    wf(rel, result)

print("Resolving en-US.po...")
resolve_po_file("application/main/WebApp/shared/translations/locale/en-US.po", prefer_merge=True)

print("Resolving da-DK.po...")
resolve_po_file("application/main/WebApp/shared/translations/locale/da-DK.po", prefer_merge=True)

print("Done!")
