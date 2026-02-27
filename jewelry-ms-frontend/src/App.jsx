import { useState, useEffect } from 'react';
import { AuthProvider } from './context/AuthContext';
import LoginPage from './components/Auth/LoginPage';
import RegisterPage from './components/Auth/RegisterPage';
import Dashboard from './pages/Dashboard';
import SalesPage from './pages/SalesPage';

function AppContent() {
  const [currentPage, setCurrentPage] = useState('login');
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem('token');
    setIsLoggedIn(!!token);
    if (token) setCurrentPage('dashboard');
  }, []);

  if (!isLoggedIn) {
    return currentPage === 'login' ? 
      <LoginPage onRegisterClick={() => setCurrentPage('register')} /> : 
      <RegisterPage onLoginClick={() => setCurrentPage('login')} />;
  }

  return (
    <>
      {currentPage === 'sales' ? 
        <SalesPage onDashboardClick={() => setCurrentPage('dashboard')} /> : 
        <Dashboard onSalesClick={() => setCurrentPage('sales')} />
      }
    </>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}

export default App;