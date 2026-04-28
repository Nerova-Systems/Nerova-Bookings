import { Switch as SwitchPrimitive } from "@base-ui/react/switch";

import { cn } from "../utils";

function Switch({ className, readOnly, ...props }: SwitchPrimitive.Root.Props) {
  return (
    // Wrapped in a <span> because BaseUI renders a hidden <input> as a sibling of the Root. BaseUI positions that input
    // with `position: absolute; width: 1px; height: 1px; clip-path: inset(50%)` -- already removed from flex flow, so we must
    // NOT apply `display: none` to it. If hidden via display:none the input becomes un-focusable, which breaks BaseUI's label
    // click chain (label -> input.onFocus -> switchRef.focus()). In read-only mode we surface a focus ring as click feedback
    // via focus-within on this wrapper so the ring shows whether focus lands on the Root or the hidden input.
    <span
      className={cn(
        "relative inline-flex",
        readOnly && "rounded-full outline-ring focus-within:outline-2 focus-within:outline-offset-2"
      )}
    >
      <SwitchPrimitive.Root
        data-slot="switch"
        readOnly={readOnly}
        className={cn(
          "peer relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent shadow-xs outline-ring transition-colors group-has-disabled/field:opacity-50 after:absolute after:-inset-3 focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-50 data-checked:bg-primary data-checked:active:bg-primary/80 data-unchecked:bg-input data-unchecked:active:bg-input/80 dark:data-unchecked:bg-input/80",
          className
        )}
        {...props}
      >
        <SwitchPrimitive.Thumb
          data-slot="switch-thumb"
          className="pointer-events-none block size-4 rounded-full bg-background shadow-lg ring-0 transition-transform data-checked:translate-x-4 data-unchecked:translate-x-0"
        />
      </SwitchPrimitive.Root>
    </span>
  );
}

export { Switch };
