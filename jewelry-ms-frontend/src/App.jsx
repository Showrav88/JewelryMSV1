import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import LoginPage from './components/Auth/LoginPage';
import RegisterPage from './components/Auth/RegisterPage';
import Dashboard from './pages/Dashboard';
import SalesPage from './pages/SalesPage';
import PurchasePage from './pages/PurchasePage';

function AppContent() {
  const [isLoggedIn, setIsLoggedIn] = useState(() => !!localStorage.getItem('token'));
  const navigate = useNavigate();

  useEffect(() => {
    const sync = () => setIsLoggedIn(!!localStorage.getItem('token'));
    window.addEventListener('storage', sync);
    return () => window.removeEventListener('storage', sync);
  }, []);

  const handleLoginSuccess = () => {
    setIsLoggedIn(true);
    navigate('/dashboard');
  };

  const handleLogout = () => {
    localStorage.clear();
    setIsLoggedIn(false);
    navigate('/login');
  };

  return (
    <Routes>
      {/* ── Public routes ── */}
      <Route
        path="/login"
        element={
          isLoggedIn
            ? <Navigate to="/dashboard" replace />
            : <LoginPage
                onLoginSuccess={handleLoginSuccess}
                onRegisterClick={() => navigate('/register')}
              />
        }
      />
      <Route
        path="/register"
        element={
          isLoggedIn
            ? <Navigate to="/dashboard" replace />
            : <RegisterPage onLoginClick={() => navigate('/login')} />
        }
      />

      {/* ── Protected routes ── */}
      <Route
        path="/dashboard"
        element={
          isLoggedIn
            ? <Dashboard
                onSalesClick={()    => navigate('/sales')}
                onPurchaseClick={() => navigate('/purchase')}
                onLogout={handleLogout}
              />
            : <Navigate to="/login" replace />
        }
      />
      <Route
        path="/sales"
        element={
          isLoggedIn
            ? <SalesPage onDashboardClick={() => navigate('/dashboard')} />
            : <Navigate to="/login" replace />
        }
      />
      <Route
        path="/purchase"
        element={
          isLoggedIn
            ? <PurchasePage onDashboardClick={() => navigate('/dashboard')} />
            : <Navigate to="/login" replace />
        }
      />

      {/* ── Fallback ── */}
      <Route
        path="*"
        element={<Navigate to={isLoggedIn ? '/dashboard' : '/login'} replace />}
      />
    </Routes>
  );
}

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;