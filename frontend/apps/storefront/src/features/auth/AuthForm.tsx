"use client";

import { ApiError } from "@cicek/api-client";
import { Alert, Button, TextField } from "@cicek/ui";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState, type FormEvent } from "react";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { Messages } from "@/lib/i18n/messages";
import { safeRedirectPath } from "@/lib/security";
import { authApi, currentUserKey } from "./api";
import { loginSchema, registerSchema } from "./schemas";

type Mode = "login" | "register";
type FieldErrors = Partial<Record<"email" | "password", string[]>>;
type AuthErrorMessages = Messages["auth"]["errors"];

function translateFieldMessage(code: string, messages: AuthErrorMessages): string {
  return code in messages ? messages[code as keyof AuthErrorMessages] : code;
}

function messageForError(error: unknown, messages: AuthErrorMessages): string {
  if (!(error instanceof ApiError)) return messages.generic;
  if (error.code === "auth.invalid_credentials") return messages.invalidCredentials;
  if (error.code === "auth.email_taken") return messages.emailTaken;
  if (error.kind === "rate_limited") return messages.rateLimited;
  if (error.kind === "unavailable" || error.kind === "network") return messages.unavailable;
  return messages.generic;
}

/** Login and registration share validation, error mapping and post-success navigation. */
export function AuthForm({ mode }: { mode: Mode }) {
  const { messages } = useI18n();
  const t = messages.auth;
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    const form = new FormData(event.currentTarget);
    const values = { email: String(form.get("email") ?? ""), password: String(form.get("password") ?? "") };

    const parsed = (mode === "login" ? loginSchema : registerSchema).safeParse(values);
    if (!parsed.success) {
      const errors: FieldErrors = {};
      for (const issue of parsed.error.issues) {
        const field = issue.path[0] as keyof FieldErrors;
        (errors[field] ??= []).push(translateFieldMessage(issue.message, t.errors));
      }
      setFieldErrors(errors);
      return;
    }

    setFieldErrors({});
    setSubmitting(true);
    try {
      const { user } =
        mode === "login" ? await authApi.login(parsed.data) : await authApi.register(parsed.data);
      queryClient.setQueryData(currentUserKey, user);
      router.replace(safeRedirectPath(searchParams.get("next")));
      router.refresh();
    } catch (error) {
      if (
        error instanceof ApiError &&
        error.kind === "validation" &&
        Object.keys(error.fieldErrors).length > 0
      ) {
        setFieldErrors({
          email: error.fieldErrors.email ? [t.errors.invalid_email] : undefined,
          password: error.fieldErrors.password ? [t.errors.password_too_short] : undefined,
        });
      } else {
        setFormError(messageForError(error, t.errors));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={onSubmit}
      noValidate
      className="flex flex-col gap-5"
      aria-describedby={formError ? "auth-form-error" : undefined}
    >
      {formError ? (
        <div id="auth-form-error">
          <Alert tone="error">{formError}</Alert>
        </div>
      ) : null}
      <TextField
        label={t.email}
        name="email"
        type="email"
        autoComplete="email"
        required
        errors={fieldErrors.email}
      />
      <TextField
        label={t.password}
        name="password"
        type="password"
        autoComplete={mode === "login" ? "current-password" : "new-password"}
        required
        hint={mode === "register" ? t.passwordHint : undefined}
        errors={fieldErrors.password}
      />
      <Button type="submit" loading={submitting}>
        {mode === "login" ? t.submitLogin : t.submitRegister}
      </Button>
      <Link
        href={mode === "login" ? "/register" : "/login"}
        className="text-center text-sm font-medium text-brand-700 hover:underline"
      >
        {mode === "login" ? t.switchToRegister : t.switchToLogin}
      </Link>
    </form>
  );
}
