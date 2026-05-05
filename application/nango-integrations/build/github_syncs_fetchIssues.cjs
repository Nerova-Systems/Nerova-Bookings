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

// nango-integrations/github/syncs/fetchIssues.ts
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
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsibmFuZ28taW50ZWdyYXRpb25zL2dpdGh1Yi9zeW5jcy9mZXRjaElzc3Vlcy50cyJdLAogICJzb3VyY2VzQ29udGVudCI6IFsiaW1wb3J0IHsgY3JlYXRlU3luYyB9IGZyb20gJ25hbmdvJztcbmltcG9ydCAqIGFzIHogZnJvbSAnem9kJztcbmNvbnN0IExJTUlUID0gMTAwO1xuY29uc3QgaXNzdWVTY2hlbWEgPSB6Lm9iamVjdCh7XG4gIGlkOiB6LnN0cmluZygpLFxuICBvd25lcjogei5zdHJpbmcoKSxcbiAgcmVwbzogei5zdHJpbmcoKSxcbiAgaXNzdWVfbnVtYmVyOiB6Lm51bWJlcigpLFxuICB0aXRsZTogei5zdHJpbmcoKSxcbiAgc3RhdGU6IHouc3RyaW5nKCksXG4gIGF1dGhvcjogei5zdHJpbmcoKSxcbiAgYXV0aG9yX2lkOiB6Lm51bWJlcigpLFxuICBib2R5OiB6LnN0cmluZygpLFxuICBkYXRlX2NyZWF0ZWQ6IHouc3RyaW5nKCksXG4gIGRhdGVfbGFzdF9tb2RpZmllZDogei5zdHJpbmcoKVxufSk7XG50eXBlIEdpdGh1Yklzc3VlID0gei5pbmZlcjx0eXBlb2YgaXNzdWVTY2hlbWE+O1xuY29uc3Qgc3luYyA9IHtcbiAgdHlwZTogXCJzeW5jXCIsXG4gIGRlc2NyaXB0aW9uOiBgRmV0Y2hlcyB0aGUgR2l0aHViIGlzc3VlcyBmcm9tIGFsbCBhIHVzZXIncyByZXBvc2l0b3JpZXMuYCxcbiAgdmVyc2lvbjogJzEuMC4wJyxcbiAgZW5kcG9pbnRzOiBbe1xuICAgIG1ldGhvZDogJ0dFVCcsXG4gICAgcGF0aDogJy9leGFtcGxlL2dpdGh1Yi9pc3N1ZXMnLFxuICAgIGdyb3VwOiAnSXNzdWVzJ1xuICB9XSxcbiAgZnJlcXVlbmN5OiAnZXZlcnkgaG91cicsXG4gIGF1dG9TdGFydDogdHJ1ZSxcbiAgc3luY1R5cGU6ICdmdWxsJyxcbiAgbWV0YWRhdGE6IHoudm9pZCgpLFxuICBtb2RlbHM6IHtcbiAgICBHaXRodWJJc3N1ZTogaXNzdWVTY2hlbWFcbiAgfSxcbiAgLy8gU3luYyBleGVjdXRpb25cbiAgZXhlYzogYXN5bmMgbmFuZ28gPT4ge1xuICAgIGF3YWl0IG5hbmdvLnRyYWNrRGVsZXRlc1N0YXJ0KCdHaXRodWJJc3N1ZScpO1xuICAgIGNvbnN0IHJlcG9zID0gYXdhaXQgZ2V0QWxsUmVwb3NpdG9yaWVzKG5hbmdvKTtcbiAgICBmb3IgKGNvbnN0IHJlcG8gb2YgcmVwb3MpIHtcbiAgICAgIGNvbnN0IHByb3h5Q29uZmlnID0ge1xuICAgICAgICBlbmRwb2ludDogYC9yZXBvcy8ke3JlcG8ub3duZXIubG9naW59LyR7cmVwby5uYW1lfS9pc3N1ZXNgLFxuICAgICAgICBwYWdpbmF0ZToge1xuICAgICAgICAgIGxpbWl0OiBMSU1JVFxuICAgICAgICB9XG4gICAgICB9O1xuICAgICAgZm9yIGF3YWl0IChjb25zdCBpc3N1ZUJhdGNoIG9mIG5hbmdvLnBhZ2luYXRlKHByb3h5Q29uZmlnKSkge1xuICAgICAgICBjb25zdCBpc3N1ZXMgPSBpc3N1ZUJhdGNoLmZpbHRlcihpc3N1ZSA9PiAhKCdwdWxsX3JlcXVlc3QnIGluIGlzc3VlKSk7XG4gICAgICAgIGNvbnN0IG1hcHBlZElzc3VlczogR2l0aHViSXNzdWVbXSA9IGlzc3Vlcy5tYXAoaXNzdWUgPT4gKHtcbiAgICAgICAgICBpZDogaXNzdWUuaWQsXG4gICAgICAgICAgb3duZXI6IHJlcG8ub3duZXIubG9naW4sXG4gICAgICAgICAgcmVwbzogcmVwby5uYW1lLFxuICAgICAgICAgIGlzc3VlX251bWJlcjogaXNzdWUubnVtYmVyLFxuICAgICAgICAgIHRpdGxlOiBpc3N1ZS50aXRsZSxcbiAgICAgICAgICBzdGF0ZTogaXNzdWUuc3RhdGUsXG4gICAgICAgICAgYXV0aG9yOiBpc3N1ZS51c2VyLmxvZ2luLFxuICAgICAgICAgIGF1dGhvcl9pZDogaXNzdWUudXNlci5pZCxcbiAgICAgICAgICBib2R5OiBpc3N1ZS5ib2R5LFxuICAgICAgICAgIGRhdGVfY3JlYXRlZDogaXNzdWUuY3JlYXRlZF9hdCxcbiAgICAgICAgICBkYXRlX2xhc3RfbW9kaWZpZWQ6IGlzc3VlLnVwZGF0ZWRfYXRcbiAgICAgICAgfSkpO1xuICAgICAgICBpZiAobWFwcGVkSXNzdWVzLmxlbmd0aCA+IDApIHtcbiAgICAgICAgICBhd2FpdCBuYW5nby5iYXRjaFNhdmUobWFwcGVkSXNzdWVzLCAnR2l0aHViSXNzdWUnKTtcbiAgICAgICAgICBhd2FpdCBuYW5nby5sb2coYFNlbnQgJHttYXBwZWRJc3N1ZXMubGVuZ3RofSBpc3N1ZXMgZnJvbSAke3JlcG8ub3duZXIubG9naW59LyR7cmVwby5uYW1lfWApO1xuICAgICAgICB9XG4gICAgICB9XG4gICAgfVxuICAgIGF3YWl0IG5hbmdvLnRyYWNrRGVsZXRlc0VuZCgnR2l0aHViSXNzdWUnKTtcbiAgfSxcbiAgLy8gV2ViaG9vayBoYW5kbGVyXG4gIG9uV2ViaG9vazogYXN5bmMgKG5hbmdvLCBwYXlsb2FkKSA9PiB7XG4gICAgYXdhaXQgbmFuZ28ubG9nKCdUaGlzIGlzIGEgd2ViaG9vayBzY3JpcHQnLCBwYXlsb2FkKTtcbiAgfVxufTtcbmV4cG9ydCB0eXBlIE5hbmdvU3luY0xvY2FsID0gUGFyYW1ldGVyczwodHlwZW9mIHN5bmMpWydleGVjJ10+WzBdO1xuZXhwb3J0IGRlZmF1bHQgc3luYztcbmFzeW5jIGZ1bmN0aW9uIGdldEFsbFJlcG9zaXRvcmllcyhuYW5nbzogTmFuZ29TeW5jTG9jYWwpOiBQcm9taXNlPGFueVtdPiB7XG4gIGNvbnN0IHJlY29yZHM6IGFueVtdID0gW107XG4gIGNvbnN0IHByb3h5Q29uZmlnID0ge1xuICAgIGVuZHBvaW50OiAnL3VzZXIvcmVwb3MnLFxuICAgIHBhZ2luYXRlOiB7XG4gICAgICBsaW1pdDogTElNSVRcbiAgICB9XG4gIH07XG4gIGZvciBhd2FpdCAoY29uc3QgcmVjb3JkQmF0Y2ggb2YgbmFuZ28ucGFnaW5hdGUocHJveHlDb25maWcpKSB7XG4gICAgcmVjb3Jkcy5wdXNoKC4uLnJlY29yZEJhdGNo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
