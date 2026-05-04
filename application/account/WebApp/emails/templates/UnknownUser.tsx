// @jsxRuntime automatic
import { Trans } from "@lingui/react/macro";
import { Link, Text } from "@react-email/components";
import { Heading } from "@repo/emails/components/Heading";
import { Subject } from "@repo/emails/components/Subject";
import { TransactionalEmail } from "@repo/emails/components/TransactionalEmail";
import { Value } from "@repo/emails/helpers/Value";

type UnknownUserProps = {
  locale: string;
};

export default function UnknownUser({ locale }: Readonly<UnknownUserProps>) {
  return (
    <TransactionalEmail locale={locale} preview="Unknown user tried to login to PlatformPlatform">
      <Subject>
        <Trans>Unknown user tried to login to PlatformPlatform</Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>You or someone else tried to login to PlatformPlatform</Trans>
      </Heading>

      <Text className="m-[0px] mb-[16px] text-center text-[14px] leading-[24px]">
        <Trans>
          This request was made by entering your mail <Value path="Email" sample="alex@example.com" />, but we have no
          record of such user.
        </Trans>
      </Text>

      <Text className="m-[0px] text-center text-[14px] leading-[24px]">
        <Trans>
          You can sign up for an account on{" "}
          <Link href="{{SignupUrl}}" className="email-link text-[#0f172a] underline">
            {`'{{'SignupUrl'}}'`}
          </Link>
          .
        </Trans>
      </Text>
    </TransactionalEmail>
  );
}
