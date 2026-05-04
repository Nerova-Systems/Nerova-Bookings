// @jsxRuntime automatic
import { Trans } from "@lingui/react/macro";
import { Link, Text } from "@react-email/components";
import { Heading } from "@repo/emails/components/Heading";
import { Subject } from "@repo/emails/components/Subject";
import { TransactionalEmail } from "@repo/emails/components/TransactionalEmail";
import { Value } from "@repo/emails/helpers/Value";

type InviteUserProps = {
  locale: string;
};

export default function InviteUser({ locale }: Readonly<InviteUserProps>) {
  return (
    <TransactionalEmail locale={locale} preview="You have been invited to PlatformPlatform">
      <Subject>
        <Trans>{`You have been invited to join '{{'TenantName'}}' on PlatformPlatform`}</Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>
          <Value path="InviterName" sample="Alex Taylor" /> invited you to join PlatformPlatform.
        </Trans>
      </Heading>

      <Text className="m-[0px] text-center text-[14px] leading-[24px]">
        <Trans>
          To gain access,{" "}
          <Link href="{{LoginUrl}}" className="email-link text-[#0f172a] underline">
            go to this page in your open browser
          </Link>{" "}
          and login using <Value path="Email" sample="invitee@example.com" />.
        </Trans>
      </Text>
    </TransactionalEmail>
  );
}
