import re

CONFLICT = re.compile(r'<<<<<<< HEAD\r?\n(.*?)\r?\n=======\r?\n(.*?)\r?\n?>>>>>>> [0-9a-f]+[^\r\n]*', re.DOTALL)

for lang in ['en-US', 'da-DK']:
    path = r'application\main\WebApp\shared\translations\locale\\' + lang + '.po'
    with open(path, 'r', encoding='utf-8') as f:
        c = f.read()
    ms = CONFLICT.findall(c)
    print(lang + ': ' + str(len(ms)) + ' conflicts')
    for i, (h, m) in enumerate(ms):
        head_ids = set(re.findall(r'msgid "([^"]+)"', h))
        merge_ids = set(re.findall(r'msgid "([^"]+)"', m))
        overlap = head_ids & merge_ids
        if overlap:
            print('  conflict ' + str(i) + ' overlap: ' + str(overlap))
print('Done')
