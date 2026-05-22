import { Component, type ErrorInfo, type ReactNode } from "react";

/**
 * Standard React error boundary.
 * Ported from cal.com `packages/ui/components/errorBoundary/ErrorBoundary.tsx` (cf2a55c).
 *
 * Prop deviation: added `onError?: (error, info) => void` callback for telemetry;
 * not present in cal.com version which uses console.error only.
 */
interface ErrorBoundaryProps {
  fallback?: ReactNode | ((error: Error) => ReactNode);
  onError?: (error: Error, info: ErrorInfo) => void;
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  override componentDidCatch(error: Error, info: ErrorInfo) {
    this.props.onError?.(error, info);
    console.error("[ErrorBoundary] Caught error:", error, info);
  }

  override render() {
    if (this.state.hasError && this.state.error) {
      const { fallback } = this.props;
      if (typeof fallback === "function") {
        return fallback(this.state.error);
      }
      return fallback ?? null;
    }
    return this.props.children;
  }
}
