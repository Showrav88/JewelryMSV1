const API_URL = process.env.REACT_APP_API_URL;
console.log('API URL:', API_URL);

// ── Auth ──────────────────────────────────────────────────────────────────────

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

// ── Generic authenticated fetch ───────────────────────────────────────────────

export const fetchWithToken = async (endpoint, options = {}) => {
  const token = localStorage.getItem('token');
  const headers = {
    'Content-Type': 'application/json',
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_URL}${endpoint}`, { ...options, headers });
  if (!response.ok) throw new Error('Request failed');
  return response.json();
};

// ── Sales ─────────────────────────────────────────────────────────────────────

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

// ── Customer Search (used by Sales + Purchase pages) ─────────────────────────
// Hits the existing GET /api/purchases/customers/search?q= endpoint
// Returns: [{ id, fullName, contactNumber, nidNumber, email, activityStatus }]

export const customerAPI = {
  search: async (query) => {
    if (!query || query.trim().length < 2) return [];

    const token = localStorage.getItem('token');
    const response = await fetch(
      `${API_URL}/Purchases/customers/search?q=${encodeURIComponent(query.trim())}`,
      { headers: { 'Authorization': `Bearer ${token}` } }
    );
    if (!response.ok) throw new Error('Customer search failed');
    return response.json();
    // Returns array of CustomerSearchResult:
    // { id, fullName, contactNumber, nidNumber, email, activityStatus }
  }
};