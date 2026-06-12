import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient, ReceptionistTone } from "@/shared/lib/api/client";

import { settingsQueryKey, type ReceptionistSettings } from "./receptionistHelpers";

export function ReceptionistFineTuneDialog({
  isOpen,
  settings,
  onOpenChange
}: Readonly<{
  isOpen: boolean;
  settings: ReceptionistSettings;
  onOpenChange: (isOpen: boolean) => void;
}>) {
  const [tone, setTone] = useState(settings.tone);
  const [languages, setLanguages] = useState(settings.languages.join(", "));
  const [faqNotes, setFaqNotes] = useState(settings.faqNotes ?? "");
  const [ownerPhoneNumber, setOwnerPhoneNumber] = useState(settings.ownerPhoneNumber ?? "");
  const updateSettingsMutation = api.useMutation("put", "/api/main/receptionist/settings", {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: settingsQueryKey })
  });

  const handleSave = () => {
    updateSettingsMutation.mutate(
      {
        body: {
          isEnabled: settings.isEnabled,
          tone,
          languages: languages
            .split(",")
            .map((language) => language.trim())
            .filter(Boolean),
          faqNotes: faqNotes.trim() || null,
          ownerPhoneNumber: ownerPhoneNumber.trim() || null
        }
      },
      {
        onSuccess: () => {
          toast.success(t`Receptionist updated`, {
            description: t`We will use these notes in new WhatsApp conversations.`
          });
          onOpenChange(false);
        }
      }
    );
  };

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Fine-tune AI receptionist">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Fine-tune your AI receptionist</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Tell us how to sound and when to bring you into a conversation.</Trans>
          </DialogDescription>
        </DialogHeader>
        <DialogBody>
          <SelectField<ReceptionistTone>
            label={t`Tone`}
            name="tone"
            value={tone}
            onValueChange={(value) => value && setTone(value)}
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ReceptionistTone.Professional}>
                <Trans>Professional</Trans>
              </SelectItem>
              <SelectItem value={ReceptionistTone.Friendly}>
                <Trans>Friendly</Trans>
              </SelectItem>
              <SelectItem value={ReceptionistTone.Playful}>
                <Trans>Playful</Trans>
              </SelectItem>
            </SelectContent>
          </SelectField>
          <TextField
            name="languages"
            label={t`Languages`}
            value={languages}
            onChange={setLanguages}
            description={t`Separate languages with commas.`}
          />
          <TextAreaField
            name="faqNotes"
            label={t`Business notes`}
            value={faqNotes}
            onChange={setFaqNotes}
            lines={5}
            resizable={true}
          />
          <TextField
            name="ownerPhoneNumber"
            label={t`Owner WhatsApp number`}
            value={ownerPhoneNumber}
            onChange={setOwnerPhoneNumber}
          />
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={updateSettingsMutation.isPending}>
            <Trans>Cancel</Trans>
          </Button>
          <Button onClick={handleSave} isPending={updateSettingsMutation.isPending}>
            <Trans>Save changes</Trans>
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
