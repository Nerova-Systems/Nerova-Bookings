import re, os

BASE = r"C:\Users\colin\dev\NerovaBookings"
def rf(rel):
    with open(os.path.join(BASE, rel.replace("/", os.sep)), "r", encoding="utf-8") as f: return f.read()
def wf(rel, content):
    with open(os.path.join(BASE, rel.replace("/", os.sep)), "w", encoding="utf-8") as f: f.write(content)

# Updated regex that handles empty MERGE
CONFLICT = re.compile(
    r"<<<<<<< HEAD\r?\n(.*?)\r?\n=======\r?\n(.*?)\r?\n?>>>>>>> [0-9a-f]+[^\r\n]*",
    re.DOTALL
)

for lang in ["en-US", "da-DK"]:
    rel = "application/main/WebApp/shared/translations/locale/" + lang + ".po"
    c = rf(rel)
    count_before = len(CONFLICT.findall(c))
    # For remaining conflicts: just take_both (HEAD + MERGE, both are unique entries)
    result = CONFLICT.sub(lambda m: m.group(1).rstrip() + "\n\n" + m.group(2).strip(), c)
    count_after = len(CONFLICT.findall(result))
    print(lang + ": " + str(count_before) + " -> " + str(count_after) + " conflicts")
    wf(rel, result)

print("Done!")
