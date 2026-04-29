import { createContext, useContext, useEffect, useState } from "react";

interface CmdkContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}

const CmdkContext = createContext<CmdkContextValue | null>(null);

export function CmdkProvider({ children }: { children: React.ReactNode }) {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen(true);
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, []);

  return <CmdkContext.Provider value={{ open, setOpen }}>{children}</CmdkContext.Provider>;
}

export function useCmdk() {
  const context = useContext(CmdkContext);
  if (!context) throw new Error("useCmdk must be used within CmdkProvider.");
  return context;
}
