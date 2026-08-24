import axios from 'axios';

// Pre-configured axios instance pointed at the backend API.
const client = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:8080/api',
});

// Automatically attach the JWT token to every outgoing request.
client.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// When the server says the session is invalid, force the user back to login
// and carry the server's explanation over so Login can show why it happened.
client.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.data?.redirectToLogin) {
      localStorage.removeItem('token');
      const reason = err.response?.data?.message || 'Your session has ended. Please sign in again.';
      try {
        sessionStorage.setItem('flash', reason);
      } catch {
        /* storage unavailable — user just gets the plain login form */
      }
      window.location.href = '/login';
    }
    return Promise.reject(err);
  }
);

export default client;
