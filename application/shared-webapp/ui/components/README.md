# UI Components

This directory contains all shared UI components used across the application.

Components were initially scaffolded from ShadCN (`npx shadcn@latest add <name>`) on top of BaseUI primitives, then heavily customized for our project conventions (focus ring, Apple HIG control heights, dark-mode tokens, cursor-pointer, active states, accessibility, label-click behavior, etc.). The original ShadCN classes are no longer the source of truth — read the component file.

## Component Inventory

| Component                 | Origin                   | Notes                                                                                                                                                         |
| ------------------------- | ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Accordion                 | shadcn `accordion`       | Single chevron rotates on expand                                                                                                                              |
| AddToHomescreen           | Custom                   | PWA install prompt for iOS                                                                                                                                    |
| Alert                     | shadcn `alert`           | Adds warning and info variants                                                                                                                                |
| AlertDialog               | shadcn `alert-dialog`    | -                                                                                                                                                             |
| AppLayout                 | Custom                   | Application layout orchestration                                                                                                                              |
| AspectRatio               | shadcn `aspect-ratio`    | Reserves a box at a fixed ratio (e.g. 4/3, 16/9); use for image/video/PDF thumbnails to prevent layout shift                                                  |
| Avatar                    | shadcn `avatar`          | -                                                                                                                                                             |
| Badge                     | shadcn `badge`           | -                                                                                                                                                             |
| Breadcrumb                | shadcn `breadcrumb`      | -                                                                                                                                                             |
| Button                    | shadcn `button`          | `isPending` prop auto-disables and prepends a Spinner                                                                                                         |
| ButtonGroup               | shadcn `button-group`    | Visually group independent action buttons (each fires its own click). Use ToggleGroup when items represent on/off state. `orientation="vertical"` for stacked |
| Calendar                  | shadcn `calendar`        | Third-party react-day-picker; 44px cells, weekStartsOn=Monday, locale from app context                                                                        |
| Card                      | shadcn `card`            | -                                                                                                                                                             |
| Chart                     | shadcn `chart`           | Third-party recharts; exports `ChartContainer`, `ChartTooltip`, `ChartTooltipContent`, `ChartLegend`, `ChartLegendContent`, `ChartStyle`, `ChartConfig` type  |
| Checkbox                  | shadcn `checkbox`        | -                                                                                                                                                             |
| Collapsible               | shadcn `collapsible`     | Unstyled single-section disclosure primitive; style the trigger per usage site                                                                                |
| Command                   | shadcn `command`         | Third-party cmdk library; wrap in CommandDialog for a Cmd+K palette                                                                                           |
| ContextMenu               | shadcn `context-menu`    | Right-click (desktop) / long-press (mobile) menu                                                                                                              |
| DatePicker                | shadcn `calendar-22`     | ShadCN block template; shares mask/parse via `useDateField` hook                                                                                              |
| DateInput                 | Custom                   | Standalone editable date input without popover; shares logic with DatePicker via `useDateField`                                                               |
| DateRangePicker           | shadcn `calendar-23`     | ShadCN block template; shares mask/parse via `useDateRangeField` hook                                                                                         |
| Dialog                    | shadcn `dialog`          | DirtyDialog integration, mobile full-screen, DialogBody scroll wrapper, DialogForm helper for form dialogs                                                    |
| DirtyDialog               | Custom                   | Unsaved changes warning wrapper                                                                                                                               |
| Drawer                    | shadcn `drawer`          | Third-party vaul library; mobile-first bottom sheet with drag handle, swipe-to-dismiss, snap points. Use Sheet for non-gesture side panels                    |
| DropdownMenu              | shadcn `dropdown-menu`   | -                                                                                                                                                             |
| Dropzone                  | Custom                   | Drag-and-drop file picker on `react-dropzone`; supports `accept`, `maxSize`, `maxFiles`, `multiple`, and custom children for preview content                  |
| Empty                     | shadcn `empty`           | Empty state with icon, title, description                                                                                                                     |
| Field                     | shadcn `field`           | -                                                                                                                                                             |
| Form                      | Custom                   | Validation context provider                                                                                                                                   |
| HoverCard                 | shadcn `hover-card`      | Card-style preview on hover/focus (BaseUI PreviewCard). Use for user mentions and link previews                                                               |
| Input                     | shadcn `input`           | -                                                                                                                                                             |
| InputGroup                | shadcn `input-group`     | -                                                                                                                                                             |
| InputOtp                  | shadcn `input-otp`       | Third-party input-otp library                                                                                                                                 |
| Item                      | shadcn `item`            | Composable list-row primitive (Media + Title + Description + Actions). Use for settings rows, member rows, notification rows, ItemGroup for stacked lists     |
| Kbd                       | shadcn `kbd`             | Inline keyboard hint chip; pair with KbdGroup for multi-key shortcuts                                                                                         |
| Label                     | shadcn `label`           | -                                                                                                                                                             |
| LabelWithTooltip          | Custom                   | Label + Tooltip composition                                                                                                                                   |
| Link                      | Custom                   | TanStack Router integration                                                                                                                                   |
| MarkdownRenderer          | Custom                   | Markdown to HTML converter                                                                                                                                    |
| NavigationMenu            | shadcn `navigation-menu` | Horizontal navigation bar with dropdown sections (BaseUI). For marketing-style site headers                                                                   |
| NumberField               | Custom                   | Field + Input with stepper buttons, locale decimal separator, long-press repeat                                                                               |
| Pagination                | shadcn `pagination`      | -                                                                                                                                                             |
| Popover                   | shadcn `popover`         | -                                                                                                                                                             |
| Progress                  | shadcn `progress`        | Linear progress bar for determinate tasks; pair with Spinner for indeterminate                                                                                |
| RadioGroup                | shadcn `radio-group`     | -                                                                                                                                                             |
| Resizable                 | shadcn `resizable`       | Draggable split panels via react-resizable-panels; supports horizontal/vertical groups and nested splits                                                      |
| Select                    | shadcn `select`          | -                                                                                                                                                             |
| Separator                 | shadcn `separator`       | -                                                                                                                                                             |
| Sheet                     | shadcn `sheet`           | Side-anchored modal panel (left/right/top/bottom) on BaseUI Dialog. Lighter than Drawer; use for desktop side surfaces. Use SidePane for docked app-shell     |
| Skeleton                  | shadcn `skeleton`        | Loading placeholder                                                                                                                                           |
| Slider                    | shadcn `slider`          | Pointer-and-keyboard numeric input; supports single value or range with two thumbs. Forwards `id` to inner input so `<FieldLabel htmlFor>` works              |
| SideMenu                  | Custom                   | Complex sidebar navigation                                                                                                                                    |
| SidePane                  | Custom                   | Docked side panel (desktop) / full-screen overlay (mobile). Exports: SidePane, SidePaneHeader, SidePaneBody, SidePaneFooter, SidePaneClose                    |
| Tabs                      | shadcn `tabs`            | -                                                                                                                                                             |
| Sonner                    | shadcn `sonner`          | Third-party toast notification library                                                                                                                        |
| Spinner                   | shadcn `spinner`         | Indeterminate loader (lucide Loader2 with animate-spin); pair with Progress for determinate                                                                   |
| Table                     | shadcn `table`           | Built-in keyboard navigation with roving tabindex (selectedIndex/onNavigate on Table, index on TableRow); Enter/Space dispatches click on focused row         |
| TablePagination           | Custom                   | Pagination wrapper for tables                                                                                                                                 |
| TenantLogo                | Custom                   | Avatar wrapper for tenant logos with square shape support                                                                                                     |
| Textarea                  | Custom                   | Native textarea with field-sizing:content, resize-none                                                                                                        |
| TextAreaField             | Custom                   | Field + Textarea + validation composition                                                                                                                     |
| TextField                 | Custom                   | Field + Input + validation composition                                                                                                                        |
| Toggle                    | shadcn `toggle`          | Single on/off toggle (e.g. Bold). For a row of related toggles, use ToggleGroup                                                                               |
| ToggleGroup               | shadcn `toggle-group`    | Segmented row of toggles with single- or multi-select state (e.g. Bold/Italic/Underline). Use ButtonGroup when items are independent action buttons           |
| Tooltip                   | shadcn `tooltip`         | Tap-to-open support for touch devices                                                                                                                         |
| UnsavedChangesAlertDialog | Custom                   | Unsaved changes confirmation                                                                                                                                  |

---

## Project conventions for shared components

When adding or reinstalling a component, apply these project-wide conventions:

1. **Focus ring**: `outline-ring focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2` (replaces ShadCN's `ring-*` utilities). Use `outline-primary` / `outline-destructive` for variants where the ring should be solid.
2. **Apple HIG control heights**: `h-[var(--control-height)]` (38px desktop / 44px mobile) and the `--control-height-sm` / `--control-height-xs` variants. Smaller controls add a 44px tap target via `after:absolute after:-inset-3`.
3. **Dark-mode fills**: `bg-white dark:bg-input/30` for filled controls.
4. **Cursor**: clickable elements use `cursor-pointer` (replaces ShadCN's `cursor-default`).
5. **Active state**: press feedback via `active:bg-*` for buttons/triggers/menu items, `active:border-primary` for small controls.
6. **No `*:` or `**:`Tailwind variants**: in shared components use`[&>*]:`or`[&_*]:`instead — module federation CSS scoping cannot scope`:is()` selectors.

When reinstalling via `npx shadcn add <name>`: rename to PascalCase, fix `@/` imports to relative, then re-apply the conventions above.
