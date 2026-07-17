import DOMPurify from 'dompurify';

export const designerRuntimeContentSecurityPolicy = [
  "default-src 'self'",
  "base-uri 'none'",
  "object-src 'none'",
  "frame-ancestors 'self'",
  "form-action 'self'",
  "img-src 'self' data: blob:",
  "media-src 'self' data: blob:",
  "connect-src 'self'"
].join('; ');

const allowedUriPattern = /^(?:(?:https?|mailto|tel):|\/|#)/i;

export function sanitizeDesignerRuntimeHtml(value: string): string {
  return DOMPurify.sanitize(value, {
    ALLOW_DATA_ATTR: false,
    ALLOWED_ATTR: ['class', 'id', 'title', 'aria-label', 'aria-describedby', 'href', 'src', 'alt', 'target', 'rel'],
    ALLOWED_URI_REGEXP: allowedUriPattern,
    FORBID_ATTR: ['style', 'srcdoc'],
    FORBID_TAGS: ['base', 'embed', 'form', 'iframe', 'object', 'script', 'style', 'template']
  });
}

export function resolveSafeRuntimeUrl(value: unknown, fallback = ''): string {
  if (typeof value !== 'string') return fallback;
  const candidate = value.trim();
  if (!candidate || !allowedUriPattern.test(candidate)) return fallback;
  return candidate;
}

export function isSafeRuntimeUrl(value: unknown): value is string {
  return resolveSafeRuntimeUrl(value) !== '';
}

export function resolvePublishedApiRoute(binding: unknown): string | null {
  if (!isRecord(binding) || typeof binding.id !== 'string' || !isRecord(binding.config)) return null;
  const published = binding.config.published === true ||
    binding.config.status === 'published' || binding.config.status === 'Published' ||
    binding.config.lifecycleStatus === 'published' || binding.config.lifecycleStatus === 'Published';
  if (!published) return null;
  const route = binding.config.routePath ?? binding.config.route ?? binding.config.endpoint;
  return typeof route === 'string' && route.startsWith('/') && !route.startsWith('//') ? route : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}
