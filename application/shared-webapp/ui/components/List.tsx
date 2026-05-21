import { cn } from "../utils";

/**
 * Generic list container with optional dividers between items.
 * Ported from cal.com `packages/ui/components/list/List.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface ListProps extends React.ComponentProps<"ul"> {
  /** Whether to show dividers between items. @default true */
  dividers?: boolean;
}

function List({ className, dividers = true, children, ...props }: ListProps) {
  return (
    <ul
      data-slot="list"
      className={cn(
        "flex flex-col rounded-xl border border-border bg-card",
        dividers && "[&>li:not(:last-child)]:border-b [&>li:not(:last-child)]:border-border",
        className
      )}
      {...props}
    >
      {children}
    </ul>
  );
}

interface ListItemProps extends React.ComponentProps<"li"> {
  /** Leading content (icon, avatar). */
  leading?: React.ReactNode;
  /** Trailing content (actions, badges). */
  trailing?: React.ReactNode;
  /** Subtitle / description below the main content. */
  description?: React.ReactNode;
  disabled?: boolean;
}

function ListItem({ className, leading, trailing, description, disabled, children, ...props }: ListItemProps) {
  return (
    <li
      data-slot="list-item"
      data-disabled={disabled || undefined}
      className={cn("flex items-center gap-4 px-6 py-4 data-[disabled]:opacity-50", className)}
      {...props}
    >
      {leading && <div className="shrink-0">{leading}</div>}
      <div className="min-w-0 flex-1">
        {children}
        {description && <p className="mt-0.5 text-sm text-muted-foreground">{description}</p>}
      </div>
      {trailing && <div className="shrink-0">{trailing}</div>}
    </li>
  );
}

export { List, ListItem };
