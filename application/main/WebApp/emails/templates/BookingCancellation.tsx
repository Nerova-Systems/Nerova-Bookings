// @jsxRuntime automatic
import { Trans } from "@lingui/react/macro";
import { Hr, Section, Text } from "@react-email/components";
import { Heading } from "@repo/emails/components/Heading";
import { Subject } from "@repo/emails/components/Subject";
import { TransactionalEmail } from "@repo/emails/components/TransactionalEmail";
import { If, Else } from "@repo/emails/helpers/If";
import { Value } from "@repo/emails/helpers/Value";

type BookingCancellationProps = {
  locale: string;
};

export default function BookingCancellation({ locale }: Readonly<BookingCancellationProps>) {
  return (
    <TransactionalEmail locale={locale} preview="Your booking has been cancelled">
      <Subject>
        <Trans>Booking cancelled: <Value path="EventTitle" sample="30 min Meeting" /></Trans>
      </Subject>

      <Heading level={1} className="text-center">
        <Trans>Your booking has been cancelled</Trans>
      </Heading>

      <Text className="m-[0px] mb-[4px] text-[14px] leading-[24px]">
        <Trans>
          Hi <Value path="RecipientName" sample="Alex" />,
        </Trans>
      </Text>

      <Text className="m-[0px] mb-[16px] text-[14px] leading-[24px]">
        <Trans>
          Your booking for <strong><Value path="EventTitle" sample="30 min Meeting" /></strong> with{" "}
          <strong><Value path="HostName" sample="Anna Host" /></strong> has been cancelled.
        </Trans>
      </Text>

      <Hr className="email-separator my-[16px] border-t border-[#e2e8f0]" />

      <Section className="rounded-[8px] bg-[#f8fafc] px-[16px] py-[12px]">
        <Text className="m-[0px] mb-[8px] text-[13px] leading-[20px]">
          <strong>
            <Trans>Was scheduled for</Trans>
          </strong>
          {": "}
          <Value path={`StartTime | format_date '${locale}' 'f'`} sample="Thursday, January 15, 2026 2:00 PM" />
        </Text>

        <If path="Reason" sample={true}>
          <Text className="m-[0px] text-[13px] leading-[20px]">
            <strong>
              <Trans>Reason</Trans>
            </strong>
            {": "}
            <Value path="Reason" sample="Scheduling conflict" />
          </Text>
        </If>
      </Section>

      <Text className="email-muted m-[0px] mt-[24px] text-center text-[13px] leading-[20px] text-[#64748b]">
        <Trans>
          If you have questions, please contact <Value path="HostName" sample="Anna Host" /> directly.
        </Trans>
      </Text>
    </TransactionalEmail>
  );
}
