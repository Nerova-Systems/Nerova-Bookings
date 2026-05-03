import type { ReactNode } from "react";

import { Body, Container, Head, Html, Preview, Tailwind } from "@react-email/components";

type TransactionalEmailProps = {
  locale: string;
  preview: string;
  children: ReactNode;
};

// System font stack — no @font-face. Apple Mail / Outlook / Gmail / Yahoo all support
// these natively. Keeping the list short avoids margin-of-error in legacy clients.
const FONT_STACK = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

// Custom <style> block injected into <Head> with STANDARD CSS (not nested). It serves two purposes:
//
// 1. Set <html> background-color in both modes. The body element only fills the content height; the
//    html element extends to the viewport. Without an explicit html bg the page area outside the
//    body shows whatever's behind it (white iframe chrome in webmail, a stark white margin in some
//    desktop clients). Setting html bg ensures the visible page is uniform light gray (light mode)
//    or dark navy (dark mode) regardless of how the client wraps the message.
//
// 2. Dark-mode opt-in. React Email's Tailwind plugin emits non-standard nested syntax for dark:
//    variants ( .foo { @media ... { ... } } ) which some email clients misinterpret as
//    unconditional. Plain @media (prefers-color-scheme: dark) at the top level is the email-
//    industry-standard pattern and degrades gracefully — clients that strip <style> blocks or
//    don't honor the media query render the inline light defaults.
//
// React Email's <Body> renders <body><table><tr><td style={...}>{children}</td></tr></table></body>.
// The body's `style` prop (background, color, font) lands on the INNER td — which has no class — so
// the dark-mode rules below also target ".email-body td" to flip that inner cell. Without this, the
// outer <body> recolors to dark navy via @media but the inner td stays inline-light, leaving a
// visible light strip inside a dark frame (and unstyled text inside still inherits the inline dark
// color). The same descendant selector also flips the default text color for unclassed elements
// (paragraphs, divs) inside the card — they inherit the td's color.
const EMAIL_STYLES = `
html { background-color: #f4f4f5; }
@media (prefers-color-scheme: dark) {
  html { background-color: #0b1220 !important; }
  .email-body { background-color: #0b1220 !important; }
  .email-body td { background-color: #0b1220 !important; color: #e2e8f0 !important; }
  .email-card { background-color: #1e293b !important; color: #e2e8f0 !important; }
  .email-card td { background-color: #1e293b !important; color: #e2e8f0 !important; }
  .email-heading { color: #f1f5f9 !important; }
  .email-otp-box { background-color: #0f172a !important; }
  .email-otp-box td { background-color: #0f172a !important; }
  .email-otp-text { color: #f1f5f9 !important; }
  .email-muted { color: #94a3b8 !important; }
  .email-link { color: #e2e8f0 !important; }
  .email-button-default { background-color: #f1f5f9 !important; color: #0f172a !important; }
  .email-progressbar-track { background-color: #334155 !important; }
  .email-progressbar-fill { background-color: #f1f5f9 !important; }
  .email-separator { border-top-color: #334155 !important; }
  .email-alert-default { border-color: #334155 !important; background-color: #1e293b !important; color: #e2e8f0 !important; }
  .email-avatar { background-color: #334155 !important; color: #cbd5e1 !important; }
}
`.trim();

export function TransactionalEmail({ locale, preview, children }: TransactionalEmailProps) {
  return (
    <Tailwind>
      <Html lang={locale}>
        <Head>
          <meta name="color-scheme" content="light dark" />
          <meta name="supported-color-schemes" content="light dark" />
          <style dangerouslySetInnerHTML={{ __html: EMAIL_STYLES }} />
        </Head>
        <Preview>{preview}</Preview>
        <Body style={{ fontFamily: FONT_STACK }} className="email-body m-[0px] bg-[#f4f4f5] p-[0px] text-[#0f172a]">
          <Container className="email-card mx-auto my-[40px] w-full max-w-[600px] rounded-[12px] bg-white p-[32px]">
            {children}
          </Container>
        </Body>
      </Html>
    </Tailwind>
  );
}
