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

// github/syncs/fetchIssues.ts
var fetchIssues_exports = {};
__export(fetchIssues_exports, {
  default: () => fetchIssues_default
});
module.exports = __toCommonJS(fetchIssues_exports);
var z = __toESM(require("zod"), 1);
var LIMIT = 100;
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
var sync = {
  type: "sync",
  description: `Fetches the Github issues from all a user's repositories.`,
  version: "1.0.0",
  endpoints: [{
    method: "GET",
    path: "/example/github/issues",
    group: "Issues"
  }],
  frequency: "every hour",
  autoStart: true,
  syncType: "full",
  metadata: z.void(),
  models: {
    GithubIssue: issueSchema
  },
  // Sync execution
  exec: async (nango) => {
    await nango.trackDeletesStart("GithubIssue");
    const repos = await getAllRepositories(nango);
    for (const repo of repos) {
      const proxyConfig = {
        endpoint: `/repos/${repo.owner.login}/${repo.name}/issues`,
        paginate: {
          limit: LIMIT
        }
      };
      for await (const issueBatch of nango.paginate(proxyConfig)) {
        const issues = issueBatch.filter((issue) => !("pull_request" in issue));
        const mappedIssues = issues.map((issue) => ({
          id: issue.id,
          owner: repo.owner.login,
          repo: repo.name,
          issue_number: issue.number,
          title: issue.title,
          state: issue.state,
          author: issue.user.login,
          author_id: issue.user.id,
          body: issue.body,
          date_created: issue.created_at,
          date_last_modified: issue.updated_at
        }));
        if (mappedIssues.length > 0) {
          await nango.batchSave(mappedIssues, "GithubIssue");
          await nango.log(`Sent ${mappedIssues.length} issues from ${repo.owner.login}/${repo.name}`);
        }
      }
    }
    await nango.trackDeletesEnd("GithubIssue");
  },
  // Webhook handler
  onWebhook: async (nango, payload) => {
    await nango.log("This is a webhook script", payload);
  }
};
var fetchIssues_default = sync;
async function getAllRepositories(nango) {
  const records = [];
  const proxyConfig = {
    endpoint: "/user/repos",
    paginate: {
      limit: LIMIT
    }
  };
  for await (const recordBatch of nango.paginate(proxyConfig)) {
    records.push(...recordBatch);
  }
  return records;
}
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsiZ2l0aHViL3N5bmNzL2ZldGNoSXNzdWVzLnRzIl0sCiAgInNvdXJjZXNDb250ZW50IjogWyJpbXBvcnQgeyBjcmVhdGVTeW5jIH0gZnJvbSAnbmFuZ28nO1xuaW1wb3J0ICogYXMgeiBmcm9tICd6b2QnO1xuY29uc3QgTElNSVQgPSAxMDA7XG5jb25zdCBpc3N1ZVNjaGVtYSA9IHoub2JqZWN0KHtcbiAgaWQ6IHouc3RyaW5nKCksXG4gIG93bmVyOiB6LnN0cmluZygpLFxuICByZXBvOiB6LnN0cmluZygpLFxuICBpc3N1ZV9udW1iZXI6IHoubnVtYmVyKCksXG4gIHRpdGxlOiB6LnN0cmluZygpLFxuICBzdGF0ZTogei5zdHJpbmcoKSxcbiAgYXV0aG9yOiB6LnN0cmluZygpLFxuICBhdXRob3JfaWQ6IHoubnVtYmVyKCksXG4gIGJvZHk6IHouc3RyaW5nKCksXG4gIGRhdGVfY3JlYXRlZDogei5zdHJpbmcoKSxcbiAgZGF0ZV9sYXN0X21vZGlmaWVkOiB6LnN0cmluZygpXG59KTtcbnR5cGUgR2l0aHViSXNzdWUgPSB6LmluZmVyPHR5cGVvZiBpc3N1ZVNjaGVtYT47XG5jb25zdCBzeW5jID0ge1xuICB0eXBlOiBcInN5bmNcIixcbiAgZGVzY3JpcHRpb246IGBGZXRjaGVzIHRoZSBHaXRodWIgaXNzdWVzIGZyb20gYWxsIGEgdXNlcidzIHJlcG9zaXRvcmllcy5gLFxuICB2ZXJzaW9uOiAnMS4wLjAnLFxuICBlbmRwb2ludHM6IFt7XG4gICAgbWV0aG9kOiAnR0VUJyxcbiAgICBwYXRoOiAnL2V4YW1wbGUvZ2l0aHViL2lzc3VlcycsXG4gICAgZ3JvdXA6ICdJc3N1ZXMnXG4gIH1dLFxuICBmcmVxdWVuY3k6ICdldmVyeSBob3VyJyxcbiAgYXV0b1N0YXJ0OiB0cnVlLFxuICBzeW5jVHlwZTogJ2Z1bGwnLFxuICBtZXRhZGF0YTogei52b2lkKCksXG4gIG1vZGVsczoge1xuICAgIEdpdGh1Yklzc3VlOiBpc3N1ZVNjaGVtYVxuICB9LFxuICAvLyBTeW5jIGV4ZWN1dGlvblxuICBleGVjOiBhc3luYyBuYW5nbyA9PiB7XG4gICAgYXdhaXQgbmFuZ28udHJhY2tEZWxldGVzU3RhcnQoJ0dpdGh1Yklzc3VlJyk7XG4gICAgY29uc3QgcmVwb3MgPSBhd2FpdCBnZXRBbGxSZXBvc2l0b3JpZXMobmFuZ28pO1xuICAgIGZvciAoY29uc3QgcmVwbyBvZiByZXBvcykge1xuICAgICAgY29uc3QgcHJveHlDb25maWcgPSB7XG4gICAgICAgIGVuZHBvaW50OiBgL3JlcG9zLyR7cmVwby5vd25lci5sb2dpbn0vJHtyZXBvLm5hbWV9L2lzc3Vlc2AsXG4gICAgICAgIHBhZ2luYXRlOiB7XG4gICAgICAgICAgbGltaXQ6IExJTUlUXG4gICAgICAgIH1cbiAgICAgIH07XG4gICAgICBmb3IgYXdhaXQgKGNvbnN0IGlzc3VlQmF0Y2ggb2YgbmFuZ28ucGFnaW5hdGUocHJveHlDb25maWcpKSB7XG4gICAgICAgIGNvbnN0IGlzc3VlcyA9IGlzc3VlQmF0Y2guZmlsdGVyKGlzc3VlID0+ICEoJ3B1bGxfcmVxdWVzdCcgaW4gaXNzdWUpKTtcbiAgICAgICAgY29uc3QgbWFwcGVkSXNzdWVzOiBHaXRodWJJc3N1ZVtdID0gaXNzdWVzLm1hcChpc3N1ZSA9PiAoe1xuICAgICAgICAgIGlkOiBpc3N1ZS5pZCxcbiAgICAgICAgICBvd25lcjogcmVwby5vd25lci5sb2dpbixcbiAgICAgICAgICByZXBvOiByZXBvLm5hbWUsXG4gICAgICAgICAgaXNzdWVfbnVtYmVyOiBpc3N1ZS5udW1iZXIsXG4gICAgICAgICAgdGl0bGU6IGlzc3VlLnRpdGxlLFxuICAgICAgICAgIHN0YXRlOiBpc3N1ZS5zdGF0ZSxcbiAgICAgICAgICBhdXRob3I6IGlzc3VlLnVzZXIubG9naW4sXG4gICAgICAgICAgYXV0aG9yX2lkOiBpc3N1ZS51c2VyLmlkLFxuICAgICAgICAgIGJvZHk6IGlzc3VlLmJvZHksXG4gICAgICAgICAgZGF0ZV9jcmVhdGVkOiBpc3N1ZS5jcmVhdGVkX2F0LFxuICAgICAgICAgIGRhdGVfbGFzdF9tb2RpZmllZDogaXNzdWUudXBkYXRlZF9hdFxuICAgICAgICB9KSk7XG4gICAgICAgIGlmIChtYXBwZWRJc3N1ZXMubGVuZ3RoID4gMCkge1xuICAgICAgICAgIGF3YWl0IG5hbmdvLmJhdGNoU2F2ZShtYXBwZWRJc3N1ZXMsICdHaXRodWJJc3N1ZScpO1xuICAgICAgICAgIGF3YWl0IG5hbmdvLmxvZyhgU2VudCAke21hcHBlZElzc3Vlcy5sZW5ndGh9IGlzc3VlcyBmcm9tICR7cmVwby5vd25lci5sb2dpbn0vJHtyZXBvLm5hbWV9YCk7XG4gICAgICAgIH1cbiAgICAgIH1cbiAgICB9XG4gICAgYXdhaXQgbmFuZ28udHJhY2tEZWxldGVzRW5kKCdHaXRodWJJc3N1ZScpO1xuICB9LFxuICAvLyBXZWJob29rIGhhbmRsZXJcbiAgb25XZWJob29rOiBhc3luYyAobmFuZ28sIHBheWxvYWQpID0+IHtcbiAgICBhd2FpdCBuYW5nby5sb2coJ1RoaXMgaXMgYSB3ZWJob29rIHNjcmlwdCcsIHBheWxvYWQpO1xuICB9XG59O1xuZXhwb3J0IHR5cGUgTmFuZ29TeW5jTG9jYWwgPSBQYXJhbWV0ZXJzPCh0eXBlb2Ygc3luYylbJ2V4ZWMnXT5bMF07XG5leHBvcnQgZGVmYXVsdCBzeW5jO1xuYXN5bmMgZnVuY3Rpb24gZ2V0QWxsUmVwb3NpdG9yaWVzKG5hbmdvOiBOYW5nb1N5bmNMb2NhbCk6IFByb21pc2U8YW55W10+IHtcbiAgY29uc3QgcmVjb3JkczogYW55W10gPSBbXTtcbiAgY29uc3QgcHJveHlDb25maWcgPSB7XG4gICAgZW5kcG9pbnQ6ICcvdXNlci9yZXBvcycsXG4gICAgcGFnaW5hdGU6IHtcbiAgICAgIGxpbWl0OiBMSU1JVFxuICAgIH1cbiAgfTtcbiAgZm9yIGF3YWl0IChjb25zdCByZWNvcmRCYXRjaCBvZiBuYW5nby5wYWdpbmF0ZShwcm94eUNvbmZpZykpIHtcbiAgICByZWNvcmRzLnB1c2goLi4ucmVjb3JkQmF0Y2gpO1xuICB9XG4gIHJldHVybiByZWNvcmRzO1xufSJdLAogICJtYXBwaW5ncyI6ICI7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7OztBQUFBO0FBQUE7QUFBQTtBQUFBO0FBQUE7QUFDQSxRQUFtQjtBQUNuQixJQUFNLFFBQVE7QUFDZCxJQUFNLGNBQWdCLFNBQU87QUFBQSxFQUMzQixJQUFNLFNBQU87QUFBQSxFQUNiLE9BQVMsU0FBTztBQUFBLEVBQ2hCLE1BQVEsU0FBTztBQUFBLEVBQ2YsY0FBZ0IsU0FBTztBQUFBLEVBQ3ZCLE9BQVMsU0FBTztBQUFBLEVBQ2hCLE9BQVMsU0FBTztBQUFBLEVBQ2hCLFFBQVUsU0FBTztBQUFBLEVBQ2pCLFdBQWEsU0FBTztBQUFBLEVBQ3BCLE1BQVEsU0FBTztBQUFBLEVBQ2YsY0FBZ0IsU0FBTztBQUFBLEVBQ3ZCLG9CQUFzQixTQUFPO0FBQy9CLENBQUM7QUFFRCxJQUFNLE9BQU87QUFBQSxFQUNYLE1BQU07QUFBQSxFQUNOLGFBQWE7QUFBQSxFQUNiLFNBQVM7QUFBQSxFQUNULFdBQVcsQ0FBQztBQUFBLElBQ1YsUUFBUTtBQUFBLElBQ1IsTUFBTTtBQUFBLElBQ04sT0FBTztBQUFBLEVBQ1QsQ0FBQztBQUFBLEVBQ0QsV0FBVztBQUFBLEVBQ1gsV0FBVztBQUFBLEVBQ1gsVUFBVTtBQUFBLEVBQ1YsVUFBWSxPQUFLO0FBQUEsRUFDakIsUUFBUTtBQUFBLElBQ04sYUFBYTtBQUFBLEVBQ2Y7QUFBQTtBQUFBLEVBRUEsTUFBTSxPQUFNLFVBQVM7QUFDbkIsVUFBTSxNQUFNLGtCQUFrQixhQUFhO0FBQzNDLFVBQU0sUUFBUSxNQUFNLG1CQUFtQixLQUFLO0FBQzVDLGVBQVcsUUFBUSxPQUFPO0FBQ3hCLFlBQU0sY0FBYztBQUFBLFFBQ2xCLFVBQVUsVUFBVSxLQUFLLE1BQU0sS0FBSyxJQUFJLEtBQUssSUFBSTtBQUFBLFFBQ2pELFVBQVU7QUFBQSxVQUNSLE9BQU87QUFBQSxRQUNUO0FBQUEsTUFDRjtBQUNBLHVCQUFpQixjQUFjLE1BQU0sU0FBUyxXQUFXLEdBQUc7QUFDMUQsY0FBTSxTQUFTLFdBQVcsT0FBTyxXQUFTLEVBQUUsa0JBQWtCLE1BQU07QUFDcEUsY0FBTSxlQUE4QixPQUFPLElBQUksWUFBVTtBQUFBLFVBQ3ZELElBQUksTUFBTTtBQUFBLFVBQ1YsT0FBTyxLQUFLLE1BQU07QUFBQSxVQUNsQixNQUFNLEtBQUs7QUFBQSxVQUNYLGNBQWMsTUFBTTtBQUFBLFVBQ3BCLE9BQU8sTUFBTTtBQUFBLFVBQ2IsT0FBTyxNQUFNO0FBQUEsVUFDYixRQUFRLE1BQU0sS0FBSztBQUFBLFVBQ25CLFdBQVcsTUFBTSxLQUFLO0FBQUEsVUFDdEIsTUFBTSxNQUFNO0FBQUEsVUFDWixjQUFjLE1BQU07QUFBQSxVQUNwQixvQkFBb0IsTUFBTTtBQUFBLFFBQzVCLEVBQUU7QUFDRixZQUFJLGFBQWEsU0FBUyxHQUFHO0FBQzNCLGdCQUFNLE1BQU0sVUFBVSxjQUFjLGFBQWE7QUFDakQsZ0JBQU0sTUFBTSxJQUFJLFFBQVEsYUFBYSxNQUFNLGdCQUFnQixLQUFLLE1BQU0sS0FBSyxJQUFJLEtBQUssSUFBSSxFQUFFO0FBQUEsUUFDNUY7QUFBQSxNQUNGO0FBQUEsSUFDRjtBQUNBLFVBQU0sTUFBTSxnQkFBZ0IsYUFBYTtBQUFBLEVBQzNDO0FBQUE7QUFBQSxFQUVBLFdBQVcsT0FBTyxPQUFPLFlBQVk7QUFDbkMsVUFBTSxNQUFNLElBQUksNEJBQTRCLE9BQU87QUFBQSxFQUNyRDtBQUNGO0FBRUEsSUFBTyxzQkFBUTtBQUNmLGVBQWUsbUJBQW1CLE9BQXVDO0FBQ3ZFLFFBQU0sVUFBaUIsQ0FBQztBQUN4QixRQUFNLGNBQWM7QUFBQSxJQUNsQixVQUFVO0FBQUEsSUFDVixVQUFVO0FBQUEsTUFDUixPQUFPO0FBQUEsSUFDVDtBQUFBLEVBQ0Y7QUFDQSxtQkFBaUIsZUFBZSxNQUFNLFNBQVMsV0FBVyxHQUFHO0FBQzNELFlBQVEsS0FBSyxHQUFHLFdBQVc7QUFBQSxFQUM3QjtBQUNBLFNBQU87QUFDVDsiLAogICJuYW1lcyI6IFtdCn0K
