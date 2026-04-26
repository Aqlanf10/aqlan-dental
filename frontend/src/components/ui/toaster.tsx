"use client";

import * as RadixToast from "@radix-ui/react-toast";
import { X, CheckCircle2, AlertCircle, Info } from "lucide-react";
import { useToastStore } from "@/stores/toastStore";
import { cn } from "@/lib/utils";

const ICONS = {
  success: <CheckCircle2 className="w-5 h-5 text-green-500 flex-shrink-0" />,
  error:   <AlertCircle  className="w-5 h-5 text-red-500   flex-shrink-0" />,
  info:    <Info         className="w-5 h-5 text-blue-500  flex-shrink-0" />,
};

export function Toaster() {
  const { toasts, dismiss } = useToastStore();

  return (
    <RadixToast.Provider swipeDirection="right" duration={4000}>
      {toasts.map((t) => (
        <RadixToast.Root
          key={t.id}
          open={t.open}
          onOpenChange={(open) => { if (!open) dismiss(t.id); }}
          className={cn(
            "flex items-start gap-3 p-4 rounded-xl shadow-lg border bg-white",
            "data-[state=open]:animate-slideIn data-[state=closed]:animate-hide",
            "w-80 max-w-[90vw]"
          )}
        >
          {ICONS[t.type]}
          <div className="flex-1 min-w-0">
            {t.title && (
              <RadixToast.Title className="font-semibold text-sm text-gray-900">
                {t.title}
              </RadixToast.Title>
            )}
            <RadixToast.Description className="text-sm text-gray-600 mt-0.5">
              {t.description}
            </RadixToast.Description>
          </div>
          <RadixToast.Close
            onClick={() => dismiss(t.id)}
            className="text-gray-400 hover:text-gray-600 transition-colors flex-shrink-0"
          >
            <X className="w-4 h-4" />
          </RadixToast.Close>
        </RadixToast.Root>
      ))}
      <RadixToast.Viewport className="fixed bottom-6 left-6 z-[9999] flex flex-col gap-2" />
    </RadixToast.Provider>
  );
}
