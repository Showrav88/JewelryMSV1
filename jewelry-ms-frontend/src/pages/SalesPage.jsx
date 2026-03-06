// src/pages/SalesPage.jsx

import { useState, useEffect, useCallback, useRef } from 'react';
import {
  Plus, Trash2, AlertCircle, Search, Download,
  History, Loader2, RefreshCw, CheckCircle, XCircle, Award, Lock
} from 'lucide-react';
import { processSale, downloadPdf, fetchRecentSales } from '../services/salesService';
import CustomerSearchInput from '../components/CustomerSearchInput';

const API_URL = process.env.REACT_APP_API_URL;

const CATEGORIES = ['All', 'Ring', 'Necklace', 'Bracelet', 'Earring', 'Chain', 'Bangle', 'Nosepin'];
const MATERIALS  = ['Gold', 'Silver', 'Platinum'];
const STATUSES   = ['All', 'Available', 'Sold', 'Reserved', 'Out_at_Workshop'];

const PURITY_LABELS = {
  Gold:     ['All', '14K', '18K', '21K', '22K', '24K'],
  Silver:   ['All', '750', '925', '999'],
  Platinum: ['All', '950'],
};

// Fixed loss % per material — no range, locked to minimum
const LOSS_FIXED = {
  Gold:     10,
  Silver:   30,
  Platinum: 10,
};

const MATERIAL_COLORS = {
  gold:     'bg-yellow-100 text-yellow-700',
  silver:   'bg-slate-100 text-slate-600',
  platinum: 'bg-indigo-100 text-indigo-700',
};

function MaterialBadge({ material }) {
  if (!material) return null;
  const cls = MATERIAL_COLORS[material.toLowerCase()] ?? 'bg-slate-100 text-slate-600';
  return (
    <span className={`text-xs px-1.5 py-0.5 rounded font-medium ${cls}`}>
      {material}
    </span>
  );
}

function ExchangePreviewCard({ data, loading, error }) {
  if (loading) return (
    <div className="flex items-center gap-2 p-3 bg-amber-50 border border-amber-200 rounded-lg text-sm text-amber-700">
      <Loader2 className="w-4 h-4 animate-spin" /> Calculating exchange requirement...
    </div>
  );
  if (error) return (
    <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-xs text-red-600 flex gap-2">
      <AlertCircle className="w-4 h-4 flex-shrink-0" /> {error}
    </div>
  );
  if (!data) return null;

  return (
    <div data-testid="exchange-preview-card" className="p-3 bg-green-50 border border-green-200 rounded-lg space-y-2">
      <p className="text-xs font-bold text-green-700 uppercase tracking-wide">Exchange Requirement</p>

      {/* ── Current product market value ── */}
      {data.productMarketValue != null && (
        <div className="p-2 bg-blue-50 border border-blue-100 rounded-lg">
          <p className="text-xs font-semibold text-blue-700 mb-1">Current Product Value</p>
          <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
            <span className="text-slate-500">Metal rate / gram</span>
            <span className="font-semibold text-right">
              ৳{Number(data.metalRatePerGram).toLocaleString('en-BD', { minimumFractionDigits: 2 })}
            </span>
            <span className="text-slate-500">Product weight</span>
            <span className="font-semibold text-right">{data.productGrossWeight} g</span>
            <span className="text-slate-700 font-bold">Market value</span>
            <span className="font-bold text-right text-blue-700">
              ৳{Number(data.productMarketValue).toLocaleString('en-BD', { minimumFractionDigits: 2 })}
            </span>
          </div>
        </div>
      )}

      {/* ── Exchange calculation ── */}
      <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
        <span className="text-slate-500">Loss applied</span>
        <span className="font-semibold text-right">{data.lossPercentage}%</span>
        <span className="text-slate-600 font-semibold">Customer must bring</span>
        <span className="font-bold text-right text-green-700">{data.requiredGoldWeight} g</span>
        <span className="text-slate-500">Shop profit (wt)</span>
        <span className="font-semibold text-right">{data.shopProfitWeight} g</span>
      </div>

      <div className="border-t border-green-200 pt-2 flex justify-between items-center">
        <span className="text-xs text-slate-500">Customer pays (making + VAT)</span>
        <span className="text-sm font-bold text-amber-700">
          ৳{Number(data.customerPayAmount).toLocaleString('en-BD', { minimumFractionDigits: 2 })}
        </span>
      </div>
    </div>
  );
}

export default function SalesPage({ onDashboardClick }) {
  const [activeTab, setActiveTab] = useState('new');

  const [products, setProducts]                 = useState([]);
  const [filteredProducts, setFilteredProducts] = useState([]);
  const [searchSku, setSearchSku]               = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All');
  const [filterMaterial, setFilterMaterial]     = useState('All');
  const [filterPurity, setFilterPurity]         = useState('All');
  const [filterStatus, setFilterStatus]         = useState('All');
  const [productsLoading, setProductsLoading]   = useState(false);
  const lastFetchRef = useRef(0);

  const [selectedItems, setSelectedItems] = useState([]);
  const [selectedCustomer, setSelectedCustomer] = useState(null);
  const [customerId, setCustomerId]             = useState('');
  const [paymentMethod, setPaymentMethod] = useState('Cash');
  const [discount, setDiscount]           = useState(0);
  const [remarks, setRemarks]             = useState('');

  const [hasExchange, setHasExchange]           = useState(false);
  const [exchangeMaterial, setExchangeMaterial] = useState('Gold');
  const [exchangePurity, setExchangePurity]     = useState('22K');
  const [exchangeLoss, setExchangeLoss]         = useState(LOSS_FIXED.Gold);

  const [exchangePreview, setExchangePreview]               = useState(null);
  const [exchangePreviewLoading, setExchangePreviewLoading] = useState(false);
  const [exchangePreviewError, setExchangePreviewError]     = useState('');

  const [loading, setLoading]     = useState(false);
  const [error, setError]         = useState('');
  const [success, setSuccess]     = useState('');
  const [invoiceNo, setInvoiceNo] = useState('');

  const [serverHistory, setServerHistory]   = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError]     = useState('');
  const [searchInvoice, setSearchInvoice]   = useState('');
  const [invoiceLoading, setInvoiceLoading] = useState(false);
  const [invoiceError, setInvoiceError]     = useState('');

  // ── Auto-lock exchange material, purity and loss from cart product ──────────
  useEffect(() => {
    if (!hasExchange || selectedItems.length === 0) return;

    const first    = selectedItems[0];
    const mat      = first.baseMaterial ?? 'Gold';
    const purity   = first.purity       ?? '';
    const fixedLoss = LOSS_FIXED[mat]   ?? LOSS_FIXED.Gold;

    setExchangeMaterial(mat);
    setExchangePurity(purity);
    setExchangeLoss(fixedLoss);
  }, [hasExchange, selectedItems]);

  // ── When exchange is toggled off + cart cleared, reset exchange state ───────
  useEffect(() => {
    if (selectedItems.length === 0) {
      setExchangePreview(null);
      setExchangePreviewError('');
    }
  }, [selectedItems]);

  // ── Fetch products ──────────────────────────────────────────────────────────
  const fetchProducts = useCallback(async (force = false) => {
    const now = Date.now();
    if (!force && now - lastFetchRef.current < 60_000) return;
    lastFetchRef.current = now;
    setProductsLoading(true);
    try {
      const token = localStorage.getItem('token');
      const res   = await fetch(`${API_URL}/Products`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      setProducts(data || []);
    } catch {
      setError('Failed to load products');
    } finally {
      setProductsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchProducts(true);
    const onVisible = () => {
      if (document.visibilityState === 'visible') fetchProducts();
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => document.removeEventListener('visibilitychange', onVisible);
  }, [fetchProducts]);

  // ── Filter products ─────────────────────────────────────────────────────────
  const filterProducts = useCallback(() => {
    let result = products;
    if (selectedCategory !== 'All')
      result = result.filter(p => p.category?.toLowerCase() === selectedCategory.toLowerCase());
    if (filterMaterial !== 'All')
      result = result.filter(p => p.baseMaterial?.toLowerCase() === filterMaterial.toLowerCase());
    if (filterPurity !== 'All')
      result = result.filter(p => p.purity === filterPurity);
    if (filterStatus !== 'All')
      result = result.filter(p => p.status?.toLowerCase() === filterStatus.toLowerCase());
    if (searchSku.trim())
      result = result.filter(p =>
        p.sku?.toLowerCase().includes(searchSku.toLowerCase()) ||
        p.name?.toLowerCase().includes(searchSku.toLowerCase()));
    setFilteredProducts(result);
  }, [searchSku, selectedCategory, filterMaterial, filterPurity, filterStatus, products]);

  useEffect(() => { filterProducts(); }, [filterProducts]);

  // ── Exchange preview API call ───────────────────────────────────────────────
  useEffect(() => {
    if (!hasExchange || selectedItems.length === 0) {
      setExchangePreview(null);
      setExchangePreviewError('');
      return;
    }
    const timer = setTimeout(async () => {
      setExchangePreviewLoading(true);
      setExchangePreviewError('');
      try {
        const token = localStorage.getItem('token');
        const ids   = selectedItems.map(i => i.id).join(',');
        const res   = await fetch(
          `${API_URL}/Sales/exchange-requirement?productIds=${ids}&lossPercentage=${exchangeLoss}`,
          { headers: { 'Authorization': `Bearer ${token}` } }
        );
        const json = await res.json();
        if (!res.ok) throw new Error(json.message || 'Calculation failed');
        setExchangePreview(json.data ?? json);
      } catch (err) {
        setExchangePreviewError(err.message);
        setExchangePreview(null);
      } finally {
        setExchangePreviewLoading(false);
      }
    }, 400);
    return () => clearTimeout(timer);
  }, [hasExchange, selectedItems, exchangeLoss]);

  // ── Handlers ────────────────────────────────────────────────────────────────
  const handleMaterialFilterChange = (mat) => { setFilterMaterial(mat); setFilterPurity('All'); };

  const handleTabChange = (tab) => {
    setActiveTab(tab);
    if (tab === 'history') loadHistory();
  };

  const loadHistory = async () => {
    setHistoryLoading(true);
    setHistoryError('');
    try {
      const data = await fetchRecentSales();
      setServerHistory(data);
    } catch {
      const local = JSON.parse(localStorage.getItem('sale_history') || '[]');
      setServerHistory(local.map(l => ({
        invoiceNo: l.invoiceNo, saleDate: l.createdAt,
        customerName: '—', netPayable: null, status: '—',
      })));
      setHistoryError('Could not reach server. Showing local history only.');
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleCustomerSelect = (c) => { setSelectedCustomer(c); setCustomerId(c.id); setError(''); };
  const handleCustomerClear  = ()  => { setSelectedCustomer(null); setCustomerId(''); };

  const addProduct = (p) => {
    if (selectedItems.length > 0) {
      const first = selectedItems[0];
      if (first.baseMaterial?.toLowerCase() !== p.baseMaterial?.toLowerCase()) {
        setError(
          `Mixed materials not allowed. Cart contains ${first.baseMaterial}. ` +
          `Cannot add ${p.baseMaterial}. Only same-material products can be sold together.`
        );
        return;
      }
      if (hasExchange && first.purity !== p.purity) {
        setError(
          `Exchange allows one purity at a time. Cart has ${first.purity}. ` +
          `Cannot add ${p.purity}. All exchange products must share the same purity.`
        );
        return;
      }
    }
    setError('');
    setSelectedItems(prev => [...prev, { ...p, key: Date.now() }]);
  };

  const removeProduct = (key) => {
    setSelectedItems(prev => {
      const updated = prev.filter(i => i.key !== key);
      if (updated.length === 0) setError('');
      return updated;
    });
  };

  const handleCheckout = async () => {
    if (!customerId)                { setError('Please select a customer');           return; }
    if (selectedItems.length === 0) { setError('Please select at least one product'); return; }
    if (hasExchange && !exchangePreview) {
      setError('Wait for exchange calculation to complete');
      return;
    }
    setLoading(true); setError(''); setSuccess('');
    try {
      const inv = await processSale(
        selectedItems, customerId, paymentMethod, discount,
        hasExchange,
        hasExchange && exchangePreview ? {
          material:       exchangeMaterial,
          purity:         exchangePurity,
          receivedWeight: exchangePreview.requiredGoldWeight,
          lossPercentage: exchangeLoss
        } : null,
        remarks
      );
      setInvoiceNo(inv);
      setSuccess(`✓ Sale completed! Invoice: ${inv}`);
      setSelectedItems([]);
      setSelectedCustomer(null); setCustomerId('');
      setDiscount(0); setRemarks('');
      setHasExchange(false);
      setExchangePreview(null);
    } catch (err) {
      setError(err.message || 'Checkout failed');
    } finally {
      setLoading(false);
    }
  };

  const handleInvoiceSearch = async () => {
    if (!searchInvoice.trim()) return;
    setInvoiceLoading(true); setInvoiceError('');
    try { await downloadPdf(searchInvoice.trim()); }
    catch { setInvoiceError(`Invoice "${searchInvoice}" not found.`); }
    finally { setInvoiceLoading(false); }
  };

  // Whether cart has items — used for locking exchange fields
  const cartLocked = selectedItems.length > 0;

  return (
    <div data-testid="sales-page" className="min-h-screen bg-slate-50 p-8">

      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold">Sales Checkout</h1>
        <button
          data-testid="sales-back-btn"
          onClick={onDashboardClick}
          className="px-4 py-2 bg-slate-600 text-white rounded-lg hover:bg-slate-700">
          ← Back
        </button>
      </div>

      {/* ── Tab switcher ── */}
      <div className="flex gap-2 mb-6 bg-white rounded-xl p-1 border border-slate-200 w-fit">
        <button
          data-testid="sales-tab-new"
          onClick={() => handleTabChange('new')}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all
            ${activeTab === 'new' ? 'bg-amber-600 text-white' : 'text-slate-500 hover:text-slate-700'}`}>
          <Plus className="w-4 h-4" /> New Sale
        </button>
        <button
          data-testid="sales-tab-history"
          onClick={() => handleTabChange('history')}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all
            ${activeTab === 'history' ? 'bg-amber-600 text-white' : 'text-slate-500 hover:text-slate-700'}`}>
          <History className="w-4 h-4" /> Invoice Lookup
        </button>
      </div>

      {/* ══ HISTORY TAB ════════════════════════════════════════════════════ */}
      {activeTab === 'history' && (
        <div className="max-w-2xl mx-auto space-y-5">
          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 space-y-4">
            <h2 className="font-bold text-slate-800 flex items-center gap-2">
              <Search className="w-4 h-4 text-amber-600" /> Search by Invoice Number
            </h2>
            <div className="flex gap-2">
              <input
                type="text"
                id="invoice-search"
                name="invoiceSearch"
                data-testid="sales-invoice-search"
                placeholder="e.g. INV-20260227034523"
                value={searchInvoice}
                onChange={e => setSearchInvoice(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleInvoiceSearch()}
                className="flex-1 px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
              />
              <button
                data-testid="sales-invoice-download-btn"
                onClick={handleInvoiceSearch}
                disabled={invoiceLoading || !searchInvoice.trim()}
                className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 text-sm font-medium flex items-center gap-2">
                <Download className="w-4 h-4" />
                {invoiceLoading ? 'Searching...' : 'Download'}
              </button>
            </div>
            {invoiceError && (
              <p data-testid="sales-invoice-error" className="text-sm text-red-600 flex items-center gap-1">
                <AlertCircle className="w-4 h-4" /> {invoiceError}
              </p>
            )}
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-bold text-slate-800">
                Sales History
                {!historyLoading && serverHistory.length > 0 && (
                  <span className="text-xs text-slate-400 font-normal ml-2">({serverHistory.length} records)</span>
                )}
              </h2>
              <button
                data-testid="sales-history-refresh"
                onClick={loadHistory}
                className="text-xs text-amber-600 hover:underline flex items-center gap-1">
                <RefreshCw className="w-3 h-3" /> Refresh
              </button>
            </div>
            {historyError && <p className="text-xs text-amber-600 mb-3">{historyError}</p>}
            {historyLoading ? (
              <div className="flex items-center justify-center py-10 gap-2 text-slate-400">
                <Loader2 className="w-5 h-5 animate-spin" /><span className="text-sm">Loading...</span>
              </div>
            ) : serverHistory.length === 0 ? (
              <p className="text-slate-400 text-sm text-center py-8">No sales recorded yet.</p>
            ) : (
              <div data-testid="sales-history-list" className="space-y-2">
                {serverHistory.map((item, i) => (
                  <div
                    key={item.invoiceNo || i}
                    data-testid={`sales-history-item-${item.invoiceNo}`}
                    className="flex items-center justify-between p-3 bg-slate-50 rounded-lg hover:bg-amber-50 transition-colors">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <p className="font-medium text-sm text-slate-800">{item.invoiceNo}</p>
                        {item.status && item.status !== '—' && (
                          <span className="text-xs px-2 py-0.5 bg-green-100 text-green-700 rounded-full">{item.status}</span>
                        )}
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">{item.customerName}</p>
                      <div className="flex items-center gap-3 mt-0.5">
                        {item.saleDate && (
                          <p className="text-xs text-slate-400">
                            {new Date(item.saleDate).toLocaleString('en-BD', {
                              day: '2-digit', month: 'short', year: 'numeric',
                              hour: '2-digit', minute: '2-digit'
                            })}
                          </p>
                        )}
                        {item.netPayable != null && (
                          <p className="text-xs font-semibold text-emerald-600">
                            ৳{Number(item.netPayable).toLocaleString('en-BD', { minimumFractionDigits: 2 })}
                          </p>
                        )}
                      </div>
                    </div>
                    <button
                      data-testid={`sales-history-download-${item.invoiceNo}`}
                      onClick={() => downloadPdf(item.invoiceNo)}
                      className="ml-3 flex-shrink-0 flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-amber-700 bg-amber-100 rounded-lg hover:bg-amber-200 transition-colors">
                      <Download className="w-3.5 h-3.5" /> Invoice
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ══ NEW SALE TAB ══════════════════════════════════════════════════ */}
      {activeTab === 'new' && (
        <>
          {error && (
            <div data-testid="sales-error" className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg flex gap-3">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <p className="text-red-700">{error}</p>
            </div>
          )}
          {success && (
            <div data-testid="sales-success" className="mb-6 p-4 bg-green-50 border border-green-200 rounded-lg flex items-start justify-between gap-4">
              <div>
                <p className="text-green-700 font-semibold mb-3">{success}</p>
                <button
                  data-testid="sales-download-invoice-btn"
                  onClick={() => downloadPdf(invoiceNo)}
                  className="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-700 flex items-center gap-2">
                  <Download className="w-4 h-4" /> Download Invoice
                </button>
              </div>
              <button
                data-testid="sales-success-close"
                onClick={() => setSuccess('')}
                className="text-green-400 hover:text-green-700 flex-shrink-0 text-xl leading-none font-bold">
                ×
              </button>
            </div>
          )}

          <div className="grid grid-cols-3 gap-8">

            {/* ── Product list ──────────────────────────────────────────── */}
            <div className="col-span-2">
              <div className="mb-3 flex gap-2">
                <div className="relative flex-1">
                  <Search className="absolute left-3 top-3 w-5 h-5 text-slate-400" />
                  <input
                    type="text"
                    id="sku-search"
                    name="skuSearch"
                    data-testid="sales-sku-search"
                    placeholder="Search by SKU or name..."
                    value={searchSku}
                    onChange={e => setSearchSku(e.target.value)}
                    className="w-full pl-10 pr-4 py-2 border border-slate-200 rounded-lg"
                  />
                </div>
                <button
                  data-testid="sales-products-refresh"
                  onClick={() => fetchProducts(true)}
                  title="Refresh products"
                  className="px-3 py-2 bg-white border border-slate-200 rounded-lg hover:bg-slate-50 text-slate-500">
                  <RefreshCw className={`w-4 h-4 ${productsLoading ? 'animate-spin' : ''}`} />
                </button>
              </div>

              {/* Category pills */}
              <div data-testid="sales-category-filters" className="flex gap-2 flex-wrap mb-3">
                {CATEGORIES.map(cat => (
                  <button
                    key={cat}
                    data-testid={`sales-category-${cat.toLowerCase()}`}
                    onClick={() => setSelectedCategory(cat)}
                    className={`px-3 py-1 rounded-full text-xs font-medium border transition-all
                      ${selectedCategory === cat ? 'bg-amber-600 text-white border-amber-600' : 'bg-white text-slate-500 border-slate-200 hover:border-amber-400'}`}>
                    {cat}
                  </button>
                ))}
              </div>

              {/* Material pills */}
              <div data-testid="sales-material-filters" className="flex gap-2 flex-wrap mb-2">
                {['All', ...MATERIALS].map(mat => (
                  <button
                    key={mat}
                    data-testid={`sales-material-${mat.toLowerCase()}`}
                    onClick={() => handleMaterialFilterChange(mat)}
                    className={`px-3 py-1 rounded-full text-xs font-medium border transition-all
                      ${filterMaterial === mat ? 'bg-slate-700 text-white border-slate-700' : 'bg-white text-slate-500 border-slate-200 hover:border-slate-400'}`}>
                    {mat}
                  </button>
                ))}
              </div>

              {/* Purity pills */}
              {filterMaterial !== 'All' && (
                <div data-testid="sales-purity-filters" className="flex gap-2 flex-wrap mb-2">
                  {PURITY_LABELS[filterMaterial]?.map(pur => (
                    <button
                      key={pur}
                      data-testid={`sales-purity-${pur.toLowerCase()}`}
                      onClick={() => setFilterPurity(pur)}
                      className={`px-3 py-1 rounded-full text-xs font-medium border transition-all
                        ${filterPurity === pur ? 'bg-amber-100 text-amber-700 border-amber-400' : 'bg-white text-slate-500 border-slate-200 hover:border-amber-300'}`}>
                      {pur}
                    </button>
                  ))}
                </div>
              )}

              {/* Status pills */}
              <div data-testid="sales-status-filters" className="flex gap-2 flex-wrap mb-3">
                {STATUSES.map(s => (
                  <button
                    key={s}
                    data-testid={`sales-status-${s.toLowerCase()}`}
                    onClick={() => setFilterStatus(s)}
                    className={`px-3 py-1 rounded-full text-xs font-medium border transition-all
                      ${filterStatus === s
                        ? s === 'Available'       ? 'bg-green-600 text-white border-green-600'
                        : s === 'Sold'            ? 'bg-red-500 text-white border-red-500'
                        : s === 'Reserved'        ? 'bg-blue-500 text-white border-blue-500'
                        : s === 'Out_at_Workshop' ? 'bg-purple-500 text-white border-purple-500'
                        :                          'bg-slate-700 text-white border-slate-700'
                        : 'bg-white text-slate-500 border-slate-200 hover:border-slate-400'}`}>
                    {s.replace('_', ' ')}
                  </button>
                ))}
              </div>

              {/* ── Product cards ──────────────────────────────────────── */}
              <div data-testid="sales-product-list" className="bg-white rounded-lg shadow max-h-[560px] overflow-y-auto">
                {filteredProducts.length === 0 ? (
                  <p className="p-4 text-slate-500">No products found</p>
                ) : (
                  filteredProducts.map(product => {
                    const isAvailable = product.status?.toLowerCase() === 'available';
                    return (
                      <div
                        key={product.id}
                        data-testid={`sales-product-${product.id}`}
                        className={`border-b p-4 flex justify-between items-start transition-colors
                          ${isAvailable ? 'hover:bg-slate-50' : 'bg-slate-50 opacity-60'}`}>
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-2 flex-wrap mb-1">
                            <h3 className="font-semibold text-slate-800">{product.name}</h3>
                            {isAvailable ? (
                              <span className="flex items-center gap-0.5 text-xs px-2 py-0.5 bg-green-100 text-green-700 rounded-full">
                                <CheckCircle className="w-3 h-3" /> Available
                              </span>
                            ) : (
                              <span className="flex items-center gap-0.5 text-xs px-2 py-0.5 bg-red-100 text-red-600 rounded-full">
                                <XCircle className="w-3 h-3" /> {product.status?.replace('_', ' ')}
                              </span>
                            )}
                            {product.isHallmarked && (
                              <span className="flex items-center gap-0.5 text-xs px-2 py-0.5 bg-blue-100 text-blue-700 rounded-full">
                                <Award className="w-3 h-3" /> Hallmarked
                              </span>
                            )}
                            <MaterialBadge material={product.baseMaterial} />
                            {product.purity && (
                              <span className="text-xs px-2 py-0.5 bg-amber-100 text-amber-700 rounded-full">
                                {product.purity}
                              </span>
                            )}
                            {product.category && (
                              <span className="text-xs px-2 py-0.5 bg-slate-100 text-slate-500 rounded-full">
                                {product.category}
                              </span>
                            )}
                          </div>
                          <p className="text-sm text-slate-600">
                            SKU: <span className="font-medium">{product.sku}</span>
                            {' · '}Gross: <span className="font-medium">{product.grossWeight}g</span>
                            {product.netWeight != null && (
                              <span> · Net: <span className="font-medium">{product.netWeight}g</span></span>
                            )}
                          </p>
                          <div className="flex items-center gap-3 mt-0.5 text-sm text-slate-600">
                            <span>Making: <span className="font-medium">৳{product.makingCharge}</span></span>
                            {product.workshopWastagePercentage != null && (
                              <span className="text-xs text-slate-400">
                                Wastage: {product.workshopWastagePercentage}%
                              </span>
                            )}
                          </div>
                        </div>
                        <button
                          data-testid={`sales-add-product-${product.id}`}
                          onClick={() => isAvailable && addProduct(product)}
                          disabled={!isAvailable}
                          title={isAvailable ? 'Add to cart' : `Not available (${product.status})`}
                          className="ml-3 flex-shrink-0 px-3 py-1.5 bg-amber-600 text-white rounded hover:bg-amber-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
                          <Plus className="w-4 h-4" />
                        </button>
                      </div>
                    );
                  })
                )}
              </div>
            </div>

            {/* ── Cart panel ─────────────────────────────────────────────── */}
            <div className="col-span-1">
              <div className="bg-white rounded-lg shadow p-6 sticky top-8 space-y-4">

                <div className="flex items-center justify-between">
                  <h2 data-testid="sales-cart-count" className="text-lg font-bold">
                    Cart ({selectedItems.length})
                  </h2>
                  {selectedItems.length > 0 && (
                    <span
                      data-testid="sales-cart-material-badge"
                      className={`text-xs px-2 py-1 rounded-full font-semibold
                        ${selectedItems[0].baseMaterial?.toLowerCase() === 'gold'     ? 'bg-yellow-100 text-yellow-700'
                        : selectedItems[0].baseMaterial?.toLowerCase() === 'silver'   ? 'bg-slate-200 text-slate-600'
                        : selectedItems[0].baseMaterial?.toLowerCase() === 'platinum' ? 'bg-indigo-100 text-indigo-700'
                        : 'bg-slate-100 text-slate-500'}`}>
                      {selectedItems[0].baseMaterial} only
                    </span>
                  )}
                </div>

                <div data-testid="sales-cart-items" className="max-h-48 overflow-y-auto border-b pb-4 space-y-2">
                  {selectedItems.length === 0
                    ? <p className="text-sm text-slate-400">No items added</p>
                    : selectedItems.map(item => (
                        <div
                          key={item.key}
                          data-testid={`sales-cart-item-${item.id}`}
                          className="flex justify-between items-start p-2 bg-slate-50 rounded-lg border border-slate-100">
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-1.5 flex-wrap">
                              <p className="text-sm font-semibold text-slate-800 truncate">{item.name}</p>
                              <MaterialBadge material={item.baseMaterial} />
                              {item.purity && (
                                <span className="text-xs px-1.5 py-0.5 bg-amber-100 text-amber-700 rounded">
                                  {item.purity}
                                </span>
                              )}
                              {item.isHallmarked && (
                                <span className="text-xs px-1.5 py-0.5 bg-blue-100 text-blue-600 rounded">HM</span>
                              )}
                            </div>
                            <div className="flex items-center gap-2 mt-0.5 text-xs text-slate-500">
                              <span>SKU: {item.sku}</span>
                              <span>·</span>
                              <span>Gross: {item.grossWeight}g</span>
                              {item.netWeight != null && <span>· Net: {item.netWeight}g</span>}
                            </div>
                            <p className="text-xs text-slate-500 mt-0.5">
                              Making: <span className="font-medium text-slate-700">৳{item.makingCharge}</span>
                            </p>
                          </div>
                          <button
                            data-testid={`sales-remove-item-${item.id}`}
                            onClick={() => removeProduct(item.key)}
                            className="ml-2 flex-shrink-0 text-red-400 hover:text-red-600 hover:bg-red-50 rounded p-0.5 transition-colors">
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      ))
                  }
                </div>

                {/* Customer */}
                <div>
                  <label htmlFor="customer-search" className="text-sm font-semibold text-slate-700 block mb-1">
                    Customer <span className="text-red-500">*</span>
                  </label>
                  <CustomerSearchInput
                    onSelect={handleCustomerSelect}
                    selectedCustomer={selectedCustomer}
                    onClear={handleCustomerClear}
                  />
                  <p className="text-xs text-slate-400 mt-1">Search by name, phone or NID</p>
                </div>

                {/* Discount */}
                <div>
                  <label htmlFor="discount" className="text-sm font-medium text-slate-700 block mb-1">
                    Discount %
                  </label>
                  <input
                    type="number"
                    id="discount"
                    name="discount"
                    data-testid="sales-discount"
                    value={discount}
                    min="0"
                    max="10"
                    onChange={e => setDiscount(Number(e.target.value))}
                    className="w-full px-3 py-2 border border-slate-200 rounded text-sm"
                  />
                </div>

                {/* Payment */}
                <div>
                  <label htmlFor="payment-method" className="text-sm font-medium text-slate-700 block mb-1">
                    Payment Method
                  </label>
                  <select
                    id="payment-method"
                    name="paymentMethod"
                    data-testid="sales-payment-method"
                    value={paymentMethod}
                    onChange={e => setPaymentMethod(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded text-sm">
                    <option value="Cash">Cash</option>
                    <option value="Card">Card</option>
                    <option value="Check">Check</option>
                  </select>
                </div>

                {/* Remarks */}
                <div>
                  <label htmlFor="remarks" className="text-sm font-medium text-slate-700 block mb-1">
                    Remarks
                  </label>
                  <input
                    type="text"
                    id="remarks"
                    name="remarks"
                    data-testid="sales-remarks"
                    placeholder="Remarks (optional)"
                    value={remarks}
                    onChange={e => setRemarks(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded text-sm"
                  />
                </div>

                {/* Exchange toggle */}
                <div className="border-t pt-4">
                  <label htmlFor="has-exchange" className="flex items-center gap-2 text-sm font-medium cursor-pointer">
                    <input
                      type="checkbox"
                      id="has-exchange"
                      name="hasExchange"
                      data-testid="sales-exchange-toggle"
                      checked={hasExchange}
                      onChange={e => {
                        setHasExchange(e.target.checked);
                        if (!e.target.checked) {
                          setExchangePreview(null);
                          setExchangePreviewError('');
                        }
                      }}
                      className="accent-amber-600"
                    />
                    Has Exchange (old gold/silver)
                  </label>
                </div>

                {/* Exchange section */}
                {hasExchange && (
                  <div data-testid="sales-exchange-section" className="space-y-3 p-3 bg-amber-50 rounded-lg border border-amber-200">
                    <div className="flex items-center justify-between">
                      <p className="text-xs font-semibold text-amber-700 uppercase tracking-wide">Exchange Details</p>
                      {cartLocked && (
                        <span className="flex items-center gap-1 text-xs text-green-700 font-medium">
                          <Lock className="w-3 h-3" /> Auto-set from cart
                        </span>
                      )}
                    </div>

                    {/* Exchange Material — locked when cart has items */}
                    <div>
                      <p className="text-xs text-slate-500 mb-1">Material</p>
                      <div data-testid="sales-exchange-material-group" className="flex gap-1.5">
                        {MATERIALS.map(m => (
                          <button
                            key={m}
                            data-testid={`sales-exchange-material-${m.toLowerCase()}`}
                            disabled={cartLocked}
                            onClick={() => !cartLocked && setExchangeMaterial(m)}
                            className={`flex-1 py-1.5 rounded-lg border text-xs font-medium transition-all
                              ${exchangeMaterial === m
                                ? 'bg-amber-600 text-white border-amber-600'
                                : 'bg-white text-slate-600 border-slate-200'}
                              ${cartLocked ? 'opacity-70 cursor-not-allowed' : 'hover:border-amber-400'}`}>
                            {exchangeMaterial === m && cartLocked
                              ? <span className="flex items-center justify-center gap-1">{m} <Lock className="w-2.5 h-2.5" /></span>
                              : m}
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Exchange Purity — locked when cart has items */}
                    <div>
                      <p className="text-xs text-slate-500 mb-1">Purity</p>
                      <div data-testid="sales-exchange-purity-group" className="flex gap-1.5 flex-wrap">
                        {(PURITY_LABELS[exchangeMaterial] ?? []).filter(p => p !== 'All').map(p => (
                          <button
                            key={p}
                            data-testid={`sales-exchange-purity-${p.toLowerCase()}`}
                            disabled={cartLocked}
                            onClick={() => !cartLocked && setExchangePurity(p)}
                            className={`px-3 py-1 rounded-lg border text-xs font-medium transition-all
                              ${exchangePurity === p
                                ? 'bg-amber-100 text-amber-700 border-amber-400'
                                : 'bg-white text-slate-500 border-slate-200'}
                              ${cartLocked ? 'opacity-70 cursor-not-allowed' : 'hover:border-amber-300'}`}>
                            {exchangePurity === p && cartLocked
                              ? <span className="flex items-center gap-1">{p} <Lock className="w-2.5 h-2.5" /></span>
                              : p}
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Weight — always locked / read-only */}
                    <div>
                      <div className="flex items-center justify-between mb-1">
                        <p className="text-xs text-slate-500">Weight received (grams)</p>
                        {exchangePreview && (
                          <span className="flex items-center gap-0.5 text-xs text-green-700 font-medium">
                            <Lock className="w-3 h-3" /> Locked
                          </span>
                        )}
                      </div>
                      <div
                        data-testid="sales-exchange-weight-display"
                        className={`w-full px-2 py-1.5 border rounded text-sm flex items-center justify-between
                          ${exchangePreview ? 'bg-green-50 border-green-300 text-green-800 font-semibold' : 'bg-slate-50 border-slate-200 text-slate-400'}`}>
                        <span>
                          {exchangePreview
                            ? `${exchangePreview.requiredGoldWeight} g`
                            : exchangePreviewLoading ? 'Calculating...' : 'Add products to cart first'}
                        </span>
                        {exchangePreview && <Lock className="w-3.5 h-3.5 text-green-600" />}
                      </div>
                      <p className="text-xs text-slate-400 mt-1">
                        Exact weight is calculated from product weight + loss%. Customer gives this amount only.
                      </p>
                    </div>

                    {/* Loss % — fixed, read-only, derived from material */}
                    <div>
                      <div className="flex items-center justify-between mb-1">
                        <label htmlFor="exchange-loss" className="text-xs text-slate-500">Loss %</label>
                        <span className="flex items-center gap-1 text-xs text-green-700 font-medium">
                          <Lock className="w-3 h-3" /> Fixed
                        </span>
                      </div>
                      <div
                        data-testid="sales-exchange-loss"
                        className="w-full px-2 py-1.5 border border-green-300 rounded text-sm bg-green-50 text-green-800 font-semibold flex items-center justify-between">
                        <span>{exchangeLoss}%</span>
                        <Lock className="w-3.5 h-3.5 text-green-600" />
                      </div>
                      <p className="text-xs text-slate-400 mt-1">
                        Loss % is fixed based on material ({exchangeMaterial}: {LOSS_FIXED[exchangeMaterial] ?? exchangeLoss}%).
                      </p>
                    </div>

                    <ExchangePreviewCard
                      data={exchangePreview}
                      loading={exchangePreviewLoading}
                      error={exchangePreviewError}
                    />
                  </div>
                )}

                <button
                  data-testid="sales-checkout-btn"
                  onClick={handleCheckout}
                  disabled={loading || selectedItems.length === 0 || !customerId ||
                    (hasExchange && !exchangePreview)}
                  className="w-full py-2.5 bg-amber-600 text-white font-bold rounded-lg hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
                  {loading ? 'Processing...' : 'Complete Sale'}
                </button>

                {hasExchange && !exchangePreview && !exchangePreviewLoading && selectedItems.length > 0 && (
                  <p className="text-xs text-center text-amber-600">
                    Exchange calculation required before checkout
                  </p>
                )}
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}