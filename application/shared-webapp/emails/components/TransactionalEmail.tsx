import type { ReactNode } from "react";

import { Body, Container, Font, Head, Html, Preview, Tailwind } from "@react-email/components";

type TransactionalEmailProps = {
  locale: string;
  preview: string;
  children: ReactNode;
};

export function TransactionalEmail({ locale, preview, children }: TransactionalEmailProps) {
  return (
    <Html lang={locale}>
      <Head>
        <Font
          fontFamily="Inter"
          fallbackFontFamily={["Helvetica", "Arial", "sans-serif"]}
          webFont={{
            url: "https://rsms.me/inter/font-files/Inter-Regular.woff2",
            format: "woff2"
          }}
          fontWeight={400}
          fontStyle="normal"
        />
      </Head>
      <Preview>{preview}</Preview>
      <Tailwind>
        <Body className="m-0 bg-[#f4f4f5] p-0 font-sans text-[#0f172a]">
          <Container className="mx-auto my-[2.5rem] w-full max-w-[37.5rem] rounded-[0.75rem] bg-white p-[2rem] shadow-sm">
            {children}
          </Container>
        </Body>
      </Tailwind>
    </Html>
  );
}
