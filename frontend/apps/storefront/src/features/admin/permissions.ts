/**
 * Permission strings issued by the API (backend `Permissions`). Kept in a plain module (not a "use client" file) so that
 * server components can import the values: objects exported from client modules are not usable on the server.
 * The server enforces the permissions; the UI only reflects them.
 */
export const adminPermissions = {
  overview: "admin.overview.read",
  shops: "shops.manage",
  categories: "categories.manage",
  orders: "orders.read",
  ordersManage: "orders.manage",
  users: "users.read",
} as const;
