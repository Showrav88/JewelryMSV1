// src/services/salesService.js

import { salesAPI } from './api';

export const processSale = async (selectedProducts, customerId, paymentMethod, discount, hasExchange, exchangeData) => {
  const saleRequest = {
    customerId,
    items: selectedProducts.map(p => ({ productId: p.id })),
    discountPercentage: discount,
    paymentMethod,
    hasExchange,
    exchange: hasExchange ? {
      material: exchangeData.material,
      purity: exchangeData.purity,
      receivedWeight: exchangeData.receivedWeight,
      lossPercentage: exchangeData.lossPercentage
    } : null
  };

  const response = await salesAPI.checkout(saleRequest);
  return response.invoiceNo;
};

export const downloadPdf = async (invoiceNo) => {
  const blob = await salesAPI.downloadInvoice(invoiceNo);
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `Invoice-${invoiceNo}.pdf`;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
};