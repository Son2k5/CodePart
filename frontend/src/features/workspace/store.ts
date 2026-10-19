import { createStore } from 'zustand/vanilla';

export type Draft = { code: string; savedAt?: number; error?: string };
export type Phase = 'idle' | 'running' | 'submitting' | 'queued' | 'judging' | 'done' | 'error';
export function createWorkspaceStore(storage: Pick<Storage, 'getItem' | 'setItem'> = localStorage) {
  const timers = new Map<string, ReturnType<typeof setTimeout>>();
  const store = createStore<{
    drafts: Record<string, Draft>; phase: Phase;
    open: (key: string, starter: string) => string;
    edit: (key: string, code: string) => void;
    save: (key: string) => void;
    begin: (phase: 'running' | 'submitting') => boolean;
    transition: (phase: Phase) => void;
  }>((set, get) => ({
    drafts: {}, phase: 'idle',
    open: (key, starter) => {
      if (get().drafts[key]) return get().drafts[key].code;
      let code = starter;
      try { code = storage.getItem(key) ?? starter; } catch { /* Editor remains usable without storage. */ }
      set(s => ({ drafts: { ...s.drafts, [key]: { code } } }));
      return code;
    },
    edit: (key, code) => {
      clearTimeout(timers.get(key));
      set(s => ({ drafts: { ...s.drafts, [key]: { code } } }));
      timers.set(key, setTimeout(() => get().save(key), 800));
    },
    save: key => {
      clearTimeout(timers.get(key)); timers.delete(key);
      const draft = get().drafts[key]; if (!draft) return;
      try {
        storage.setItem(key, draft.code);
        set(s => ({ drafts: { ...s.drafts, [key]: { code: draft.code, savedAt: Date.now() } } }));
      } catch {
        set(s => ({ drafts: { ...s.drafts, [key]: { ...draft, error: 'Chưa lưu được trên thiết bị này' } } }));
      }
    },
    begin: phase => {
      if (['running', 'submitting', 'queued', 'judging'].includes(get().phase)) return false;
      set({ phase }); return true;
    },
    transition: phase => set({ phase }),
  }));
  return store;
}
export type WorkspaceStore = ReturnType<typeof createWorkspaceStore>;
export const draftKey = (userId: string, problemId: string, language: string) => `codepath:draft:v2:${userId}:${problemId}:${language}`;
