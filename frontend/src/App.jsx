import { Routes, Route, Navigate } from 'react-router-dom';
import Login from './pages/Login.jsx';
import Register from './pages/Register.jsx';
import Verify from './pages/Verify.jsx';
import UsersPage from './pages/UsersPage.jsx';

function isAuthed() {
  return !!localStorage.getItem('token');
}

// Redirects unauthenticated visitors to the login page.
function PrivateRoute({ children }) {
  return isAuthed() ? children : <Navigate to="/login" replace />;
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
