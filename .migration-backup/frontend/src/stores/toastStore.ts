import { create } from "zustand";

type ToastType = "success" | "error" | "info";

interface Toast {
  id: string;
  type: ToastType;
  title?: string;
  description: string;
  open: boolean;
}

interface ToastStore {
  toasts: Toast[];
  show: (type: ToastType, description: string, title?: string) => void;
  dismiss: (id: string) => void;
}

export const useToastStore = create<ToastStore>((set) => ({
  toasts: [],
  show: (type, description, title) => {
    const id = crypto.randomUUID();
    set((s) => ({ toasts: [...s.toasts, { id, type, description, title, open: true }] }));
    setTimeout(() => {
      set((s) => ({
        toasts: s.toasts.map((t) => (t.id === id ? { ...t, open: false } : t)),
      }));
      setTimeout(() => {
        set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }));
      }, 300);
    }, 4000);
  },
  dismiss: (id) => {
    set((s) => ({
      toasts: s.toasts.map((t) => (t.id === id ? { ...t, open: false } : t)),
    }));
    setTimeout(() => {
      set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }));
    }, 300);
  },
}));

export const toast = {
  success: (description: string, title?: string) =>
    useToastStore.getState().show("success", description, title),
  error: (description: string, title?: string) =>
    useToastStore.getState().show("error", description, title),
  info: (description: string, title?: string) =>
    useToastStore.getState().show("info", description, title),
};
