import { useNavigate } from 'react-router-dom';

export default function Header() {
  const navigate = useNavigate();

  function logout() {
    localStorage.removeItem('token');
    navigate('/login');
  }

  return (
    <nav className="navbar navbar-light bg-white border-bottom mb-4">
      <div className="container-fluid">
        <span className="navbar-brand fw-bold">THE APP</span>
        <button className="btn btn-outline-secondary btn-sm" onClick={logout} title="Log out">
          <i className="bi bi-box-arrow-right me-1" /> Logout
        </button>
      </div>
    </nav>
  );
}
