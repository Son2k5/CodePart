import { create } from 'zustand';
import type { User } from '../types/auth.types';

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
  setAuth: (user, accessToken) => set({ user, accessToken, isInitialized: true }),
  clearAuth: () => set({ user: null, accessToken: null, isInitialized: true }),
}));
