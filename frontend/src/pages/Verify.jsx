import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import client from '../api/client.js';

export default function Verify() {
  const [params] = useSearchParams();
  const [status, setStatus] = useState('pending');
  const [message, setMessage] = useState('Verifying\u2026');

  useEffect(() => {
    const token = params.get('token');
    if (!token) {
      setStatus('error');
      setMessage('Missing verification token.');
      return;
    }
    client
      .get(`/auth/verify?token=${encodeURIComponent(token)}`)
      .then((res) => {
        setStatus('ok');
        setMessage(res.data.message);
      })
      .catch((err) => {
        setStatus('error');
        setMessage(err.response?.data?.message || 'Verification failed.');
      });
  }, [params]);

  return (
    <div className="container" style={{ maxWidth: 420, marginTop: '8vh' }}>
      <div className={`alert ${status === 'error' ? 'alert-danger' : 'alert-info'}`}>{message}</div>
      <Link to="/login">Go to login</Link>
    </div>
  );
}
