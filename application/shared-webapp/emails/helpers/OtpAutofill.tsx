import { getEmailRenderMode } from "./renderMode";

type OtpAutofillProps = {
  code: string;
  domain: string;
};

// iOS Mail (and Apple Messages) detects the trailing "@{domain} #{code}" pattern in the plaintext
// body and offers a one-tap autofill suggestion above the keyboard. We render it in plaintext only
// and add a visually hidden block in the HTML body — the visually hidden element keeps screen readers
// from announcing the same code twice when the rest of the email already presents it visibly.
export function OtpAutofill({ code, domain }: OtpAutofillProps) {
  const isBuild = getEmailRenderMode() === "build";
  const codeValue = isBuild ? `{{${code}}}` : code;
  const domainValue = isBuild ? `{{${domain}}}` : domain;
  return (
    <div
      aria-hidden="true"
      style={{
        position: "absolute",
        left: "-9999px",
        top: "auto",
        width: "1px",
        height: "1px",
        overflow: "hidden"
      }}
    >
      @{domainValue} #{codeValue}
    </div>
  );
}

// The exact suffix the build pipeline appends to the rendered plaintext body. Kept colocated with
// the component so the build script and the component cannot drift apart.
export function buildOtpAutofillPlainTextSuffix(code: string, domain: string): string {
  return `@${domain} #${code}`;
}
