import { api, type Schemas } from "@/shared/lib/api/client";

const awaitingApprovalStatus = "AwaitingApproval" as Schemas["JobRunStatus"];
const completedStatus = "Completed" as Schemas["JobRunStatus"];

export const receptionistSettingsQueryKey = ["get", "/api/main/receptionist/settings"] as const;
export const receptionistEscalationsQueryKey = ["get", "/api/main/receptionist/escalations"] as const;
export const awaitingJobRunsQueryKey = [
  "get",
  "/api/main/autonomy/job-runs",
  { params: { query: { Status: awaitingApprovalStatus } } }
] as const;
export const completedJobRunsQueryKey = [
  "get",
  "/api/main/autonomy/job-runs",
  { params: { query: { Status: completedStatus } } }
] as const;
export const receptionistPoliciesQueryKey = ["get", "/api/main/autonomy/policies"] as const;

export function useReceptionistSettingsQuery() {
  return api.useQuery("get", "/api/main/receptionist/settings");
}

export function useOpenReceptionistEscalationsQuery() {
  return api.useQuery("get", "/api/main/receptionist/escalations", {
    params: { query: { OpenOnly: true } }
  });
}

export function useAwaitingReceptionistJobRunsQuery(limit = 5) {
  return api.useQuery("get", "/api/main/autonomy/job-runs", {
    params: { query: { Status: awaitingApprovalStatus, Limit: limit } }
  });
}

export function useCompletedReceptionistJobRunsQuery(limit = 8) {
  return api.useQuery("get", "/api/main/autonomy/job-runs", {
    params: { query: { Status: completedStatus, Limit: limit } }
  });
}
