import { Hr } from "@react-email/components";

type SeparatorProps = {
  className?: string;
};

export function Separator({ className }: SeparatorProps) {
  return <Hr className={`my-[1.5rem] border-t border-solid border-[#e2e8f0] ${className ?? ""}`} />;
}
