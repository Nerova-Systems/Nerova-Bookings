import { createContext, useContext } from "react";

export type DirtyDialogContextValue = {
  setDirty: (dirty: boolean) => void;
  cancel: () => void;
  hasUnsavedChanges: boolean;
};

export const DirtyDialogContext = createContext<DirtyDialogContextValue | null>(null);

/**
 * Returns a setter that the dialog body calls (e.g. in field onChange handlers) to mark the
 * dialog as having unsaved changes. When there's no surrounding DirtyDialog the setter is a no-op.
 */
export function useDialogSetDirty(): (dirty: boolean) => void {
  const context = useContext(DirtyDialogContext);
  return context?.setDirty ?? noop;
}

function noop() {}
