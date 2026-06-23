import { getApiConfig } from "@/lib/api/config";

/**
 * Returns a fully qualified URL for a thumbnail/image path.
 *
 * - When `baseUrl` is set (e.g., dev mode separated from backend), the backend
 *   origin is prepended so the browser fetches from the correct port.
 * - When `thumbnailUrl` is falsy or empty, returns a local placeholder so the
 *   frontend never makes a needless network round-trip to the backend.
 * - When `thumbnailUrl` already starts with `http`, returns it as-is.
 */
export const formatThumbnailUrl = (thumbnailUrl?: string): string => {
  const config = getApiConfig();
  if (!thumbnailUrl) {
    return "/rensaio.png";
  }
  if (thumbnailUrl.startsWith("http")) {
    return thumbnailUrl;
  }
  return `${config.baseUrl}${thumbnailUrl}`;
};
