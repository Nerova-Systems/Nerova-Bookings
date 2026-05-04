import { getEmailRenderMode } from "./renderMode";

type OtpAutofillProps = {
  code: string;
  domain: string;
};

// iOS Mail (and Apple Messages) detects the trailing "@{domain} #{code}" pattern in the plaintext
// body and offers a one-tap autofill suggestion above the keyboard. The HTML mirror is hidden via
// `display:none` (compatible with most clients; falls back to inline-but-styled if stripped). The
// real value of this element is the plaintext-rendered counterpart that the build pipeline emits as
// the last line of the .txt artifact — iOS reads from plaintext for autofill detection.
export function OtpAutofill({ code, domain }: Readonly<OtpAutofillProps>) {
  const isBuild = getEmailRenderMode() === "build";
  const codeValue = isBuild ? `{{ ${code} }}` : code;
  const domainValue = isBuild ? `{{ ${domain} }}` : domain;
  return (
    <div aria-hidden="true" style={{ display: "none" }}>
      @{domainValue} #{codeValue}
    </div>
  );
}

// The exact suffix the build pipeline appends to the rendered plaintext body. Kept colocated with
// the component so the build script and the component cannot drift apart.
export function buildOtpAutofillPlainTextSuffix(code: string, domain: string): string {
  return `@${domain} #${code}`;
}
