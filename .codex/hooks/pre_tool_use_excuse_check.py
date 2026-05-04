from hook_common import EXCUSE_REMINDER, contains_excuse, read_input, write_json


payload = read_input()

if contains_excuse(payload):
    write_json({"systemMessage": EXCUSE_REMINDER})
