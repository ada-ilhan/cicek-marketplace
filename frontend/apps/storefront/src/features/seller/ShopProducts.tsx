"use client";

import { ApiError, type ShopProduct } from "@cicek/api-client";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  ErrorState,
  SelectField,
  Spinner,
  TextAreaField,
  TextField,
} from "@cicek/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { DataTable } from "@/components/DataTable";
import { PagerButtons } from "@/components/PagerButtons";
import { SafeImage } from "@/components/SafeImage";
import { ProductStatusBadge } from "@/components/StatusBadge";
import { errorMessage } from "@/features/cart/api";
import { bffFetch } from "@/lib/bff-client";
import { localizeFieldErrors } from "@/lib/field-errors";
import { format, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { sellerKeys, shopPath, useCategories, useShopProducts } from "./api";
import { ImageManager, type ManagedImage } from "./ImageManager";
import { ShopGate, ShopNav } from "./ShopDashboard";

export function ShopProducts({ shopId }: { shopId: string }) {
  const t = useI18n().messages.market;
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("");
  const products = useShopProducts(shopId, page, { q: query, status });
  const queryClient = useQueryClient();
  const toggleStatus = useMutation({
    mutationFn: ({ productId, active }: { productId: string; active: boolean }) =>
      bffFetch<ShopProduct>(
        `${shopPath(shopId)}/products/${productId}/${active ? "activate" : "deactivate"}`,
        { method: "POST" },
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["seller", "products", shopId] }),
  });

  return (
    <ShopGate shopId={shopId}>
      {(shop) => (
        <>
          <ShopNav shop={shop} />
          <div className="mb-4 flex justify-end">
            <Link
              href={`/seller/shops/${shopId}/products/new`}
              className="inline-flex min-h-11 items-center rounded-lg bg-brand-600 px-4 font-semibold text-white hover:bg-brand-700"
            >
              {t.seller.newProduct}
            </Link>
          </div>
          <form
            role="search"
            className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-end"
            onSubmit={(event) => {
              event.preventDefault();
              setPage(1);
              setQuery(search);
            }}
          >
            <div className="flex-1">
              <TextField
                label={t.seller.searchProducts}
                name="q"
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
            <SelectField
              label={t.orders.status}
              name="status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              <option value="">{t.common.all}</option>
              <option value="Active">{t.seller.productStatuses.Active}</option>
              <option value="Inactive">{t.seller.productStatuses.Inactive}</option>
            </SelectField>
            <Button type="submit" variant="secondary">
              {t.catalog.apply}
            </Button>
          </form>
          {toggleStatus.isError ? (
            <div className="mb-4">
              <Alert tone="error">{errorMessage(toggleStatus.error, t)}</Alert>
            </div>
          ) : null}
          {products.isPending ? (
            <Spinner label={t.common.loading} />
          ) : products.isError ? (
            <ErrorState
              title={t.common.loadError}
              onRetry={() => void products.refetch()}
              retryLabel={t.common.retry}
            />
          ) : products.data.totalCount === 0 ? (
            query || status ? (
              <EmptyState title={t.catalog.noResultsTitle} description={t.catalog.noResultsDescription} />
            ) : (
              <EmptyState title={t.seller.noProductsTitle} description={t.seller.noProductsDescription} />
            )
          ) : (
            <>
              <DataTable
                caption={t.seller.products}
                rows={products.data.items}
                rowKey={(product) => product.id}
                columns={[
                  {
                    key: "name",
                    header: t.seller.productName,
                    primary: true,
                    cell: (product) => (
                      <div className="flex items-center gap-3">
                        <div className="size-12 shrink-0 overflow-hidden rounded-lg bg-ink-100">
                          <SafeImage
                            src={product.images[0]?.url}
                            alt=""
                            fallbackLabel=""
                            className="[&_span]:hidden [&_svg]:size-6"
                          />
                        </div>
                        <Link
                          href={`/seller/shops/${shopId}/products/${product.id}`}
                          className="font-medium text-brand-700 hover:underline"
                        >
                          {product.name}
                        </Link>
                      </div>
                    ),
                  },
                  {
                    key: "status",
                    header: t.orders.status,
                    cell: (product) => (
                      <ProductStatusBadge status={product.status} labels={t.seller.productStatuses} />
                    ),
                  },
                  {
                    key: "price",
                    header: t.seller.priceLabel,
                    align: "right",
                    cell: (product) => formatMoney(product.price, product.currency, t.intlLocale),
                  },
                  {
                    key: "stock",
                    header: t.seller.stock,
                    align: "right",
                    cell: (product) =>
                      product.stockQuantity === 0 ? (
                        <span className="font-medium text-danger-700">{t.catalog.outOfStock}</span>
                      ) : (
                        product.stockQuantity
                      ),
                  },
                  {
                    key: "manage",
                    header: t.seller.manage,
                    srOnlyHeader: true,
                    align: "right",
                    cell: (product) => (
                      <Button
                        variant="ghost"
                        disabled={toggleStatus.isPending}
                        onClick={() =>
                          toggleStatus.mutate({ productId: product.id, active: product.status !== "Active" })
                        }
                      >
                        {product.status === "Active" ? t.seller.deactivate : t.seller.activate}
                      </Button>
                    ),
                  },
                ]}
              />
              <PagerButtons
                page={products.data.page}
                totalPages={products.data.totalPages}
                onChange={setPage}
              />
            </>
          )}
        </>
      )}
    </ShopGate>
  );
}

const MAX_PRODUCT_IMAGES = 8;

type ProductField = "categoryId" | "name" | "description" | "price" | "stockQuantity" | "images";

/** Create or edit a product. Edits send the version that was loaded, so concurrent changes are detected (409). */
export function ProductForm({ shopId, productId }: { shopId: string; productId?: string }) {
  const t = useI18n().messages.market;
  const router = useRouter();
  const queryClient = useQueryClient();
  const categories = useCategories();
  const existing = useQuery({
    queryKey: sellerKeys.product(shopId, productId ?? "new"),
    queryFn: () => bffFetch<ShopProduct>(`${shopPath(shopId)}/products/${productId}`),
    enabled: Boolean(productId),
  });
  const [errors, setErrors] = useState<Partial<Record<ProductField, string[]>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  // null = "not edited yet": the list is derived from the loaded product, then owned by the form until saved.
  const [editedImages, setEditedImages] = useState<ManagedImage[] | null>(null);

  if (productId && existing.isPending) return <Spinner label={t.common.loading} />;
  if (productId && existing.isError) {
    return (
      <ErrorState
        title={t.common.loadError}
        onRetry={() => void existing.refetch()}
        retryLabel={t.common.retry}
      />
    );
  }

  const product = existing.data;
  const images: ManagedImage[] =
    editedImages ??
    (product?.images ?? []).map((image) => ({ url: image.url, altText: image.altText ?? "" }));

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const price = Number(String(form.get("price") ?? "").replace(",", "."));
    const stock = Number(form.get("stockQuantity"));
    const body = {
      categoryId: String(form.get("categoryId") ?? ""),
      name: String(form.get("name") ?? ""),
      description: String(form.get("description") ?? "") || null,
      price,
      stockQuantity: stock,
      images: images.map((image) => ({ url: image.url, altText: image.altText.trim() || null })),
      version: product?.version ?? null,
    };

    const clientErrors: Partial<Record<ProductField, string[]>> = {};
    if (!body.categoryId) clientErrors.categoryId = [t.checkout.errors.required];
    if (!body.name.trim()) clientErrors.name = [t.checkout.errors.required];
    if (!Number.isFinite(price) || price <= 0) clientErrors.price = [t.checkout.errors.required];
    if (!Number.isInteger(stock) || stock < 0) clientErrors.stockQuantity = [t.checkout.errors.required];
    if (images.length > MAX_PRODUCT_IMAGES) {
      clientErrors.images = [format(t.media.errors.tooMany, { max: MAX_PRODUCT_IMAGES })];
    }
    setErrors(clientErrors);
    if (Object.keys(clientErrors).length > 0) return;

    setFormError(null);
    setSaved(false);
    setSubmitting(true);
    try {
      const result = await bffFetch<ShopProduct>(
        productId ? `${shopPath(shopId)}/products/${productId}` : `${shopPath(shopId)}/products`,
        { method: productId ? "PUT" : "POST", body },
      );
      await queryClient.invalidateQueries({ queryKey: ["seller", "products", shopId] });
      if (productId) {
        queryClient.setQueryData(sellerKeys.product(shopId, productId), result);
        setEditedImages(null);
        setSaved(true);
      } else {
        router.push(`/seller/shops/${shopId}/products/${result.id}`);
      }
    } catch (error) {
      if (
        error instanceof ApiError &&
        error.kind === "validation" &&
        Object.keys(error.fieldErrors).length > 0
      ) {
        const next = localizeFieldErrors<ProductField>(error.fieldErrors, t.common.invalidValue, (key) =>
          key.startsWith("Images") || key.startsWith("images") ? "images" : (key as ProductField),
        );
        if (next.images) next.images = [t.media.errors.invalidUrl];
        setErrors(next);
      } else {
        setFormError(errorMessage(error, t));
      }
    } finally {
      setSubmitting(false);
    }
  }

  const parents = (categories.data ?? []).filter((c) => c.parentId === null);
  return (
    <ShopGate shopId={shopId}>
      {(shop) => (
        <>
          <ShopNav shop={shop} />
          <Card>
            <h1 className="mb-4 text-xl font-semibold">
              {productId ? t.seller.editProduct : t.seller.newProduct}
            </h1>
            {saved ? (
              <div className="mb-4">
                <Alert tone="success">{t.seller.saved}</Alert>
              </div>
            ) : null}
            {formError ? (
              <div className="mb-4">
                <Alert tone="error">{formError}</Alert>
              </div>
            ) : null}
            <form
              key={product?.version ?? "new"}
              onSubmit={onSubmit}
              noValidate
              className="grid gap-4 sm:grid-cols-2"
            >
              <SelectField
                label={t.seller.category}
                name="categoryId"
                required
                defaultValue={product?.categoryId ?? ""}
                errors={errors.categoryId}
              >
                <option value="">{t.seller.chooseCategory}</option>
                {parents.map((parent) => (
                  <optgroup key={parent.id} label={parent.name}>
                    {(categories.data ?? [])
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
                label={t.seller.productName}
                name="name"
                required
                maxLength={150}
                defaultValue={product?.name ?? ""}
                errors={errors.name}
              />
              <div className="sm:col-span-2">
                <TextAreaField
                  label={t.seller.productDescription}
                  name="description"
                  maxLength={2000}
                  defaultValue={product?.description ?? ""}
                  errors={errors.description}
                />
              </div>
              <TextField
                label={product ? format(t.seller.price, { currency: product.currency }) : t.seller.priceLabel}
                name="price"
                inputMode="decimal"
                required
                defaultValue={product ? String(product.price) : ""}
                errors={errors.price}
              />
              <TextField
                label={t.seller.stock}
                name="stockQuantity"
                type="number"
                min={0}
                required
                defaultValue={product ? String(product.stockQuantity) : "0"}
                errors={errors.stockQuantity}
              />
              <div className="sm:col-span-2">
                <ImageManager
                  shopId={shopId}
                  images={images}
                  onChange={setEditedImages}
                  max={MAX_PRODUCT_IMAGES}
                  error={errors.images?.[0]}
                />
              </div>
              <div className="sm:col-span-2">
                <Button type="submit" loading={submitting}>
                  {t.common.save}
                </Button>
              </div>
            </form>
          </Card>
        </>
      )}
    </ShopGate>
  );
}
