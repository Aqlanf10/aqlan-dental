import * as SecureStore from "expo-secure-store";

const ACCESS_TOKEN_KEY = "aqlan.patient.access";
const REFRESH_TOKEN_KEY = "aqlan.patient.refresh";

export type PatientTokens = {
  accessToken: string;
  refreshToken: string;
};

const options: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY
};

export async function readTokens(): Promise<PatientTokens | null> {
  const [accessToken, refreshToken] = await Promise.all([
    SecureStore.getItemAsync(ACCESS_TOKEN_KEY, options),
    SecureStore.getItemAsync(REFRESH_TOKEN_KEY, options)
  ]);

  return accessToken && refreshToken ? { accessToken, refreshToken } : null;
}

export async function writeTokens(tokens: PatientTokens): Promise<void> {
  await Promise.all([
    SecureStore.setItemAsync(ACCESS_TOKEN_KEY, tokens.accessToken, options),
    SecureStore.setItemAsync(REFRESH_TOKEN_KEY, tokens.refreshToken, options)
  ]);
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(ACCESS_TOKEN_KEY, options),
    SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY, options)
  ]);
}
