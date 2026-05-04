import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { ExternalLinkIcon } from "lucide-react";
import { useState } from "react";

const TEMPLATES = ["StartSignup", "StartLogin", "ResendEmailLogin", "UnknownUser", "InviteUser"] as const;
const LOCALES = ["en-US", "da-DK"] as const;

type Template = (typeof TEMPLATES)[number];
type Locale = (typeof LOCALES)[number];

export function EmailsPreview() {
  const [template, setTemplate] = useState<Template>("StartSignup");
  const [locale, setLocale] = useState<Locale>("en-US");

  const iframeSrc = `/emails/assets/${template}.${locale}.preview.html`;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <h4>
          <Trans>Template</Trans>
        </h4>
        <div className="flex flex-wrap gap-2">
          {TEMPLATES.map((name) => (
            <Button
              key={name}
              variant={template === name ? "default" : "outline"}
              size="sm"
              onClick={() => setTemplate(name)}
            >
              {name}
            </Button>
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <h4>
          <Trans>Locale</Trans>
        </h4>
        <div className="flex flex-wrap gap-2">
          {LOCALES.map((code) => (
            <Button
              key={code}
              variant={locale === code ? "default" : "outline"}
              size="sm"
              onClick={() => setLocale(code)}
            >
              {code}
            </Button>
          ))}
        </div>
      </div>

      <div className="overflow-hidden rounded-md border border-border">
        <iframe
          key={iframeSrc}
          src={iframeSrc}
          title={`${template} (${locale})`}
          className="block h-[40rem] w-full border-0 bg-white"
        />
      </div>

      <p className="text-sm text-muted-foreground">
        <Trans>
          Need a new building block? Browse the MIT-licensed recipes at{" "}
          <a
            href="https://react.email/components"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1 underline"
          >
            react.email/components
            <ExternalLinkIcon className="size-[0.875rem]" />
          </a>{" "}
          and copy the source into <code>application/shared-webapp/emails/components/</code>.
        </Trans>
      </p>
    </div>
  );
}
