import { Hr } from "@react-email/components";

type SeparatorProps = {
  className?: string;
};

export function Separator({ className }: Readonly<SeparatorProps>) {
  return <Hr className={`email-separator my-[24px] border-t border-solid border-[#e2e8f0] ${className ?? ""}`} />;
}
