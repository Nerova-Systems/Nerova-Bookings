import type { ReactNode } from "react";

import { Img, Section } from "@react-email/components";

type AvatarSize = "sm" | "default" | "lg" | "xl";

const sizeStyle: Record<AvatarSize, { dimension: string; fontSize: string }> = {
  sm: { dimension: "1.5rem", fontSize: "0.75rem" },
  default: { dimension: "2rem", fontSize: "0.875rem" },
  lg: { dimension: "2.5rem", fontSize: "1rem" },
  xl: { dimension: "3.5rem", fontSize: "1.125rem" }
};

type AvatarProps = {
  src?: string;
  alt: string;
  fallback?: string;
  size?: AvatarSize;
};

export function Avatar({ src, alt, fallback, size = "default" }: AvatarProps) {
  const { dimension, fontSize } = sizeStyle[size];
  if (src) {
    return (
      <Img
        src={src}
        alt={alt}
        width={dimension}
        height={dimension}
        className="inline-block rounded-full bg-[#f1f5f9] object-cover align-middle"
      />
    );
  }

  return (
    <span
      className="inline-flex items-center justify-center rounded-full bg-[#f1f5f9] text-center align-middle text-[#475569]"
      style={{ width: dimension, height: dimension, fontSize, lineHeight: dimension }}
      aria-label={alt}
    >
      {fallback ?? ""}
    </span>
  );
}

type AvatarGroupProps = {
  children: ReactNode;
};

// Children render side-by-side with overlap. Email clients ignore complex CSS selectors, so the
// overlap is achieved with inline `margin-left` on each Avatar's wrapper rather than a child
// combinator. Apply `style={{ marginLeft: "-0.5rem" }}` on each non-first child if you want the
// classic stacked-avatar look.
export function AvatarGroup({ children }: AvatarGroupProps) {
  return (
    <Section>
      <span className="inline-flex items-center">{children}</span>
    </Section>
  );
}
