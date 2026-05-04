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

export default function ResendEmailLogin({ locale }: Readonly<ResendEmailLoginProps>) {
  return (
    <TransactionalEmail
      locale={locale}
      preview="Your verification code (resend)"
      otpAutofill={<OtpAutofill code="OneTimePassword" domain="Domain" />}
    >
      <Subject>
        <Trans>Your verification code (resend)</Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>Here's your new verification code</Trans>
      </Heading>

      <Text className="m-[0px] mb-[16px] text-center text-[14px] leading-[24px]">
        <Trans>We're sending this code again as you requested.</Trans>
      </Text>

      <Section className="email-otp-box my-[16px] rounded-[8px] bg-[#f1f5f9] p-[16px] text-center">
        <Text className="email-otp-text m-[0px] text-center font-mono text-[32px] tracking-[8px] text-[#0f172a]">
          <Value path="OneTimePassword" sample="ABC123" />
        </Text>
      </Section>

      <Text className="email-muted m-[0px] text-center text-[13px] leading-[20px] text-[#64748b]">
        <Trans>This code will expire in a few minutes.</Trans>
      </Text>

      <Text className="email-muted m-[0px] mt-[16px] text-center text-[13px] leading-[20px] text-[#64748b]">
        <Trans>If you didn't request a new code, you can safely ignore this email.</Trans>
      </Text>
    </TransactionalEmail>
  );
}
