/// <reference types="@repo/build/react-env.d.ts" />
/// <reference types="@repo/build/module-federation-types/main.d.ts" />

interface Window {
  payfast_do_onsite_payment: ((data: { uuid: string }) => void) | undefined;
}
