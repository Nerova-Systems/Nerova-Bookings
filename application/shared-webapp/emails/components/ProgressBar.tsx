import { Section } from "@react-email/components";

type ProgressBarProps = {
  value: number;
  max?: number;
  label?: string;
};

export function ProgressBar({ value, max = 100, label }: Readonly<ProgressBarProps>) {
  const clamped = Math.max(0, Math.min(value, max));
  const percent = max === 0 ? 0 : Math.round((clamped / max) * 100);
  return (
    <Section className="my-[12px]">
      {label ? <div className="email-muted mb-[6px] text-[12px] text-[#475569]">{label}</div> : null}
      <div className="email-progressbar-track h-[8px] w-full rounded-full bg-[#e2e8f0]">
        <div className="email-progressbar-fill h-full rounded-full bg-[#0f172a]" style={{ width: `${percent}%` }} />
      </div>
    </Section>
  );
}
