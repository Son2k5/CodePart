import axios from 'axios';
import { useAuthStore } from '../features/auth/store/authStore';
import type { LoginResponse } from '../features/auth/types/auth.types';
import { isSessionIdle, markSessionEnded } from '../features/auth/sessionActivity';
import { queryClient } from './queryClient';
import { toast } from 'sonner';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api';

export const axiosInstance = axios.create({
  baseURL,
  withCredentials: true, // Prioritize httpOnly cookies for refreshToken
  headers: {
    'Content-Type': 'application/json',
  },
});

let refreshPromise: Promise<LoginResponse> | null = null;
let sessionVersion = 0;
let logoutPromise: Promise<void> | null = null;
const sessionExpired = (error: unknown) => axios.isAxiosError(error)
  && ['SESSION_IDLE_TIMEOUT', 'SESSION_EXPIRED'].includes(error.response?.data?.code);

export function expireSession(showNotice = true): Promise<void> {
  if (logoutPromise) return logoutPromise;
  const token = useAuthStore.getState().accessToken;
  sessionVersion++;
  markSessionEnded();
  useAuthStore.getState().clearAuth();
  void queryClient.cancelQueries();
  queryClient.clear();
  window.dispatchEvent(new Event('auth:logout'));
  if (showNotice) toast.error('Phiên đăng nhập đã hết hạn do không hoạt động', { id: 'session-expired' });
  // Schedule after the current refresh settles; never await this from its own catch.
  const pending = refreshPromise;
  logoutPromise = (async () => {
    await pending?.catch(() => undefined);
    const options = { withCredentials: true, timeout: 10000,
      headers: token ? { Authorization: `Bearer ${token}` } : undefined };
    // Narrow cookie path requires this alias when the access token has expired.
    await axios.post(`${baseURL}/auth/refresh-token/logout`, {}, options).catch(() => undefined);
    await axios.post(`${baseURL}/auth/logout`, {}, options).catch(() => undefined);
  })().finally(() => { logoutPromise = null; });
  return logoutPromise;
}

export function refreshSession(): Promise<LoginResponse> {
  if (isSessionIdle()) {
    void expireSession(false);
    return Promise.reject(new Error('Session expired due to inactivity.'));
  }
  if (!refreshPromise) {
    const version = sessionVersion;
    const sendRefresh = () => axios.post<LoginResponse>(`${baseURL}/auth/refresh-token`, {}, { withCredentials: true });
    // Cookies are shared by tabs. Serialize rotation across tabs as well as requests.
    refreshPromise = (navigator.locks
      ? navigator.locks.request('codepath-refresh', sendRefresh)
      : sendRefresh())
      .then(({ data }) => {
        if (version !== sessionVersion || isSessionIdle()) throw new Error('Session expired.');
        useAuthStore.getState().setAuth(data.user, data.accessToken);
        return data;
      })
      .catch((error) => {
        if (sessionExpired(error) || error.response?.status === 401)
          void expireSession(sessionExpired(error) || !!useAuthStore.getState().accessToken);
        else useAuthStore.getState().clearAuth();
        throw error;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
}

axiosInstance.interceptors.request.use(async (config) => {
  if (config.url === '/auth/login' && logoutPromise) await logoutPromise;
  const token = useAuthStore.getState().accessToken;
  if (token && isSessionIdle()) {
    void expireSession();
    throw new axios.CanceledError('Session expired due to inactivity.');
  }
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

axiosInstance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && sessionExpired(error)) {
      void expireSession();
      return Promise.reject(error);
    }

    if (error.response?.status === 401
      && originalRequest
      && !originalRequest._retry
      && originalRequest.url !== '/auth/login'
      && originalRequest.url !== '/auth/refresh-token'
      && originalRequest.url !== '/auth/logout'
      && originalRequest.url !== '/auth/refresh') {
      originalRequest._retry = true;

      try {
        const { accessToken } = await refreshSession();

        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        return axiosInstance(originalRequest);
      } catch (refreshError) {
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);
