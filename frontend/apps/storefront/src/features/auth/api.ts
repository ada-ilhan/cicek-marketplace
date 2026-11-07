import type { CurrentUser } from "@cicek/api-client";
import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@cicek/api-client";
import { bffFetch } from "@/lib/bff-client";
import type { LoginValues, RegisterValues } from "./schemas";

export const currentUserKey = ["current-user"] as const;

export const authApi = {
  login: (values: LoginValues) =>
    bffFetch<{ user: CurrentUser }>("/api/auth/login", { method: "POST", body: values }),
  register: (values: RegisterValues) =>
    bffFetch<{ user: CurrentUser }>("/api/auth/register", { method: "POST", body: values }),
  logout: () => bffFetch<void>("/api/auth/logout", { method: "POST" }),
  currentUser: () => bffFetch<CurrentUser>("/api/bff/v1/me"),
};

/** Server state for the signed-in user. Retries only transient failures. */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: authApi.currentUser,
    retry: (failureCount, error) =>
      failureCount < 2 &&
      error instanceof ApiError &&
      (error.kind === "unavailable" || error.kind === "network"),
  });
}
