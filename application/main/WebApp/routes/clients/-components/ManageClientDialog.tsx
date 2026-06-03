import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { TextField } from "@repo/ui/components/TextField";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, type components } from "@/shared/lib/api/client";

type ClientDetails = components["schemas"]["ClientDetails"];

interface ManageClientDialogProps {
  client: ClientDetails | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

export function ManageClientDialog({ client, isOpen, onOpenChange }: Readonly<ManageClientDialogProps>) {
  const queryClient = useQueryClient();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");

  const { error, isPending, mutate, reset } = api.useMutation("put", "/api/main/clients/{id}", {
    meta: { skipQueryInvalidation: true },
    onSuccess: async () => {
      toast.success(t`Client updated`);
      await queryClient.invalidateQueries({
        predicate: (query) => {
          const key = query.queryKey;
          return Array.isArray(key) && key[0] === "get" && key[1] === "/api/main/clients";
        }
      });
      onOpenChange(false);
    }
  });

  useEffect(() => {
    if (isOpen && client) {
      setFirstName(client.firstName ?? "");
      setLastName(client.lastName ?? "");
      setEmail(client.email ?? "");
      setPhoneNumber(client.phoneNumber ?? "");
      reset();
    }
  }, [isOpen, client, reset]);

  if (!client) {
    return null;
  }

  return (
    <Dialog trackingTitle="Manage client" open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={error?.errors}
          onSubmit={() => {
            mutate({
              params: { path: { id: client.id } },
              body: {
                id: client.id,
                firstName: firstName.trim(),
                lastName: lastName.trim(),
                email: email.trim() ? email.trim() : null,
                phoneNumber: phoneNumber.trim() ? phoneNumber.trim() : null
              }
            });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Manage client</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>Update this client's contact details.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="flex flex-col gap-4">
            <div className="flex gap-4">
              <TextField
                name="firstName"
                label={t`First name`}
                autoFocus={true}
                value={firstName}
                onChange={setFirstName}
                className="flex-1"
              />
              <TextField
                name="lastName"
                label={t`Last name`}
                value={lastName}
                onChange={setLastName}
                className="flex-1"
              />
            </div>
            <TextField name="email" type="email" label={t`Email`} value={email} onChange={setEmail} />
            <TextField name="phoneNumber" label={t`Phone`} value={phoneNumber} onChange={setPhoneNumber} />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" isPending={isPending}>
              <Trans>Save</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}
