import "server-only";
import { z } from "zod";

const schema = z.object({
  API_BASE_URL: z.url(),
  APP_ORIGIN: z.url(),
  COOKIE_SECURE: z.enum(["true", "false"]).optional(),
});

export interface ServerConfig {
  apiBaseUrl: string;
  appOrigin: string;
  cookieSecure: boolean;
}

let cached: ServerConfig | undefined;

/** Validated server-only configuration. Fails loudly on first use when the environment is incomplete. */
export function serverConfig(): ServerConfig {
  if (cached) return cached;
  const parsed = schema.safeParse(process.env);
  if (!parsed.success) {
    const fields = parsed.error.issues.map((issue) => issue.path.join(".")).join(", ");
    throw new Error(`Invalid storefront server configuration: ${fields}. See apps/storefront/.env.example.`);
  }

  cached = {
    apiBaseUrl: parsed.data.API_BASE_URL.replace(/\/+$/, ""),
    appOrigin: new URL(parsed.data.APP_ORIGIN).origin,
    cookieSecure: parsed.data.COOKIE_SECURE
      ? parsed.data.COOKIE_SECURE === "true"
      : process.env.NODE_ENV === "production",
  };
  return cached;
}
