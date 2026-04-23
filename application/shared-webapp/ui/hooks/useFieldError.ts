import { useCallback, useContext, useEffect, useRef, useState } from "react";

import { FormValidationContext } from "../components/Form";

/**
 * Manages error collection and suppression for field components.
 *
 * Reads errors from two sources:
 *   - `errorMessage` prop — ad-hoc errors (e.g. the component preview page). Suppressible on
 *     change+blur so the user can dismiss a stale local message without re-submitting.
 *   - `FormValidationContext` — server validation errors fed via `<Form validationErrors>`.
 *     Always shown while present: the next submission is what clears them. Suppressing them
 *     locally would hide them when the server returns the same rule on the next submit (e.g.
 *     two different invalid emails both failing "must be valid format").
 *
 * Text-like inputs (TextField, NumberField, etc.): call `markChanged()` on change, then
 * `clearOnBlur()` on blur — suppresses the `errorMessage` prop after the user changes and leaves.
 *
 * Instant-select controls (Select, DatePicker, Switch, Checkbox, etc.): call `clearNow()`
 * directly in their change handler — suppresses the `errorMessage` prop immediately on interaction.
 */
export function useFieldError({ name, errorMessage }: { name?: string; errorMessage?: string }) {
  const formErrors = useContext(FormValidationContext);
  const fieldValidationErrors = name && formErrors && name in formErrors ? formErrors[name] : undefined;
  const fieldErrorMessages = fieldValidationErrors
    ? Array.isArray(fieldValidationErrors)
      ? fieldValidationErrors
      : [fieldValidationErrors]
    : [];

  const [suppressProp, setSuppressProp] = useState(false);
  const hasChangedRef = useRef(false);

  useEffect(() => {
    setSuppressProp(false);
    hasChangedRef.current = false;
  }, [errorMessage]);

  const markChanged = useCallback(() => {
    hasChangedRef.current = true;
  }, []);

  const clearOnBlur = useCallback(() => {
    if (hasChangedRef.current) {
      setSuppressProp(true);
      hasChangedRef.current = false;
    }
  }, []);

  const clearNow = useCallback(() => {
    setSuppressProp(true);
    hasChangedRef.current = false;
  }, []);

  // Prop-supplied errorMessage takes precedence when set (and not suppressed), otherwise fall back
  // to server errors from the form context.
  const propMessage = !suppressProp && errorMessage ? errorMessage : undefined;
  const visibleMessages = propMessage ? [propMessage] : fieldErrorMessages;
  const errors = visibleMessages.length > 0 ? visibleMessages.map((message) => ({ message })) : undefined;

  return {
    errors,
    errorMessages: visibleMessages,
    isInvalid: visibleMessages.length > 0,
    markChanged,
    clearOnBlur,
    clearNow
  };
}
