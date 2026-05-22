import { cn } from "../utils";
import { Avatar, AvatarFallback, AvatarImage, AvatarGroup, AvatarGroupCount } from "./Avatar";

/**
 * An avatar group that overlays an org logo badge on the last (or a separate) org avatar.
 * Ported from cal.com `packages/ui/components/avatar/UserAvatarGroupWithOrg.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface UserEntry {
  /** User's display name (used for fallback initials). */
  name: string;
  /** User's avatar image URL. */
  avatarUrl?: string;
}

interface UserAvatarGroupWithOrgProps {
  users: UserEntry[];
  /** Organisation logo URL. When provided, displays an org badge overlay on the group. */
  orgLogoUrl?: string;
  /** Organisation name (used for alt text). */
  orgName?: string;
  /** Maximum number of avatars to show before "+N". @default 4 */
  max?: number;
  size?: "default" | "sm" | "lg" | "xl";
  className?: string;
}

export function UserAvatarGroupWithOrg({
  users,
  orgLogoUrl,
  orgName,
  max = 4,
  size = "default",
  className
}: UserAvatarGroupWithOrgProps) {
  const visible = users.slice(0, max);
  const overflowCount = users.length - max;

  return (
    <div data-slot="user-avatar-group-with-org" className={cn("relative inline-flex", className)}>
      <AvatarGroup>
        {visible.map((user, i) => (
          <Avatar key={i} size={size}>
            {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt={user.name} />}
            <AvatarFallback>{user.name.slice(0, 2).toUpperCase()}</AvatarFallback>
          </Avatar>
        ))}
        {overflowCount > 0 && <AvatarGroupCount>{`+${overflowCount}`}</AvatarGroupCount>}
      </AvatarGroup>

      {/* Org badge overlay */}
      {orgLogoUrl && (
        <div className="absolute -right-1 -bottom-1 z-10 size-4 overflow-hidden rounded-full border-2 border-background bg-background ring-0">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={orgLogoUrl} alt={orgName ?? "Organization"} className="size-full rounded-full object-cover" />
        </div>
      )}
    </div>
  );
}
