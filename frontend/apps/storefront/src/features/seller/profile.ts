import type { ShopManagement } from "@cicek/api-client";
import { z } from "zod";
import type { Messages } from "@/lib/i18n/messages";

/** Mirrors the API's ShopProfileRequestValidator; the server validates again. */
export const profileSchema = z.object({
  name: z.string().trim().min(1).max(100),
  description: z.string().trim().max(2000),
  contactEmail: z.string().trim().max(256).pipe(z.email()),
  contactPhone: z
    .string()
    .trim()
    .regex(/^\+?[0-9 ()-]{7,}$/)
    .max(32),
  addressLine: z.string().trim().min(1).max(200),
  district: z.string().trim().min(1).max(100),
  city: z.string().trim().min(1).max(100),
  postalCode: z.string().trim().max(16),
});

export type ProfileField = keyof z.infer<typeof profileSchema>;

export type ProfileValues = z.infer<typeof profileSchema>;

/** Localized message for one zod issue on a shop profile field. */
export function validationMessage(issue: { code: string }, field: ProfileField, messages: Messages): string {
  const errors = messages.market.checkout.errors;
  if (issue.code === "too_small") return errors.required;
  if (issue.code === "too_big") return errors.tooLong;
  if (field === "contactEmail") return messages.auth.errors.invalid_email;
  if (field === "contactPhone") return errors.invalidPhone;
  return errors.required;
}

export function profileFromShop(shop: ShopManagement): ProfileValues {
  return {
    name: shop.name,
    description: shop.description ?? "",
    contactEmail: shop.contactEmail,
    contactPhone: shop.contactPhone,
    addressLine: shop.addressLine,
    district: shop.district,
    city: shop.city,
    postalCode: shop.postalCode ?? "",
  };
}
