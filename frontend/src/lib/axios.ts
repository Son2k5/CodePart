import axios from 'axios';
import { useAuthStore } from '../features/auth/store/authStore';
import type { LoginResponse } from '../features/auth/types/auth.types';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api';

export const axiosInstance = axios.create({
  baseURL,
  withCredentials: true, // Prioritize httpOnly cookies for refreshToken
  headers: {
    'Content-Type': 'application/json',
  },
});

let refreshPromise: Promise<LoginResponse> | null = null;

export function refreshSession(): Promise<LoginResponse> {
  if (!refreshPromise) {
    refreshPromise = axios
      .post<LoginResponse>(`${baseURL}/auth/refresh`, {}, { withCredentials: true })
      .then(({ data }) => {
        useAuthStore.getState().setAuth(data.user, data.accessToken);
        return data;
      })
      .catch((error) => {
        useAuthStore.getState().clearAuth();
        throw error;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
}

axiosInstance.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

axiosInstance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401
      && !originalRequest._retry
      && originalRequest.url !== '/auth/login'
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
