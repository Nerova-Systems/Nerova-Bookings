import { cn } from "../utils";

export interface InlineFieldGroupProps extends React.ComponentProps<"div"> {
  alignWithLabel?: boolean;
}

export function InlineFieldGroup({ alignWithLabel, className, ...props }: InlineFieldGroupProps) {
  // alignWithLabel offset = FieldLabel height (0.875rem text-sm * 1.375 leading-snug = 1.203125rem) + Field gap-3 (0.75rem) = 1.953125rem.
  // Pushes a label-less group down by exactly a label+gap, so its contents line up with the input of a sibling field.
  return <div className={cn("flex flex-wrap gap-x-6", alignWithLabel && "mt-[1.953rem]", className)} {...props} />;
}
