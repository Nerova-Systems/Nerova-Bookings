// @jsxRuntime automatic
import { Trans } from "@lingui/react/macro";
import { Section, Text } from "@react-email/components";
import { Heading } from "@repo/emails/components/Heading";
import { Subject } from "@repo/emails/components/Subject";
import { TransactionalEmail } from "@repo/emails/components/TransactionalEmail";
import { OtpAutofill } from "@repo/emails/helpers/OtpAutofill";
import { Value } from "@repo/emails/helpers/Value";

type StartSignupProps = {
  locale: string;
};

export default function StartSignup({ locale }: StartSignupProps) {
  return (
    <TransactionalEmail locale={locale} preview="Confirm your email address">
      <Subject>
        <Trans>Confirm your email address</Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>Your confirmation code is below</Trans>
      </Heading>

      <Text className="m-0 mb-[1rem] text-center text-[0.875rem] leading-[1.5rem]">
        <Trans>Enter it in your open browser window. It is only valid for a few minutes.</Trans>
      </Text>

      <Section className="my-[1rem] rounded-[0.5rem] bg-[#f1f5f9] p-[1rem] text-center">
        <Text className="m-0 font-mono text-[2rem] tracking-[0.5rem] text-[#0f172a]">
          <Value path="OneTimePassword" sample="ABC123" />
        </Text>
      </Section>

      <OtpAutofill code="OneTimePassword" domain="Domain" />
    </TransactionalEmail>
  );
}
