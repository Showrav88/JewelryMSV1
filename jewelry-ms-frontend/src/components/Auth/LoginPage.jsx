
import { useState, useContext } from 'react';
import { Mail, Lock, Loader2, AlertCircle } from 'lucide-react';
import { AuthContext } from '../../context/AuthContext';
import { loginAPI } from '../../services/api';

export default function LoginPage({ onRegisterClick }) {
  const { login } = useContext(AuthContext);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const handleLogin = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    setLoading(true);

    try {
      const response = await loginAPI(email, password);
      login(response.token, response.user);
      setSuccess('Login successful!');
      window.location.reload();
    } catch (err) {
      setError(err.message || 'Connection error');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-white to-slate-100 flex items-center justify-center px-4">
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-0 right-0 w-96 h-96 bg-amber-50 rounded-full blur-3xl opacity-40"></div>
        <div className="absolute bottom-0 left-0 w-96 h-96 bg-amber-50 rounded-full blur-3xl opacity-40"></div>
      </div>

      <div className="relative w-full max-w-md">
        <div className="bg-white rounded-lg shadow-lg border border-slate-100 p-8">
          <h1 className="text-2xl font-bold text-slate-900 text-center mb-2">
            Jewelry MS
          </h1>
          <p className="text-sm text-slate-500 text-center mb-8">
            Management System
          </p>

         {error && (
            <div data-testid="login-error" className="mb-6 p-3 bg-red-50 border border-red-200 rounded-md flex gap-3">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <p className="text-sm text-red-700">{error}</p>
            </div>
          )}
          {success && (
            <div data-testid="login-success" className="mb-6 p-3 bg-green-50 border border-green-200 rounded-md">
              <p className="text-sm text-green-700">{success}</p>
            </div>
          )}

          <form onSubmit={handleLogin} className="space-y-5">
            <div>
            <label htmlFor="email" className="block text-sm font-medium text-slate-700 mb-2">
            Email
          </label>
              <div className="relative">
                <Mail className="absolute left-3 top-3 w-5 h-5 text-slate-400" />
             <input
                type="email"
                id="email"
                name="email"
                autoComplete="email"
                data-testid="login-email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                className="w-full pl-10 pr-4 py-2 border border-slate-200 rounded-lg focus:outline-none focus:border-amber-400"
                required
              />
              </div>
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-slate-700 mb-2">
                  Password
                </label>
              <div className="relative">
                <Lock className="absolute left-3 top-3 w-5 h-5 text-slate-400" />
                            <input
                type="password"
                id="password"
                name="password"
                autoComplete="current-password"
                data-testid="login-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full pl-10 pr-4 py-2 border border-slate-200 rounded-lg focus:outline-none focus:border-amber-400"
                required
              />
              </div>
            </div>

                      <button
              type="submit"
              data-testid="login-submit"
              disabled={loading}
              className="w-full py-2 bg-amber-600 hover:bg-amber-700 text-white font-medium rounded-lg flex items-center justify-center gap-2 disabled:opacity-75"
            >
              {loading ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Signing in...
                </>
              ) : (
                'Sign In'
              )}
            </button>
          </form>

          <p className="text-center text-sm text-slate-500 mt-6">
            Don't have account? 
                    <button
            data-testid="go-to-register"
            onClick={onRegisterClick}
            className="text-amber-600 font-medium ml-1"
          >
              Register
            </button>
          </p>
        </div>
      </div>
    </div>
  );
}