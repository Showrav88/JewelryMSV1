const API_URL = 'http://localhost:5284/api';

export const loginAPI = async (email, password) => {
  const response = await fetch(`${API_URL}/Auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });
  
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || 'Login failed');
  return data;
};

export const registerAPI = async (email, password, fullName) => {
  const response = await fetch(`${API_URL}/Auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, fullName, role: 'STAFF', shopId: '' })
  });
  
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || 'Register failed');
  return data;
};

export const fetchWithToken = async (endpoint, options = {}) => {
  const token = localStorage.getItem('token');
  const headers = {
    'Content-Type': 'application/json',
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  
  const response = await fetch(`${API_URL}${endpoint}`, {
    ...options,
    headers,
  });
  
  if (!response.ok) throw new Error('Request failed');
  return response.json();
};

export const salesAPI = {
  checkout: async (saleRequest) => {
    const token = localStorage.getItem('token');
    const response = await fetch(`${API_URL}/Sales/checkout`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      body: JSON.stringify(saleRequest)
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.message || 'Checkout failed');
    return data;
  },

  downloadInvoice: async (invoiceNo) => {
    const token = localStorage.getItem('token');
    const response = await fetch(`${API_URL}/Sales/download/${invoiceNo}`, {
      headers: { 'Authorization': `Bearer ${token}` }
    });
    if (!response.ok) throw new Error('Download failed');
    return response.blob();
  }
};