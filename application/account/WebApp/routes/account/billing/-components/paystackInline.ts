type PaystackTransaction = {
  reference?: string;
  trxref?: string;
  message?: string;
};

type PaystackCallbacks = {
  onSuccess: (transaction: PaystackTransaction) => void;
  onCancel: () => void;
  onError: (error: { message?: string }) => void;
};

type PaystackPopup = {
  resumeTransaction: (accessCode: string, callbacks: PaystackCallbacks) => void;
};

type PaystackConstructor = new () => PaystackPopup;

declare global {
  interface Window {
    Paystack?: PaystackConstructor;
    PaystackPop?: PaystackConstructor;
  }
}

const PaystackInlineScriptUrl = "https://js.paystack.co/v2/inline.js";

let paystackScriptPromise: Promise<PaystackConstructor> | undefined;

export function loadPaystackInline(): Promise<PaystackConstructor> {
  if (window.Paystack ?? window.PaystackPop) {
    return Promise.resolve((window.Paystack ?? window.PaystackPop)!);
  }

  paystackScriptPromise ??= new Promise((resolve, reject) => {
    const existingScript = document.querySelector<HTMLScriptElement>(`script[src="${PaystackInlineScriptUrl}"]`);
    if (existingScript) {
      existingScript.addEventListener("load", () => resolve((window.Paystack ?? window.PaystackPop)!));
      existingScript.addEventListener("error", () => reject(new Error("Paystack checkout could not be loaded.")));
      return;
    }

    const script = document.createElement("script");
    script.src = PaystackInlineScriptUrl;
    script.async = true;
    script.onload = () => {
      const paystack = window.Paystack ?? window.PaystackPop;
      if (paystack) {
        resolve(paystack);
      } else {
        reject(new Error("Paystack checkout could not be loaded."));
      }
    };
    script.onerror = () => reject(new Error("Paystack checkout could not be loaded."));
    document.head.append(script);
  });

  return paystackScriptPromise;
}

export async function resumePaystackTransaction(accessCode: string, callbacks: PaystackCallbacks) {
  const Paystack = await loadPaystackInline();
  const popup = new Paystack();
  popup.resumeTransaction(accessCode, callbacks);
}
