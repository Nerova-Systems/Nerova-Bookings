---
paths: **/*.tsx
description: Rules for forms with validation using ShadCN 2.0 components
---

# Form With Validation

## Form wiring

- In dialogs use `<DialogForm>` (from `@repo/ui/components/Dialog`); outside dialogs use `<Form>`. A plain `<Form>` between `DialogContent` and `DialogBody` breaks the scroll chain — `DialogForm` pre-applies `flex min-h-0 flex-1 flex-col` so `DialogBody`'s `flex-1 min-h-0 overflow-y-auto` still works.
- `api.useMutation` (or TanStack `useMutation` for multi-call flows) + `mutationSubmitter` on the form.
- Server validation: `validationErrors={mutation.error?.errors}` on the form. Fields read their own errors from `FormValidationContext` by `name`. `DialogForm` defaults `validationBehavior` to `"aria"`; `Form` requires you to set it.
- Mutation CTAs: `isPending={mutation.isPending}` on `<Button>` (auto-disables, prepends `<Spinner />`). Cancel/Close keep `disabled={...}` — they are not CTAs, no spinner.
- No client-side `isValid` gate — the server validates, and gating client-side hides the real errors from the user.
- If the backend can't return field-level errors (e.g. non-nullable `DateOnly` fails JSON deserialization on `""`), fix the backend: make it nullable + add a `NotNull` FluentValidation rule.

## Dialog wrapper/body split

Every form dialog has two components in the same file:

- **Wrapper** (`XxxDialog`): receives `isOpen` / `onOpenChange`. Contains only `DirtyDialog` + `DialogContent` + `DialogHeader`. No state, no mutation, no dirty tracking.
- **Body** (`XxxDialogBody`): child of `<DialogContent>`. Owns state, mutation, the form, `DialogBody`, and `DialogFooter`. Signals dirtiness via `useDialogSetDirty()` (from `@repo/ui/components/DirtyDialogContext`) in field `onChange` handlers.

`DialogContent` unmounts children on close, so the body is recreated on every open. Form state, mutation errors, and the dirty flag reset automatically — never write `handleCloseComplete`, `mutation.reset()`, or `setIsFormDirty(false)`.

Wizard state (`step`, intermediate values) also lives in the body — unmount resets the wizard to step 0 on reopen.

## DirtyDialog

- Props: `open`, `onOpenChange`, `trackingTitle` (+ optional label overrides). All state lives in the body.
- Cancel button: `<DialogClose render={<Button type="reset" ... />}>`. `type="reset"` bypasses the unsaved-changes warning.
- Close on success: call `onClose` passed from the wrapper. Do not reset anything manually — unmount does it.

## Anti-patterns

- `<Form>` inside a dialog — breaks the `DialogBody` scroll chain. Use `<DialogForm>`.
- State (`useState`, `useMutation`, refs) in the wrapper — persists across close/reopen. Always in the body.
- `isValid`-gated submit button — hides server errors from the user.
- `handleCloseComplete`, `mutation.reset()`, `setIsFormDirty(false)` — symptoms of state living in the wrong component.
- `<FormErrorMessage>` — deprecated. Use `validationErrors`.

## Multi-call submits

Compose `api.useMutation` calls inside a TanStack `useMutation({ mutationFn: async (d) => { ... } })`, then pass its `error?.errors` into `validationErrors`.

## Example

```tsx
export function InviteUserDialog({ isOpen, onOpenChange }: Readonly<Props>) {
  const handleClose = () => onOpenChange(false);
  return (
    <DirtyDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Invite user">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle><Trans>Invite user</Trans></DialogTitle>
        </DialogHeader>
        <InviteUserDialogBody onClose={handleClose} />
      </DialogContent>
    </DirtyDialog>
  );
}

function InviteUserDialogBody({ onClose }: { onClose: () => void }) {
  const setDirty = useDialogSetDirty();
  const inviteMutation = api.useMutation("post", "/api/account/users/invite", {
    onSuccess: () => { toast.success(t`User invited`); onClose(); }
  });

  return (
    <DialogForm onSubmit={mutationSubmitter(inviteMutation)} validationErrors={inviteMutation.error?.errors}>
      <DialogBody>
        <TextField autoFocus required name="email" label={t`Email`} onChange={() => setDirty(true)} />
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={inviteMutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={inviteMutation.isPending}>
          {inviteMutation.isPending ? <Trans>Sending...</Trans> : <Trans>Send invite</Trans>}
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}
```

Note: .NET endpoints generate `*.Api.json` on backend build; `openapi-typescript` turns it into `api.generated.d.ts`. Backend contract changes need both builds.
