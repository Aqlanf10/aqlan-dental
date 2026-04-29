import axios from "axios";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export const api = axios.create({
  baseURL: API_URL,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
    "Accept-Language": "ar",
  },
});

// Inject access token on every request
api.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("access_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// On 401: try refresh once, then redirect to login
let isRefreshing = false;

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;

      if (isRefreshing) {
        // Another refresh is in progress, just redirect
        if (typeof window !== "undefined") {
          localStorage.removeItem("access_token");
          document.cookie = "aqlan_auth_status=; path=/; max-age=0";
          window.location.href = "/login";
        }
        return Promise.reject(error);
      }

      isRefreshing = true;
      try {
        const { data } = await api.post<{ accessToken: string }>(
          "/api/auth/refresh-token"
        );
        localStorage.setItem("access_token", data.accessToken);
        original.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(original);
      } catch {
        if (typeof window !== "undefined") {
          localStorage.removeItem("access_token");
          document.cookie = "aqlan_auth_status=; path=/; max-age=0";
          window.location.href = "/login";
        }
        return Promise.reject(error);
      } finally {
        isRefreshing = false;
      }
    }
    return Promise.reject(error);
  }
);

export default api;
