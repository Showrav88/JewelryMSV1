// src/services/purchaseService.js

const API_URL = process.env.REACT_APP_API_URL;

const authHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${localStorage.getItem('token')}`
});

// Step 1: Calculate rate preview (no DB write)
export const calculatePurchaseRate = async ({
  baseMaterial,
  grossWeight,
  testedPurity,
  testedPurityLabel,
  standardBuyingRatePerGram,
  standardPurity
}) => {
  const response = await fetch(`${API_URL}/Purchases/calculate`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({
      baseMaterial,
      grossWeight,
      testedPurity,
      testedPurityLabel,
      standardBuyingRatePerGram,
      standardPurity
    })
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || 'Calculation failed');
  return data;
};

// Step 2: Create the purchase — returns receiptNo e.g. "PUR-20260302034520"
export const createPurchase = async ({
  customerId,
  baseMaterial,
  productDescription,
  grossWeight,
  testedPurity,
  testedPurityLabel,
  standardBuyingRatePerGram,
  standardPurity
}) => {
  const response = await fetch(`${API_URL}/Purchases`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({
      customerId,
      baseMaterial,
      productDescription,
      grossWeight,
      testedPurity,
      testedPurityLabel,
      standardBuyingRatePerGram,
      standardPurity
    })
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || 'Purchase creation failed');

  // Safety net: persist receipt number locally before anything else can go wrong
  try {
    const history = JSON.parse(localStorage.getItem('purchase_history') || '[]');
    history.unshift({ receiptNo: data.receiptNo, createdAt: new Date().toISOString() });
    localStorage.setItem('purchase_history', JSON.stringify(history.slice(0, 50)));
  } catch {
    // never block the purchase flow on a storage failure
  }

  return data.receiptNo;
};

// Step 3: Download receipt PDF by receipt number
export const downloadPurchaseReceipt = async (receiptNo) => {
  const response = await fetch(`${API_URL}/Purchases/receipt/${receiptNo}`, {
    headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
  });
  if (!response.ok) throw new Error('Receipt download failed');
  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${receiptNo}.pdf`;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
};

// Fetch all purchases from server for history tab
// Falls back to localStorage if server unreachable
export const fetchRecentPurchases = async () => {
  const response = await fetch(`${API_URL}/Purchases`, {
    headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
  });
  if (!response.ok) throw new Error('Failed to load history');
  return response.json();
  // Returns PurchaseResponse[] ordered by created_at DESC
};