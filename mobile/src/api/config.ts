const FALLBACK_API_URL = 'https://aqlan-dental.vercel.app';

export const API_URL = (process.env.EXPO_PUBLIC_API_URL || FALLBACK_API_URL).replace(/\/+$/, '');

if (__DEV__ && !API_URL.startsWith('https://') && !API_URL.includes('localhost')) {
  console.warn('EXPO_PUBLIC_API_URL should use HTTPS outside local development.');
}
