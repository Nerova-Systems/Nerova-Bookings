import type { ReactNode } from "react";

type SubjectProps = {
  children: ReactNode;
};

// The Handlebars renderer extracts the email subject from the rendered HTML's <title> tag, so the
// only requirement is that exactly one <title> element appears in the document head. This component
// makes that contract explicit at the template level.
export function Subject({ children }: Readonly<SubjectProps>) {
  return <title>{children}</title>;
}
