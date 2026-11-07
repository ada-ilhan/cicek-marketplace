import type { Category } from "@cicek/api-client";
import { SelectField, TextField } from "@cicek/ui";
import type { Messages } from "@/lib/i18n/messages";
import { productSorts, type ProductFilters } from "./search-params";

/** Plain GET form: filtering works without client-side JavaScript. */
export function CatalogFilters({
  filters,
  categories,
  messages,
}: {
  filters: ProductFilters;
  categories: Category[];
  messages: Messages["market"]["catalog"];
}) {
  const parents = categories.filter((c) => c.parentId === null);
  return (
    <form
      method="get"
      action="/products"
      className="grid gap-4 rounded-2xl bg-white p-4 shadow-sm sm:grid-cols-2 lg:grid-cols-6"
    >
      <div className="lg:col-span-2">
        <TextField
          label={messages.searchLabel}
          name="q"
          defaultValue={filters.q}
          placeholder={messages.searchPlaceholder}
          maxLength={100}
        />
      </div>
      <SelectField label={messages.category} name="categoryId" defaultValue={filters.categoryId}>
        <option value="">{messages.allCategories}</option>
        {parents.map((parent) => (
          <optgroup key={parent.id} label={parent.name}>
            <option value={parent.id}>{parent.name}</option>
            {categories
              .filter((c) => c.parentId === parent.id)
              .map((child) => (
                <option key={child.id} value={child.id}>
                  {child.name}
                </option>
              ))}
          </optgroup>
        ))}
      </SelectField>
      <TextField
        label={messages.minPrice}
        name="minPrice"
        defaultValue={filters.minPrice}
        inputMode="decimal"
      />
      <TextField
        label={messages.maxPrice}
        name="maxPrice"
        defaultValue={filters.maxPrice}
        inputMode="decimal"
      />
      <SelectField label={messages.sort} name="sort" defaultValue={filters.sort}>
        {productSorts.map((sort) => (
          <option key={sort} value={sort}>
            {messages.sorts[sort]}
          </option>
        ))}
      </SelectField>
      <label className="flex items-center gap-2 text-sm font-medium text-ink-900 lg:col-span-2">
        <input
          type="checkbox"
          name="inStock"
          value="true"
          defaultChecked={filters.inStock}
          className="size-4"
        />
        {messages.inStockOnly}
      </label>
      <div className="lg:col-span-4 lg:text-right">
        <button
          type="submit"
          className="min-h-11 rounded-lg bg-brand-600 px-5 font-semibold text-white hover:bg-brand-700"
        >
          {messages.apply}
        </button>
      </div>
    </form>
  );
}
