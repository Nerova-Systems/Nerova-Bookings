import { Trans } from "@lingui/react/macro";
import { Link, Section, Text } from "@react-email/components";

// Adapted from react.email/components/footer-with-one-column (MIT). Stripped of the brand logo and
// social-icon row since PlatformPlatform does not yet host those assets — the footer keeps the
// universal pieces only: brand name and a row of legal links. Per-template "if you didn't do this"
// reassurance lives inside each template's body, not in the footer, so it can be specific to the
// action that triggered the email.
//
// TransactionalEmail renders this OUTSIDE the white card so it visually appears as a separate footer
// section (Stripe/Linear/Notion convention).
//
// Legal links use the Scriban {{PublicUrl}} global injected by ScribanEmailRenderer at render time
// from the PUBLIC_URL env var. localhost dev, staging, and production each link to their own host.
export function Footer() {
  return (
    <Section className="email-footer mx-auto mb-[40px] w-full max-w-[600px] px-[32px] text-center">
      <Text className="m-[0px] text-[14px] leading-[20px] font-semibold text-[#0f172a]">PlatformPlatform</Text>
      <Text className="email-muted m-[0px] mt-[8px] text-[12px] leading-[20px] text-[#64748b]">
        <Link href="{{PublicUrl}}/legal/privacy" className="email-link text-[#64748b] underline">
          <Trans>Privacy</Trans>
        </Link>
        {" · "}
        <Link href="{{PublicUrl}}/legal/terms" className="email-link text-[#64748b] underline">
          <Trans>Terms</Trans>
        </Link>
        {" · "}
        <Link href="{{PublicUrl}}/legal/dpa" className="email-link text-[#64748b] underline">
          <Trans>DPA</Trans>
        </Link>
        {" · "}
        <Link href="{{PublicUrl}}/legal/compliance" className="email-link text-[#64748b] underline">
          <Trans>Compliance</Trans>
        </Link>
      </Text>
    </Section>
  );
}
