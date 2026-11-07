import type { Key, ReactNode } from "react";

export interface Column<T> {
  /** Stable key; also used for the cell's data attribute. */
  key: string;
  header: string;
  cell: (row: T) => ReactNode;
  align?: "right";
  /** Visually hide the header (e.g. an actions column); it stays available to screen readers. */
  srOnlyHeader?: boolean;
  /** On small screens the cell is shown as a card title without a label. */
  primary?: boolean;
}

/**
 * One accessible table that reflows into stacked cards below the `md` breakpoint (no horizontal scrolling on phones).
 * Explicit ARIA roles keep table semantics when CSS changes the display type. Labels on small screens come from the
 * column headers, so there is a single source of truth for text (and translations).
 */
export function DataTable<T>({
  columns,
  rows,
  rowKey,
  caption,
  rowClassName,
}: {
  columns: readonly Column<T>[];
  rows: readonly T[];
  rowKey: (row: T) => Key;
  caption: string;
  rowClassName?: (row: T) => string | undefined;
}) {
  return (
    <div className="rounded-2xl border border-ink-100 bg-white md:overflow-x-auto">
      <table role="table" className="block w-full text-left text-sm md:table">
        <caption className="sr-only">{caption}</caption>
        <thead
          role="rowgroup"
          className="sr-only md:not-sr-only md:table-header-group md:bg-ink-50 md:text-ink-600"
        >
          <tr role="row" className="md:table-row">
            {columns.map((column) => (
              <th
                key={column.key}
                role="columnheader"
                scope="col"
                className={`px-4 py-3 font-medium ${column.align === "right" ? "md:text-right" : ""}`}
              >
                {column.srOnlyHeader ? <span className="sr-only">{column.header}</span> : column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody role="rowgroup" className="block divide-y divide-ink-100 md:table-row-group">
          {rows.map((row) => (
            <tr
              key={rowKey(row)}
              role="row"
              className={`block px-4 py-3 md:table-row md:p-0 ${rowClassName?.(row) ?? ""}`}
            >
              {columns.map((column) => (
                <td
                  key={column.key}
                  role="cell"
                  data-label={column.srOnlyHeader || column.primary ? undefined : column.header}
                  className={[
                    "flex items-baseline justify-between gap-3 py-1 md:table-cell md:px-4 md:py-3",
                    column.align === "right" ? "md:text-right" : "",
                    column.primary ? "text-base font-semibold" : "",
                    "before:text-xs before:font-medium before:text-ink-600 before:content-[attr(data-label)] md:before:hidden",
                  ].join(" ")}
                >
                  {column.cell(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
