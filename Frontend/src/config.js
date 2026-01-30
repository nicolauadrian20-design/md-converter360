// API Configuration
// In development, uses Vite proxy (/api)
// In production, uses VITE_API_URL environment variable or relative path

export const API_BASE_URL = import.meta.env.VITE_API_URL || '';

export const getApiUrl = (path) => {
  // If API_BASE_URL is set, use it
  if (API_BASE_URL) {
    return `${API_BASE_URL}${path}`;
  }
  // Otherwise use relative path (works with Vite proxy in dev, Render rewrites in prod)
  return path;
};
