const authExpiredEventName = 'astererp:auth-expired';

export function dispatchAuthExpired(): void {
  window.dispatchEvent(new CustomEvent(authExpiredEventName));
}

export function subscribeAuthExpired(handler: () => void): () => void {
  window.addEventListener(authExpiredEventName, handler);
  return () => window.removeEventListener(authExpiredEventName, handler);
}
