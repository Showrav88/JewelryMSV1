// src/services/salesService.js

import { salesAPI } from './api';

const API_URL = process.env.REACT_APP_API_URL;

export const processSale = async (
  selectedProducts,
  customerId,
  paymentMethod,
  discount,
  hasExchange,
  exchangeData,
  remarks = ''
) => {
  const saleRequest = {
    customerId,
    items: selectedProducts.map(p => ({ productId: p.id })),
    discountPercentage: discount,
    paymentMethod,
    remarks,
    hasExchange,
    exchange: hasExchange ? {
      material:       exchangeData.material,
      purity:         exchangeData.purity,
      receivedWeight: exchangeData.receivedWeight,
      lossPercentage: exchangeData.lossPercentage
    } : null
  };

  const response = await salesAPI.checkout(saleRequest);

  // Safety net: save invoice number locally before anything else can go wrong
  try {
    const history = JSON.parse(localStorage.getItem('sale_history') || '[]');
    history.unshift({ invoiceNo: response.invoiceNo, createdAt: new Date().toISOString() });
    localStorage.setItem('sale_history', JSON.stringify(history.slice(0, 50)));
  } catch {
    // never block the sale flow on a storage failure
  }

  return response.invoiceNo;
};

export const downloadPdf = async (invoiceNo) => {
  const token = localStorage.getItem('token');
  const response = await fetch(`${API_URL}/Sales/download/${invoiceNo}`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  if (!response.ok) throw new Error('Download failed');
  const blob = await response.blob();
  const url  = window.URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = `${invoiceNo}.pdf`;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
};

// Fetch all sales summaries for history tab
// GET /api/Sales returns list — field names mapped flexibly below
export const fetchRecentSales = async () => {
  const token = localStorage.getItem('token');
  const response = await fetch(`${API_URL}/Sales`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  if (!response.ok) throw new Error('Failed to load sales history');
  const data = await response.json();

  // Normalize field names — handle both camelCase variants the API might return
  return data.map(item => ({
    invoiceNo:    item.invoiceNo    ?? item.invoice_no    ?? '—',
    customerName: item.customerName ?? item.customer_name ?? '—',
    saleDate:     item.saleDate     ?? item.sale_date     ?? item.createdAt ?? item.created_at,
    netPayable:   item.netPayable   ?? item.net_payable   ?? null,
    status:       item.status       ?? '—',
  }));
};