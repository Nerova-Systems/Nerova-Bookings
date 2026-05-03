import { Trans } from "@lingui/react/macro";
import { Section, Text } from "@react-email/components";

import { Alert } from "../components/Alert";
import { Avatar, AvatarGroup } from "../components/Avatar";
import { Badge } from "../components/Badge";
import { Button } from "../components/Button";
import { Heading } from "../components/Heading";
import { Image } from "../components/Image";
import { ProgressBar } from "../components/ProgressBar";
import { Separator } from "../components/Separator";
import { Subject } from "../components/Subject";
import { TransactionalEmail } from "../components/TransactionalEmail";
import { Else, If } from "../helpers/If";
import { Loop } from "../helpers/Loop";
import { OtpAutofill } from "../helpers/OtpAutofill";
import { Value } from "../helpers/Value";

type DemoProps = {
  locale: string;
};

export default function Demo({ locale }: DemoProps) {
  return (
    <TransactionalEmail locale={locale} preview="Demo email exercising every helper">
      <Subject>
        <Trans>Welcome to PlatformPlatform</Trans>
      </Subject>

      <Section className="mb-[1rem] text-center">
        <Image src="https://placehold.co/240x64/png?text=Logo" alt="Logo" width="120" height="32" />
      </Section>

      <Heading level={1}>
        <Trans>
          Hello <Value path="firstName" sample="Alex" />
        </Trans>
      </Heading>

      <Text className="m-0 mb-[1rem] text-[0.875rem] leading-[1.5rem]">
        <Trans>Your verification code is below. It expires in 10 minutes.</Trans>
      </Text>

      <Section className="my-[1rem] rounded-[0.5rem] bg-[#f1f5f9] p-[1rem] text-center">
        <Text className="m-0 font-mono text-[1.5rem] tracking-[0.5rem] text-[#0f172a]">
          <Value path="otpCode" sample="ABC123" />
        </Text>
      </Section>

      <Heading level={2}>
        <Trans>Project team</Trans>
      </Heading>
      <AvatarGroup>
        <Avatar alt="Alex" fallback="A" size="default" />
        <Avatar alt="Brett" fallback="B" size="default" />
        <Avatar alt="Casey" fallback="C" size="default" />
      </AvatarGroup>

      <Heading level={3}>
        <Trans>Onboarding progress</Trans>
      </Heading>
      <ProgressBar value={65} label="65% complete" />

      <Alert variant="info" title={<Trans>Recent activity</Trans>}>
        <Trans>You have the following items waiting for review:</Trans>
        <Loop path="items" sample={[{ name: "Invoice 1042" }, { name: "Invoice 1043" }]}>
          {() => (
            <div>
              - <Value path="name" sample="" />
            </div>
          )}
        </Loop>
      </Alert>

      <Section className="my-[1rem]">
        <If path="hasOutstandingBalance" sample={true}>
          <Badge variant="warning">
            <Trans>
              Outstanding balance:{" "}
              <Value path='formatCurrency balance currency="USD" locale="en-US"' sample="$129.00" />
            </Trans>
          </Badge>
          <Else>
            <Badge variant="success">
              <Trans>You are all caught up.</Trans>
            </Badge>
          </Else>
        </If>
      </Section>

      <Separator />

      <Section className="text-center">
        <Button href="https://app.platformplatform.net/account">
          <Trans>Open dashboard</Trans>
        </Button>
      </Section>

      <OtpAutofill code="otpCode" domain="domain" />
    </TransactionalEmail>
  );
}
