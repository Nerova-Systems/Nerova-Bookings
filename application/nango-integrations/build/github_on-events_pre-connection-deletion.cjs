"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// github/on-events/pre-connection-deletion.ts
var pre_connection_deletion_exports = {};
__export(pre_connection_deletion_exports, {
  default: () => pre_connection_deletion_default,
  onEvent: () => onEvent
});
module.exports = __toCommonJS(pre_connection_deletion_exports);
var onEvent = {
  type: "onEvent",
  event: "pre-connection-deletion",
  // 'post-connection-creation' | 'validate-connection'
  description: "This script is executed before a connection is deleted",
  exec: async (nango) => {
    await nango.log("Executed");
  }
};
var pre_connection_deletion_default = onEvent;
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  onEvent
});
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsiZ2l0aHViL29uLWV2ZW50cy9wcmUtY29ubmVjdGlvbi1kZWxldGlvbi50cyJdLAogICJzb3VyY2VzQ29udGVudCI6IFsiaW1wb3J0IHsgY3JlYXRlT25FdmVudCB9IGZyb20gJ25hbmdvJztcbmV4cG9ydCBjb25zdCBvbkV2ZW50ID0ge1xuICB0eXBlOiBcIm9uRXZlbnRcIixcbiAgZXZlbnQ6ICdwcmUtY29ubmVjdGlvbi1kZWxldGlvbicsXG4gIC8vICdwb3N0LWNvbm5lY3Rpb24tY3JlYXRpb24nIHwgJ3ZhbGlkYXRlLWNvbm5lY3Rpb24nXG4gIGRlc2NyaXB0aW9uOiAnVGhpcyBzY3JpcHQgaXMgZXhlY3V0ZWQgYmVmb3JlIGEgY29ubmVjdGlvbiBpcyBkZWxldGVkJyxcbiAgZXhlYzogYXN5bmMgbmFuZ28gPT4ge1xuICAgIGF3YWl0IG5hbmdvLmxvZygnRXhlY3V0ZWQnKTtcbiAgfVxufTtcbmV4cG9ydCBkZWZhdWx0IG9uRXZlbnQ7Il0sCiAgIm1hcHBpbmdzIjogIjs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7QUFBQTtBQUFBO0FBQUE7QUFBQTtBQUFBO0FBQUE7QUFDTyxJQUFNLFVBQVU7QUFBQSxFQUNyQixNQUFNO0FBQUEsRUFDTixPQUFPO0FBQUE7QUFBQSxFQUVQLGFBQWE7QUFBQSxFQUNiLE1BQU0sT0FBTSxVQUFTO0FBQ25CLFVBQU0sTUFBTSxJQUFJLFVBQVU7QUFBQSxFQUM1QjtBQUNGO0FBQ0EsSUFBTyxrQ0FBUTsiLAogICJuYW1lcyI6IFtdCn0K
