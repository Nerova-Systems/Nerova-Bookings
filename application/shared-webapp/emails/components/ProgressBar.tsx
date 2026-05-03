import { Section } from "@react-email/components";

type ProgressBarProps = {
  value: number;
  max?: number;
  label?: string;
};

export function ProgressBar({ value, max = 100, label }: ProgressBarProps) {
  const clamped = Math.max(0, Math.min(value, max));
  const percent = max === 0 ? 0 : Math.round((clamped / max) * 100);
  return (
    <Section className="my-[0.75rem]">
      {label ? <div className="mb-[0.375rem] text-[0.75rem] text-[#475569]">{label}</div> : null}
      <div className="h-[0.5rem] w-full overflow-hidden rounded-full bg-[#e2e8f0]">
        <div className="h-full rounded-full bg-[#0f172a]" style={{ width: `${percent}%` }} />
      </div>
    </Section>
  );
}
