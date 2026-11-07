import { z } from "zod";

/** Mirrors the API's limits (backend AuthLimits). The API re-validates; these give instant feedback. */
export const AUTH_LIMITS = { emailMax: 256, passwordMin: 10, passwordMax: 128 } as const;

export const loginSchema = z.object({
  email: z.string().trim().min(1, "required").max(AUTH_LIMITS.emailMax, "too_long"),
  password: z.string().min(1, "required").max(AUTH_LIMITS.passwordMax, "too_long"),
});

export const registerSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, "required")
    .max(AUTH_LIMITS.emailMax, "too_long")
    .pipe(z.email("invalid_email")),
  password: z
    .string()
    .min(AUTH_LIMITS.passwordMin, "password_too_short")
    .max(AUTH_LIMITS.passwordMax, "too_long"),
});

export type LoginValues = z.infer<typeof loginSchema>;
export type RegisterValues = z.infer<typeof registerSchema>;
