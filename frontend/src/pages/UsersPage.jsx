import { useCallback, useEffect, useState } from 'react';
import client from '../api/client.js';
import Header from '../components/Header.jsx';
import { timeAgo, fullDateTime } from '../utils/time.js';

const STATUS_BADGE = {
  active: 'text-success',
  blocked: 'text-danger',
  unverified: 'text-warning',
};

export default function UsersPage() {
  const [users, setUsers] = useState([]);
  const [selected, setSelected] = useState(new Set());
  const [filter, setFilter] = useState('');
  const [sort, setSort] = useState('last_activity');
  const [dir, setDir] = useState('desc');
  const [toast, setToast] = useState(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await client.get('/users', { params: { q: filter || undefined, sort, dir } });
      setUsers(res.data);
    } catch (err) {
      // If the server says the session expired, the interceptor handles the redirect.
      if (!err.response?.data?.redirectToLogin) {
        setToast({ type: 'danger', message: 'Failed to load users.' });
      }
    } finally {
      setLoading(false);
    }
  }, [filter, sort, dir]);

  // Debounce the search input so we don't hammer the API on every keystroke.
  useEffect(() => {
    const debounce = setTimeout(load, 250);
    return () => clearTimeout(debounce);
  }, [load]);

  // Auto-dismiss toast notifications after a short delay.
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 4000);
    return () => clearTimeout(t);
  }, [toast]);

  function toggleOne(id) {
    setSelected((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  function toggleAll() {
    setSelected((prev) => (prev.size === users.length ? new Set() : new Set(users.map((u) => u.id))));
  }

  function toggleSort(field) {
    if (sort === field) {
      setDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSort(field);
      setDir('asc');
    }
  }

  const selectedUsers = users.filter((u) => selected.has(u.id));
  const canBlock = selectedUsers.some((u) => u.status !== 'blocked');
  const canUnblock = selectedUsers.some((u) => u.status === 'blocked');
  const canDelete = selected.size > 0;

  async function runAction(url, successMsg) {
    try {
      await client.post(url, { ids: Array.from(selected) });
      setToast({ type: 'success', message: successMsg });
      setSelected(new Set());
      load();
    } catch (err) {
      setToast({ type: 'danger', message: err.response?.data?.message || 'Action failed.' });
    }
  }

  async function deleteUnverified() {
    try {
      const res = await client.post('/users/delete-unverified');
      setToast({ type: 'success', message: res.data.message });
      load();
    } catch (err) {
      setToast({ type: 'danger', message: err.response?.data?.message || 'Action failed.' });
    }
  }

  return (
    <div>
      <Header />
      <div className="container-fluid" style={{ maxWidth: 1100 }}>
        {toast && (
          <div className={`alert alert-${toast.type} py-2`} role="alert">
            {toast.message}
          </div>
        )}
        <div className="d-flex align-items-center gap-2 mb-2 p-2 border rounded bg-light">
          <button
            className="btn btn-primary btn-sm"
            disabled={!canBlock}
            title="Block selected users"
            onClick={() => runAction('/users/block', 'Selected user(s) blocked.')}
          >
            <i className="bi bi-lock-fill me-1" /> Block
          </button>
          <button
            className="btn btn-outline-secondary btn-sm"
            disabled={!canUnblock}
            title="Unblock selected users"
            onClick={() => runAction('/users/unblock', 'Selected user(s) unblocked.')}
          >
            <i className="bi bi-unlock-fill" />
          </button>
          <button
            className="btn btn-outline-danger btn-sm"
            disabled={!canDelete}
            title="Delete selected users"
            onClick={() => runAction('/users/delete', 'Selected user(s) deleted.')}
          >
            <i className="bi bi-trash-fill" />
          </button>
          <button
            className="btn btn-outline-warning btn-sm"
            title="Delete all unverified users"
            onClick={deleteUnverified}
          >
            <i className="bi bi-person-x-fill" />
          </button>
          <div className="ms-auto" style={{ width: 240 }}>
            <input
              className="form-control form-control-sm"
              placeholder="Filter"
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
            />
          </div>
        </div>
        <div className="table-responsive border rounded">
          <table className="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th style={{ width: 40 }}>
                  <input
                    type="checkbox"
                    className="form-check-input"
                    checked={users.length > 0 && selected.size === users.length}
                    onChange={toggleAll}
                    title="Select / deselect all"
                  />
                </th>
                <th
                  role="button"
                  onClick={() => toggleSort('name')}
                  title="Sort by name"
                >
                  Name {sort === 'name' && (dir === 'asc' ? '\u2191' : '\u2193')}
                </th>
                <th
                  role="button"
                  onClick={() => toggleSort('email')}
                  title="Sort by e-mail"
                >
                  Email {sort === 'email' && (dir === 'asc' ? '\u2191' : '\u2193')}
                </th>
                <th>Status</th>
                <th
                  role="button"
                  onClick={() => toggleSort('last_activity')}
                  title="Sort by last seen"
                >
                  Last seen {sort === 'last_activity' && (dir === 'asc' ? '\u2191' : '\u2193')}
                </th>
              </tr>
            </thead>
            <tbody>
              {loading && (
                <tr>
                  <td colSpan={5} className="text-center text-muted py-4">
                    Loading{'\u2026'}
                  </td>
                </tr>
              )}
              {!loading && users.length === 0 && (
                <tr>
                  <td colSpan={5} className="text-center text-muted py-4">
                    No users found.
                  </td>
                </tr>
              )}
              {!loading &&
                users.map((u) => (
                  <tr key={u.id} className={u.status === 'blocked' ? 'text-muted' : ''}>
                    <td>
                      <input
                        type="checkbox"
                        className="form-check-input"
                        checked={selected.has(u.id)}
                        onChange={() => toggleOne(u.id)}
                      />
                    </td>
                    <td className={u.status === 'blocked' ? 'text-decoration-line-through' : ''}>{u.name}</td>
                    <td>{u.email}</td>
                    <td>
                      <span className={STATUS_BADGE[u.status] || ''}>
                        {u.status.charAt(0).toUpperCase() + u.status.slice(1)}
                      </span>
                    </td>
                    <td title={fullDateTime(u.lastActivity || u.lastLogin)}>
                      {timeAgo(u.lastActivity || u.lastLogin)}
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
