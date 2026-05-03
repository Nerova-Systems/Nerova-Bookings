// @jsxRuntime automatic
import { Trans } from "@lingui/react/macro";
import { Section, Text } from "@react-email/components";
import { Heading } from "@repo/emails/components/Heading";
import { Subject } from "@repo/emails/components/Subject";
import { TransactionalEmail } from "@repo/emails/components/TransactionalEmail";
import { OtpAutofill } from "@repo/emails/helpers/OtpAutofill";
import { Value } from "@repo/emails/helpers/Value";

type ResendEmailLoginProps = {
  locale: string;
};

export default function ResendEmailLogin({ locale }: ResendEmailLoginProps) {
  return (
    <TransactionalEmail locale={locale} preview="Your verification code (resend)">
      <Subject>
        <Trans>Your verification code (resend)</Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>Here's your new verification code</Trans>
      </Heading>

      <Text className="m-0 mb-[1rem] text-center text-[0.875rem] leading-[1.5rem]">
        <Trans>We're sending this code again as you requested.</Trans>
      </Text>

      <Section className="my-[1rem] rounded-[0.5rem] bg-[#f1f5f9] p-[1rem] text-center">
        <Text className="m-0 font-mono text-[2rem] tracking-[0.5rem] text-[#0f172a]">
          <Value path="OneTimePassword" sample="ABC123" />
        </Text>
      </Section>

      <Text className="m-0 text-center text-[0.75rem] leading-[1.25rem] text-[#64748b]">
        <Trans>This code will expire in a few minutes.</Trans>
      </Text>

      <OtpAutofill code="OneTimePassword" domain="Domain" />
    </TransactionalEmail>
  );
}
