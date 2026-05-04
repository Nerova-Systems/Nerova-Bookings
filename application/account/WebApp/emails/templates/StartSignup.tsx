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

export default function StartSignup({ locale }: Readonly<StartSignupProps>) {
  return (
    <TransactionalEmail locale={locale} preview="Confirm your email address">
      <Subject>
        <Trans>Confirm your email address</Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>Your confirmation code is below</Trans>
      </Heading>

      <Text className="m-[0px] mb-[16px] text-center text-[14px] leading-[24px]">
        <Trans>Enter it in your open browser window. It is only valid for a few minutes.</Trans>
      </Text>

      <Section className="email-otp-box my-[16px] rounded-[8px] bg-[#f1f5f9] p-[16px] text-center">
        <Text className="email-otp-text m-[0px] text-center font-mono text-[32px] tracking-[8px] text-[#0f172a]">
          <Value path="OneTimePassword" sample="ABC123" />
        </Text>
      </Section>

      <OtpAutofill code="OneTimePassword" domain="Domain" />
    </TransactionalEmail>
  );
}
