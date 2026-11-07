import type { Messages } from "@/lib/i18n/messages";

/** Localized role name. Unknown (future) roles fall back to their code; the catalog test lists every known role. */
export function roleLabel(role: string, messages: Messages): string {
  return (messages.admin.roleNames as Record<string, string>)[role] ?? role;
}

/** Localized permission description, with the code as the fallback for permissions added later. */
export function permissionLabel(permission: string, messages: Messages): string {
  return (messages.admin.permissionNames as Record<string, string>)[permission] ?? permission;
}
