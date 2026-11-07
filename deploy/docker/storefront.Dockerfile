# Build context: frontend/
FROM node:22-alpine AS build
WORKDIR /repo
COPY . .
RUN npm ci --no-audit --no-fund && npm run build --workspace @cicek/storefront

FROM node:22-alpine
WORKDIR /app
# Node closes idle keep-alive connections after 5 s by default; a client (browser, proxy, test runner) that reuses a connection at
# that moment gets ECONNRESET. Keep idle connections longer than typical client/proxy idle timeouts (60 s).
ENV NODE_ENV=production PORT=3000 HOSTNAME=0.0.0.0 KEEP_ALIVE_TIMEOUT=65000
COPY --from=build /repo/apps/storefront/.next/standalone ./
COPY --from=build /repo/apps/storefront/.next/static ./apps/storefront/.next/static
USER node
EXPOSE 3000
CMD ["node", "apps/storefront/server.js"]
