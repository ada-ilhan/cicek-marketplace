/**
 * Server validation messages are English developer text and must never reach the UI. This keeps the field keys
 * (so the right inputs are flagged) and replaces every message with one localized sentence supplied by the caller.
 */
export function localizeFieldErrors<K extends string>(
  fieldErrors: Readonly<Record<string, readonly string[]>>,
  message: string,
  normalizeKey: (key: string) => K = (key) => key as K,
): Partial<Record<K, string[]>> {
  const result: Partial<Record<K, string[]>> = {};
  for (const key of Object.keys(fieldErrors)) {
    result[normalizeKey(key)] = [message];
  }
  return result;
}
