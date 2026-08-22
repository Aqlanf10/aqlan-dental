"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { LocaleProvider } from "@/i18n/LocaleProvider";

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: 1,
            refetchOnWindowFocus: false,
          },
          mutations: {
            retry: 0,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      {/* CORE-REQ-006: interface language and document direction. Wrapping here rather than
          in the root layout keeps the layout a server component. */}
      <LocaleProvider>{children}</LocaleProvider>
    </QueryClientProvider>
  );
}
