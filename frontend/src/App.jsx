import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import Login from './pages/Login.jsx';
import Register from './pages/Register.jsx';
import Verify from './pages/Verify.jsx';
import UsersPage from './pages/UsersPage.jsx';

function isAuthed() {
  return !!localStorage.getItem('token');
}

// Redirects unauthenticated visitors to the login page and tells them why,
// so the login screen can explain the unexpected change of view.
function PrivateRoute({ children }) {
  const location = useLocation();
  if (!isAuthed()) {
    return <Navigate to="/login" replace state={{ from: location.pathname, notice: 'Please sign in first — that page is only available to logged-in users.' }} />;
  }
  return children;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/verify" element={<Verify />} />
      <Route
        path="/users"
        element={
          <PrivateRoute>
            <UsersPage />
          </PrivateRoute>
        }
      />
      <Route path="*" element={<Navigate to={isAuthed() ? '/users' : '/login'} replace />} />
    </Routes>
  );
}
