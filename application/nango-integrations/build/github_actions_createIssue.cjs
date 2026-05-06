"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
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
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// github/actions/createIssue.ts
var createIssue_exports = {};
__export(createIssue_exports, {
  default: () => createIssue_default
});
module.exports = __toCommonJS(createIssue_exports);
var z = __toESM(require("zod"), 1);
var issueSchema = z.object({
  id: z.string(),
  owner: z.string(),
  repo: z.string(),
  issue_number: z.number(),
  title: z.string(),
  state: z.string(),
  author: z.string(),
  author_id: z.number(),
  body: z.string(),
  date_created: z.string(),
  date_last_modified: z.string()
});
var action = {
  type: "action",
  description: `Create an issue in GitHub`,
  version: "1.0.0",
  endpoint: {
    method: "POST",
    path: "/example/github/issues",
    group: "Issues"
  },
  input: issueSchema,
  output: z.void(),
  // Action execution
  exec: async (nango, input) => {
    await nango.proxy({
      endpoint: "/repos/NangoHQ/interactive-demo/issues",
      data: {
        title: `[demo] ${input.title}`,
        body: `This issue was created automatically using Nango Action.`,
        labels: ["automatic"]
      }
    });
  }
};
var createIssue_default = action;
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsiZ2l0aHViL2FjdGlvbnMvY3JlYXRlSXNzdWUudHMiXSwKICAic291cmNlc0NvbnRlbnQiOiBbImltcG9ydCB7IGNyZWF0ZUFjdGlvbiB9IGZyb20gJ25hbmdvJztcbmltcG9ydCAqIGFzIHogZnJvbSAnem9kJztcbmNvbnN0IGlzc3VlU2NoZW1hID0gei5vYmplY3Qoe1xuICBpZDogei5zdHJpbmcoKSxcbiAgb3duZXI6IHouc3RyaW5nKCksXG4gIHJlcG86IHouc3RyaW5nKCksXG4gIGlzc3VlX251bWJlcjogei5udW1iZXIoKSxcbiAgdGl0bGU6IHouc3RyaW5nKCksXG4gIHN0YXRlOiB6LnN0cmluZygpLFxuICBhdXRob3I6IHouc3RyaW5nKCksXG4gIGF1dGhvcl9pZDogei5udW1iZXIoKSxcbiAgYm9keTogei5zdHJpbmcoKSxcbiAgZGF0ZV9jcmVhdGVkOiB6LnN0cmluZygpLFxuICBkYXRlX2xhc3RfbW9kaWZpZWQ6IHouc3RyaW5nKClcbn0pO1xuY29uc3QgYWN0aW9uID0ge1xuICB0eXBlOiBcImFjdGlvblwiLFxuICBkZXNjcmlwdGlvbjogYENyZWF0ZSBhbiBpc3N1ZSBpbiBHaXRIdWJgLFxuICB2ZXJzaW9uOiAnMS4wLjAnLFxuICBlbmRwb2ludDoge1xuICAgIG1ldGhvZDogJ1BPU1QnLFxuICAgIHBhdGg6ICcvZXhhbXBsZS9naXRodWIvaXNzdWVzJyxcbiAgICBncm91cDogJ0lzc3VlcydcbiAgfSxcbiAgaW5wdXQ6IGlzc3VlU2NoZW1hLFxuICBvdXRwdXQ6IHoudm9pZCgpLFxuICAvLyBBY3Rpb24gZXhlY3V0aW9uXG4gIGV4ZWM6IGFzeW5jIChuYW5nbywgaW5wdXQpID0+IHtcbiAgICBhd2FpdCBuYW5nby5wcm94eSh7XG4gICAgICBlbmRwb2ludDogJy9yZXBvcy9OYW5nb0hRL2ludGVyYWN0aXZlLWRlbW8vaXNzdWVzJyxcbiAgICAgIGRhdGE6IHtcbiAgICAgICAgdGl0bGU6IGBbZGVtb10gJHtpbnB1dC50aXRsZX1gLFxuICAgICAgICBib2R5OiBgVGhpcyBpc3N1ZSB3YXMgY3JlYXRlZCBhdXRvbWF0aWNhbGx5IHVzaW5nIE5hbmdvIEFjdGlvbi5gLFxuICAgICAgICBsYWJlbHM6IFsnYXV0b21hdGljJ11cbiAgICAgIH1cbiAgICB9KTtcbiAgfVxufTtcbmV4cG9ydCB0eXBlIE5hbmdvQWN0aW9uTG9jYWwgPSBQYXJhbWV0ZXJzPCh0eXBlb2YgYWN0aW9uKVsnZXhlYyddPlswXTtcbmV4cG9ydCBkZWZhdWx0IGFjdGlvbjsiXSwKICAibWFwcGluZ3MiOiAiOzs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7QUFBQTtBQUFBO0FBQUE7QUFBQTtBQUFBO0FBQ0EsUUFBbUI7QUFDbkIsSUFBTSxjQUFnQixTQUFPO0FBQUEsRUFDM0IsSUFBTSxTQUFPO0FBQUEsRUFDYixPQUFTLFNBQU87QUFBQSxFQUNoQixNQUFRLFNBQU87QUFBQSxFQUNmLGNBQWdCLFNBQU87QUFBQSxFQUN2QixPQUFTLFNBQU87QUFBQSxFQUNoQixPQUFTLFNBQU87QUFBQSxFQUNoQixRQUFVLFNBQU87QUFBQSxFQUNqQixXQUFhLFNBQU87QUFBQSxFQUNwQixNQUFRLFNBQU87QUFBQSxFQUNmLGNBQWdCLFNBQU87QUFBQSxFQUN2QixvQkFBc0IsU0FBTztBQUMvQixDQUFDO0FBQ0QsSUFBTSxTQUFTO0FBQUEsRUFDYixNQUFNO0FBQUEsRUFDTixhQUFhO0FBQUEsRUFDYixTQUFTO0FBQUEsRUFDVCxVQUFVO0FBQUEsSUFDUixRQUFRO0FBQUEsSUFDUixNQUFNO0FBQUEsSUFDTixPQUFPO0FBQUEsRUFDVDtBQUFBLEVBQ0EsT0FBTztBQUFBLEVBQ1AsUUFBVSxPQUFLO0FBQUE7QUFBQSxFQUVmLE1BQU0sT0FBTyxPQUFPLFVBQVU7QUFDNUIsVUFBTSxNQUFNLE1BQU07QUFBQSxNQUNoQixVQUFVO0FBQUEsTUFDVixNQUFNO0FBQUEsUUFDSixPQUFPLFVBQVUsTUFBTSxLQUFLO0FBQUEsUUFDNUIsTUFBTTtBQUFBLFFBQ04sUUFBUSxDQUFDLFdBQVc7QUFBQSxNQUN0QjtBQUFBLElBQ0YsQ0FBQztBQUFBLEVBQ0g7QUFDRjtBQUVBLElBQU8sc0JBQVE7IiwKICAibmFtZXMiOiBbXQp9Cg==
