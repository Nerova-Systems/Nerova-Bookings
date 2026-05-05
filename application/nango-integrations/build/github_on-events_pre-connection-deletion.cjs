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

// nango-integrations/github/on-events/pre-connection-deletion.ts
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
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsibmFuZ28taW50ZWdyYXRpb25zL2dpdGh1Yi9vbi1ldmVudHMvcHJlLWNvbm5lY3Rpb24tZGVsZXRpb24udHMiXSwKICAic291cmNlc0NvbnRlbnQiOiBbImltcG9ydCB7IGNyZWF0ZU9uRXZlbnQgfSBmcm9tICduYW5nbyc7XG5leHBvcnQgY29uc3Qgb25FdmVudCA9IHtcbiAgdHlwZTogXCJvbkV2ZW50XCIsXG4gIGV2ZW50OiAncHJlLWNvbm5lY3Rpb24tZGVsZXRpb24nLFxuICAvLyAncG9zdC1jb25uZWN0aW9uLWNyZWF0aW9uJyB8ICd2YWxpZGF0ZS1jb25uZWN0aW9uJ1xuICBkZXNjcmlwdGlvbjogJ1RoaXMgc2NyaXB0IGlzIGV4ZWN1dGVkIGJlZm9yZSBhIGNvbm5lY3Rpb24gaXMgZGVsZXRlZCcsXG4gIGV4ZWM6IGFzeW5jIG5hbmdvID0+IHtcbiAgICBhd2FpdCBuYW5nby5sb2coJ0V4ZWN1dGVkJyk7XG4gIH1cbn07XG5leHBvcnQgZGVmYXVsdCBvbkV2ZW50OyJdLAogICJtYXBwaW5ncyI6ICI7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7O0FBQUE7QUFBQTtBQUFBO0FBQUE7QUFBQTtBQUFBO0FBQ08sSUFBTSxVQUFVO0FBQUEsRUFDckIsTUFBTTtBQUFBLEVBQ04sT0FBTztBQUFBO0FBQUEsRUFFUCxhQUFhO0FBQUEsRUFDYixNQUFNLE9BQU0sVUFBUztBQUNuQixVQUFNLE1BQU0sSUFBSSxVQUFVO0FBQUEsRUFDNUI7QUFDRjtBQUNBLElBQU8sa0NBQVE7IiwKICAibmFtZXMiOiBbXQp9Cg==
