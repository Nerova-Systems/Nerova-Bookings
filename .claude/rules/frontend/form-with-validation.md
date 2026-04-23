---
paths: **/*.tsx
description: Rules for forms with validation using ShadCN 2.0 components
---

# Form With Validation

## Form wiring

- `api.useMutation` (or TanStack `useMutation` for multi-call flows) + `mutationSubmitter` on `<Form>`.
- Server validation via `<Form validationErrors={mutation.error?.errors} validationBehavior="aria">`. Fields read their own errors from `FormValidationContext` by `name`.
- Submit button: `disabled={mutation.isPending}` only. No client-side `isValid` gate — server validates.
- If the backend can't return field-level errors (e.g. non-nullable `DateOnly` fails JSON deserialization on `""`), fix the backend: make it nullable + `NotNull` FluentValidation rule.

## Dialog wrapper/body split

Every form dialog has two components in the same file:

- **Wrapper** (`XxxDialog`): receives `isOpen` / `onOpenChange`. Contains only `DirtyDialog` + `DialogContent` + `DialogHeader`. No state, no mutation, no dirty tracking.
- **Body** (`XxxDialogBody`): child of `<DialogContent>`. Owns all state, mutation, `Form`, `DialogBody`, `DialogFooter`. Signals dirtiness via `useDialogSetDirty()` in field `onChange` handlers.

**Why this split:** `DialogContent` unmounts its children on close, so the body is recreated on every open. Form state, mutation errors, dirty flag — all reset automatically because the components holding them no longer exist. No `handleCloseComplete`, no `mutation.reset()`, no `setIsFormDirty(false)` anywhere.

## DirtyDialog API

- Props: `open`, `onOpenChange`, `trackingTitle`, optional label overrides. That's it.
- Body calls `useDialogSetDirty()(true)` on any field change. The wrapper tracks the flag internally and clears it when `open` flips false.
- Cancel button: `<DialogClose render={<Button type="reset" ... />}>` — `type="reset"` bypasses the unsaved warning.
- Close on success: call `onClose` passed from the wrapper. Do not reset anything manually.

## Anti-patterns

- State (`useState`, `useMutation`, refs) in the wrapper — persists across close/reopen. Always in the body.
- `isValid`-gated submit button — hides server errors from the user.
- `handleCloseComplete` / `mutation.reset()` / `setIsFormDirty(false)` — symptoms of state in the wrong place.
- `<FormErrorMessage>` — deprecated. Use `validationErrors`.

Note: .NET endpoints generate an `*.Api.json` on backend build; `openapi-typescript` turns it into `api.generated.d.ts`. Backend contract changes need both builds.

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
    <Form
      onSubmit={mutationSubmitter(inviteMutation)}
      validationErrors={inviteMutation.error?.errors}
      validationBehavior="aria"
    >
      <DialogBody>
        <TextField autoFocus name="email" label={t`Email`} onChange={() => setDirty(true)} />
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={inviteMutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" disabled={inviteMutation.isPending}>
          {inviteMutation.isPending ? <Trans>Sending...</Trans> : <Trans>Send invite</Trans>}
        </Button>
      </DialogFooter>
    </Form>
  );
}
```

Multi-step dialogs: wizard state (`step`, intermediate values) also lives in the body — unmount resets the wizard to step 0 on reopen.

Multi-call submits: compose `api.useMutation` calls inside a TanStack `useMutation({ mutationFn: async (d) => { ... } })`, pass its `error?.errors` into `<Form validationErrors>`.
