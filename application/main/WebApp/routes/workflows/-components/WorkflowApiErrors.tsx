import type { ApiValidationError } from "./workflowTypes";

import { getApiErrorMessages } from "./workflowTypes";

export function WorkflowApiErrors({ error }: Readonly<{ error: ApiValidationError }>) {
  const messages = getApiErrorMessages(error);
  if (messages.length === 0) return null;

  return (
    <div className="rounded-md border border-destructive/40 bg-destructive/5 px-3 py-2 text-sm text-destructive">
      {messages.map((message) => (
        <p key={message}>{message}</p>
      ))}
    </div>
  );
}
