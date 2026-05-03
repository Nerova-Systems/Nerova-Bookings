// @jsxRuntime automatic
import { Trans } from "@lingui/react/macro";
import { Section, Text } from "@react-email/components";
import { Button } from "@repo/emails/components/Button";
import { Heading } from "@repo/emails/components/Heading";
import { Subject } from "@repo/emails/components/Subject";
import { TransactionalEmail } from "@repo/emails/components/TransactionalEmail";
import { Value } from "@repo/emails/helpers/Value";

type InviteUserProps = {
  locale: string;
};

export default function InviteUser({ locale }: InviteUserProps) {
  return (
    <TransactionalEmail locale={locale} preview="You have been invited to PlatformPlatform">
      <Subject>
        <Trans>You have been invited to PlatformPlatform</Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>
          <Value path="InviterName" sample="Alex Taylor" /> invited you to join PlatformPlatform.
        </Trans>
      </Heading>

      <Text className="m-0 mb-[1.5rem] text-center text-[0.875rem] leading-[1.5rem]">
        <Trans>
          To gain access, click the button below and login using <Value path="Email" sample="invitee@example.com" />.
        </Trans>
      </Text>

      <Section className="text-center">
        <Button href="{{LoginUrl}}">
          <Trans>Open the login page</Trans>
        </Button>
      </Section>
    </TransactionalEmail>
  );
}
