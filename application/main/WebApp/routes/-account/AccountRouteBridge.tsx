import { useLocation, useNavigate } from "@tanstack/react-router";
import { lazy, Suspense } from "react";

const AccountApp = lazy(() => import("account/AccountApp"));
const NotFoundPage = lazy(() => import("account/NotFoundPage"));

const ACCOUNT_PREFIXES = [
  "/account",
  "/components",
  "/error",
  "/legal",
  "/login",
  "/profile",
  "/signup",
  "/support",
  "/user",
  "/welcome"
];

export function AccountRouteBridge() {
  const location = useLocation();
  const navigate = useNavigate();
  const isAccountRoute = ACCOUNT_PREFIXES.some(
    (prefix) => location.pathname === prefix || location.pathname.startsWith(`${prefix}/`)
  );

  if (isAccountRoute) {
    return (
      <Suspense fallback={null}>
        <AccountApp
          initialPath={location.pathname + (location.searchStr || "")}
          onNavigateToMain={(path: string) => navigate({ to: path })}
        />
      </Suspense>
    );
  }

  return (
    <Suspense fallback={null}>
      <NotFoundPage />
    </Suspense>
  );
}
