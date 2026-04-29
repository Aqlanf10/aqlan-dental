import axios from "axios";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export const portalApi = axios.create({
  baseURL: API_URL,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
    "Accept-Language": "ar",
  },
});

// Inject portal token on every request
portalApi.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("portal_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// On 401: redirect to portal login
portalApi.interceptors.response.use(
  (res) => res,
  async (error) => {
    if (error.response?.status === 401 && typeof window !== "undefined") {
      localStorage.removeItem("portal_token");
      document.cookie = "aqlan_portal_auth=; path=/portal; max-age=0";
      window.location.href = "/portal/login";
    }
    return Promise.reject(error);
  }
);

export default portalApi;
