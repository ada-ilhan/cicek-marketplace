import { Spinner } from "@cicek/ui";
import { getRequestI18n } from "@/lib/i18n/server";

export default async function Loading() {
  const { messages } = await getRequestI18n();
  return (
    <div className="flex justify-center py-24">
      <Spinner label={messages.market.common.loading} />
    </div>
  );
}
