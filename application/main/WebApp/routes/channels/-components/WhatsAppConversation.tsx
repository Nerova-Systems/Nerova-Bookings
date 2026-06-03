import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Form } from "@repo/ui/components/Form";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { MessageCircleIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import type { Schemas } from "@/shared/lib/api/client";

import { api, queryClient } from "@/shared/lib/api/client";

const MESSAGES_QUERY_KEY = ["get", "/api/main/whatsapp/messages"] as const;
const MAX_MESSAGE_LENGTH = 4096;

type WhatsAppMessage = Schemas["WhatsAppMessageItem"];

function MessageBubble({
  message,
  formatDate
}: Readonly<{ message: WhatsAppMessage; formatDate: (input: string, includeTime: boolean) => string }>) {
  const isOutbound = (message.direction as string) === "Outbound";

  return (
    <div className={`flex flex-col gap-1 ${isOutbound ? "items-end" : "items-start"}`}>
      <div
        className={`max-w-[80%] rounded-lg px-3 py-2 text-sm ${
          isOutbound ? "bg-primary text-primary-foreground" : "bg-muted text-foreground"
        }`}
      >
        {message.text}
      </div>
      <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
        <span>{isOutbound ? message.to : message.from}</span>
        {isOutbound && message.status && (
          <>
            <span aria-hidden="true">·</span>
            <span>{message.status}</span>
          </>
        )}
        <span aria-hidden="true">·</span>
        <time dateTime={message.timestamp}>{formatDate(message.timestamp, true)}</time>
      </div>
    </div>
  );
}

function MessagesList() {
  const messagesQuery = api.useQuery("get", "/api/main/whatsapp/messages");
  const formatDate = useFormatDate();

  if (messagesQuery.isLoading) {
    return (
      <div className="flex flex-col gap-3">
        <Skeleton className="h-12 w-3/4 self-end" />
        <Skeleton className="h-12 w-3/4 self-start" />
        <Skeleton className="h-10 w-2/3 self-end" />
      </div>
    );
  }

  if (messagesQuery.isError) {
    return (
      <p className="text-sm text-destructive">
        <Trans>Failed to load messages.</Trans>
      </p>
    );
  }

  const messages = messagesQuery.data?.messages ?? [];

  if (messages.length === 0) {
    return (
      <Empty>
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <MessageCircleIcon />
          </EmptyMedia>
          <EmptyTitle>
            <Trans>No messages yet</Trans>
          </EmptyTitle>
          <EmptyDescription>
            <Trans>Send your first message using the form below.</Trans>
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }

  // API returns newest-first; reverse for chronological chat display (oldest at top).
  const chronological = [...messages].reverse();

  return (
    <div className="flex flex-col gap-4">
      {chronological.map((message) => (
        <MessageBubble key={message.id} message={message} formatDate={formatDate} />
      ))}
    </div>
  );
}

function SendMessageForm() {
  const [messageText, setMessageText] = useState("");

  const sendMutation = api.useMutation("post", "/api/main/whatsapp/messages", {
    onSuccess: () => {
      toast.success(t`Message sent`, {
        description: t`Your message has been sent.`
      });
      queryClient.invalidateQueries({ queryKey: MESSAGES_QUERY_KEY });
      setMessageText("");
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Send a message</Trans>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Form
          onSubmit={mutationSubmitter(sendMutation)}
          validationErrors={sendMutation.error?.errors}
          validationBehavior="aria"
        >
          <TextField name="to" label={t`Recipient`} type="tel" required disabled={sendMutation.isPending} />
          <TextAreaField
            name="text"
            label={t`Message`}
            required
            maxLength={MAX_MESSAGE_LENGTH}
            value={messageText}
            onChange={setMessageText}
            disabled={sendMutation.isPending}
          />
          <div className="flex justify-end">
            <Button type="submit" isPending={sendMutation.isPending}>
              {sendMutation.isPending ? <Trans>Sending...</Trans> : <Trans>Send message</Trans>}
            </Button>
          </div>
        </Form>
      </CardContent>
    </Card>
  );
}

export function WhatsAppConversation() {
  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle>
            <Trans>Messages</Trans>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <MessagesList />
        </CardContent>
      </Card>
      <SendMessageForm />
    </div>
  );
}
