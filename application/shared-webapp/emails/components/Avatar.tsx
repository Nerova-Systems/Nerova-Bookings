import type { ReactNode } from "react";

import { Img, Section } from "@react-email/components";

type AvatarSize = "sm" | "default" | "lg" | "xl";

const sizePx: Record<AvatarSize, { dimension: number; fontSize: number }> = {
  sm: { dimension: 24, fontSize: 12 },
  default: { dimension: 32, fontSize: 14 },
  lg: { dimension: 40, fontSize: 16 },
  xl: { dimension: 56, fontSize: 18 }
};

type AvatarProps = {
  src?: string;
  alt: string;
  fallback?: string;
  size?: AvatarSize;
};

export function Avatar({ src, alt, fallback, size = "default" }: Readonly<AvatarProps>) {
  const { dimension, fontSize } = sizePx[size];
  if (src) {
    return (
      <Img
        src={src}
        alt={alt}
        width={dimension}
        height={dimension}
        className="email-avatar rounded-full bg-[#f1f5f9] align-middle"
        style={{ display: "inline-block", objectFit: "cover" }}
      />
    );
  }

  return (
    <span
      className="email-avatar rounded-full bg-[#f1f5f9] text-center align-middle text-[#475569]"
      style={{
        display: "inline-block",
        width: `${dimension}px`,
        height: `${dimension}px`,
        fontSize: `${fontSize}px`,
        lineHeight: `${dimension}px`
      }}
      aria-label={alt}
    >
      {fallback ?? ""}
    </span>
  );
}

type AvatarGroupProps = {
  children: ReactNode;
};

// Children render side-by-side. Use plain inline-block on each child rather than inline-flex
// so legacy clients (Outlook) lay them out correctly.
export function AvatarGroup({ children }: Readonly<AvatarGroupProps>) {
  return (
    <Section>
      <span style={{ display: "inline-block" }}>{children}</span>
    </Section>
  );
}
