// Store activity only; credentials remain in memory / HttpOnly cookies.
const key = 'codepath-last-activity';
export const idleTimeoutMs = Number(import.meta.env.VITE_IDLE_TIMEOUT_MINUTES || 60) * 60 * 1000;
let lastActivity = Date.now();

export function readActivity(): number {
  try {
    const value = localStorage.getItem(key);
    if (value !== null && Number.isFinite(Number(value))) return Number(value);
    localStorage.setItem(key, String(lastActivity));
  } catch { /* Fall back to memory when storage is unavailable. */ }
  return lastActivity;
}

export function recordActivity(): void {
  lastActivity = Date.now();
  try { localStorage.setItem(key, String(lastActivity)); } catch { /* Memory fallback. */ }
}

export function markSessionEnded(): void {
  lastActivity = 0;
  try { localStorage.setItem(key, '0'); } catch { /* Memory fallback. */ }
}

export function isSessionIdle(): boolean {
  return Date.now() - readActivity() >= idleTimeoutMs;
}
