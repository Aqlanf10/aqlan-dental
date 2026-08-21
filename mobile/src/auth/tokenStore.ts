import * as SecureStore from "expo-secure-store";

const ACCESS_TOKEN_KEY = "aqlan.mobile.access";
const REFRESH_TOKEN_KEY = "aqlan.mobile.refresh";

export type StoredTokens = {
  accessToken: string;
  refreshToken: string;
};

const options: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY
};

export async function readTokens(): Promise<StoredTokens | null> {
  const [accessToken, refreshToken] = await Promise.all([
    SecureStore.getItemAsync(ACCESS_TOKEN_KEY, options),
    SecureStore.getItemAsync(REFRESH_TOKEN_KEY, options)
  ]);

  if (!accessToken || !refreshToken) return null;
  return { accessToken, refreshToken };
}

export async function writeTokens(tokens: StoredTokens): Promise<void> {
  await Promise.all([
    SecureStore.setItemAsync(ACCESS_TOKEN_KEY, tokens.accessToken, options),
    SecureStore.setItemAsync(REFRESH_TOKEN_KEY, tokens.refreshToken, options)
  ]);
}

export async function replaceAccessToken(accessToken: string): Promise<void> {
  const current = await readTokens();
  if (!current) throw new Error("لا توجد جلسة محفوظة لتحديثها.");
  await writeTokens({ ...current, accessToken });
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(ACCESS_TOKEN_KEY, options),
    SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY, options)
  ]);
}
