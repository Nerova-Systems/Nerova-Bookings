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

// nango-integrations/github/actions/createIssue.ts
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
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsibmFuZ28taW50ZWdyYXRpb25zL2dpdGh1Yi9hY3Rpb25zL2NyZWF0ZUlzc3VlLnRzIl0sCiAgInNvdXJjZXNDb250ZW50IjogWyJpbXBvcnQgeyBjcmVhdGVBY3Rpb24gfSBmcm9tICduYW5nbyc7XG5pbXBvcnQgKiBhcyB6IGZyb20gJ3pvZCc7XG5jb25zdCBpc3N1ZVNjaGVtYSA9IHoub2JqZWN0KHtcbiAgaWQ6IHouc3RyaW5nKCksXG4gIG93bmVyOiB6LnN0cmluZygpLFxuICByZXBvOiB6LnN0cmluZygpLFxuICBpc3N1ZV9udW1iZXI6IHoubnVtYmVyKCksXG4gIHRpdGxlOiB6LnN0cmluZygpLFxuICBzdGF0ZTogei5zdHJpbmcoKSxcbiAgYXV0aG9yOiB6LnN0cmluZygpLFxuICBhdXRob3JfaWQ6IHoubnVtYmVyKCksXG4gIGJvZHk6IHouc3RyaW5nKCksXG4gIGRhdGVfY3JlYXRlZDogei5zdHJpbmcoKSxcbiAgZGF0ZV9sYXN0X21vZGlmaWVkOiB6LnN0cmluZygpXG59KTtcbmNvbnN0IGFjdGlvbiA9IHtcbiAgdHlwZTogXCJhY3Rpb25cIixcbiAgZGVzY3JpcHRpb246IGBDcmVhdGUgYW4gaXNzdWUgaW4gR2l0SHViYCxcbiAgdmVyc2lvbjogJzEuMC4wJyxcbiAgZW5kcG9pbnQ6IHtcbiAgICBtZXRob2Q6ICdQT1NUJyxcbiAgICBwYXRoOiAnL2V4YW1wbGUvZ2l0aHViL2lzc3VlcycsXG4gICAgZ3JvdXA6ICdJc3N1ZXMnXG4gIH0sXG4gIGlucHV0OiBpc3N1ZVNjaGVtYSxcbiAgb3V0cHV0OiB6LnZvaWQoKSxcbiAgLy8gQWN0aW9uIGV4ZWN1dGlvblxuICBleGVjOiBhc3luYyAobmFuZ28sIGlucHV0KSA9PiB7XG4gICAgYXdhaXQgbmFuZ28ucHJveHkoe1xuICAgICAgZW5kcG9pbnQ6ICcvcmVwb3MvTmFuZ29IUS9pbnRlcmFjdGl2ZS1kZW1vL2lzc3VlcycsXG4gICAgICBkYXRhOiB7XG4gICAgICAgIHRpdGxlOiBgW2RlbW9dICR7aW5wdXQudGl0bGV9YCxcbiAgICAgICAgYm9keTogYFRoaXMgaXNzdWUgd2FzIGNyZWF0ZWQgYXV0b21hdGljYWxseSB1c2luZyBOYW5nbyBBY3Rpb24uYCxcbiAgICAgICAgbGFiZWxzOiBbJ2F1dG9tYXRpYyddXG4gICAgICB9XG4gICAgfSk7XG4gIH1cbn07XG5leHBvcnQgdHlwZSBOYW5nb0FjdGlvbkxvY2FsID0gUGFyYW1ldGVyczwodHlwZW9mIGFjdGlvbilbJ2V4ZWMnXT5bMF07XG5leHBvcnQgZGVmYXVsdCBhY3Rpb247Il0sCiAgIm1hcHBpbmdzIjogIjs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7O0FBQUE7QUFBQTtBQUFBO0FBQUE7QUFBQTtBQUNBLFFBQW1CO0FBQ25CLElBQU0sY0FBZ0IsU0FBTztBQUFBLEVBQzNCLElBQU0sU0FBTztBQUFBLEVBQ2IsT0FBUyxTQUFPO0FBQUEsRUFDaEIsTUFBUSxTQUFPO0FBQUEsRUFDZixjQUFnQixTQUFPO0FBQUEsRUFDdkIsT0FBUyxTQUFPO0FBQUEsRUFDaEIsT0FBUyxTQUFPO0FBQUEsRUFDaEIsUUFBVSxTQUFPO0FBQUEsRUFDakIsV0FBYSxTQUFPO0FBQUEsRUFDcEIsTUFBUSxTQUFPO0FBQUEsRUFDZixjQUFnQixTQUFPO0FBQUEsRUFDdkIsb0JBQXNCLFNBQU87QUFDL0IsQ0FBQztBQUNELElBQU0sU0FBUztBQUFBLEVBQ2IsTUFBTTtBQUFBLEVBQ04sYUFBYTtBQUFBLEVBQ2IsU0FBUztBQUFBLEVBQ1QsVUFBVTtBQUFBLElBQ1IsUUFBUTtBQUFBLElBQ1IsTUFBTTtBQUFBLElBQ04sT0FBTztBQUFBLEVBQ1Q7QUFBQSxFQUNBLE9BQU87QUFBQSxFQUNQLFFBQVUsT0FBSztBQUFBO0FBQUEsRUFFZixNQUFNLE9BQU8sT0FBTyxVQUFVO0FBQzVCLFVBQU0sTUFBTSxNQUFNO0FBQUEsTUFDaEIsVUFBVTtBQUFBLE1BQ1YsTUFBTTtBQUFBLFFBQ0osT0FBTyxVQUFVLE1BQU0sS0FBSztBQUFBLFFBQzVCLE1BQU07QUFBQSxRQUNOLFFBQVEsQ0FBQyxXQUFXO0FBQUEsTUFDdEI7QUFBQSxJQUNGLENBQUM7QUFBQSxFQUNIO0FBQ0Y7QUFFQSxJQUFPLHNCQUFROyIsCiAgIm5hbWVzIjogW10KfQo=
