import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useIdleTimer } from 'react-idle-timer';
import { toast } from 'sonner';
import { axiosInstance, expireSession } from '../../../lib/axios';
import { useAuthStore } from '../store/authStore';
import { idleTimeoutMs, isSessionIdle, readActivity, recordActivity } from '../sessionActivity';

export function AutoLogoutProvider({ children, onLogout }: { children: ReactNode; onLogout: () => void }) {
  const authenticated = useAuthStore((state) => !!state.accessToken);
  const [prompted, setPrompted] = useState(false);
  const [continuing, setContinuing] = useState(false);
  const lastPing = useRef(0);
  const promptBeforeIdle = Math.min(60000, idleTimeoutMs / 2);
  const { activate, getLastActiveTime } = useIdleTimer({
    timeout: idleTimeoutMs,
    promptBeforeIdle,
    disabled: !authenticated,
    crossTab: true,
    syncTimers: 1000,
    name: 'codepath-session',
    events: ['mousemove', 'keydown', 'wheel', 'mousedown', 'touchstart', 'touchmove'],
    eventsThrottle: 500,
    onPrompt: () => setPrompted(true),
    onActive: () => setPrompted(false),
    onIdle: () => { void expireSession(); },
    onAction: (event) => {
      if (!event?.isTrusted || !authenticated || prompted) return;
      // Check before accepting the first interaction after sleep.
      if (isSessionIdle()) { void expireSession(); return; }
      recordActivity();
      // A heartbeat is sent only in response to interaction, never by a timer.
      // This keeps the server aware while the user types without navigating.
      if (Date.now() - lastPing.current >= 60000) {
        lastPing.current = Date.now();
        void axiosInstance.get('/auth/me').catch(() => undefined);
      }
    },
  });

  useEffect(() => {
    const ended = () => { setPrompted(false); onLogout(); };
    window.addEventListener('auth:logout', ended);
    return () => window.removeEventListener('auth:logout', ended);
  }, [onLogout]);

  useEffect(() => {
    if (!authenticated) return;
    const check = () => {
      const last = getLastActiveTime()?.getTime();
      if (isSessionIdle() || (last !== undefined && Date.now() - Math.max(last, readActivity()) >= idleTimeoutMs)) {
        void expireSession();
      } else if (Date.now() - readActivity() >= idleTimeoutMs - promptBeforeIdle) {
        setPrompted(true);
      }
    };
    window.addEventListener('focus', check);
    window.addEventListener('storage', check);
    document.addEventListener('visibilitychange', check);
    const interval = window.setInterval(check, 1000);
    check();
    return () => {
      window.removeEventListener('focus', check);
      window.removeEventListener('storage', check);
      document.removeEventListener('visibilitychange', check);
      window.clearInterval(interval);
    };
  }, [authenticated, getLastActiveTime, promptBeforeIdle]);

  const continueSession = async () => {
    if (isSessionIdle()) { void expireSession(); return; }
    setContinuing(true);
    try {
      await axiosInstance.get('/auth/me');
      recordActivity();
      activate();
      setPrompted(false);
    } catch {
      if (useAuthStore.getState().accessToken) toast.error('Không thể gia hạn phiên. Vui lòng thử lại.');
    } finally { setContinuing(false); }
  };

  return <>{children}{authenticated && prompted && (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4">
      <section role="alertdialog" aria-modal="true" aria-labelledby="idle-title" className="w-full max-w-md rounded-xl bg-white p-6 text-slate-900 shadow-xl">
        <h2 id="idle-title" className="text-xl font-semibold">Bạn sắp bị đăng xuất</h2>
        <p className="my-4">Phiên đăng nhập sắp hết hạn do không hoạt động. Bạn có muốn tiếp tục?</p>
        <div className="flex justify-end gap-3">
          <button type="button" onClick={() => { void expireSession(false); }} className="rounded px-4 py-2">Đăng xuất</button>
          <button type="button" autoFocus disabled={continuing} onClick={() => { void continueSession(); }} className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50">{continuing ? 'Đang xử lý…' : 'Tiếp tục'}</button>
        </div>
      </section>
    </div>
  )}</>;
}
