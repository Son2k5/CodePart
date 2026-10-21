import { create } from 'zustand';
import type { User } from '../types/auth.types';

const SESSION_INDICATOR_KEY = 'codepath-has-session';

export function hasSessionIndicator(): boolean {
  try {
    return localStorage.getItem(SESSION_INDICATOR_KEY) === 'true';
  } catch {
    return false;
  }
}

export function setSessionIndicator(active: boolean): void {
  try {
    if (active) {
      localStorage.setItem(SESSION_INDICATOR_KEY, 'true');
    } else {
      localStorage.removeItem(SESSION_INDICATOR_KEY);
    }
  } catch {
    /* Storage unavailable */
  }
}

interface AuthState {
  user: User | null;
  accessToken: string | null;
  isInitialized: boolean;
  setAuth: (user: User, token: string) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,
  isInitialized: false,
  setAuth: (user, accessToken) => {
    setSessionIndicator(true);
    set({ user, accessToken, isInitialized: true });
  },
  clearAuth: () => {
    setSessionIndicator(false);
    set({ user: null, accessToken: null, isInitialized: true });
  },
}));
