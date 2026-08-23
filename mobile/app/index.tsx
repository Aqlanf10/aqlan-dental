import { useSession } from "@/auth/SessionProvider";
import { BrandLoading } from "@/components/ui";
import { Redirect } from "expo-router";
import React from "react";

export default function Index() {
  const { isLoading, user } = useSession();

  if (isLoading) {
    return <BrandLoading />;
  }

  if (!user) return <Redirect href="/sign-in" />;
  if (user.mustChangePassword) return <Redirect href="/change-password" />;
  return <Redirect href="/(app)/home" />;
}
