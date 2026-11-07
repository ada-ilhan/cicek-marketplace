import { NextResponse, type NextRequest } from "next/server";
import { serverConfig } from "@/lib/server-config";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Public delivery of uploaded images. The browser asks for /media/{id}; this route streams the bytes from the API
 * (which owns storage), so the API base URL stays private and images are same-origin for the CSP. Only the canonical
 * GUID form is forwarded, the upstream content type and caching headers are passed through.
 */
export async function GET(
  _request: NextRequest,
  context: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await context.params;
  if (!GUID.test(id)) return new NextResponse(null, { status: 404 });

  let upstream: Response;
  try {
    upstream = await fetch(new URL(`/api/v1/media/${id.toLowerCase()}`, serverConfig().apiBaseUrl), {
      headers: { Accept: "image/*" },
      cache: "no-store",
    });
  } catch {
    return new NextResponse(null, { status: 502 });
  }

  if (!upstream.ok || !upstream.body) {
    return new NextResponse(null, { status: upstream.status === 404 ? 404 : 502 });
  }

  const headers = new Headers({
    "Content-Type": upstream.headers.get("content-type") ?? "application/octet-stream",
    "Cache-Control": upstream.headers.get("cache-control") ?? "public, max-age=3600",
    "X-Content-Type-Options": "nosniff",
    "Content-Security-Policy": "default-src 'none'; sandbox",
  });
  const etag = upstream.headers.get("etag");
  if (etag) headers.set("ETag", etag);
  return new NextResponse(upstream.body, { status: 200, headers });
}
