import  { useState, useEffect, useCallback } from 'react';
import { Plus, Trash2, AlertCircle, Search, Download } from 'lucide-react';
import { processSale, downloadPdf } from '../services/salesService';

export default function SalesPage({ onDashboardClick }) {
  const [products, setProducts] = useState([]);
  const [filteredProducts, setFilteredProducts] = useState([]);
  const [selectedItems, setSelectedItems] = useState([]);
  const [customerId, setCustomerId] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('Cash');
  const [discount, setDiscount] = useState(0);
  const [searchSku, setSearchSku] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [invoiceNo, setInvoiceNo] = useState('');
  const [hasExchange, setHasExchange] = useState(false);
  const [exchangeMaterial, setExchangeMaterial] = useState('Gold');
  const [exchangePurity, setExchangePurity] = useState('22K');
  const [exchangeWeight, setExchangeWeight] = useState('');
  const [exchangeLoss, setExchangeLoss] = useState(10);

  const filterProducts = useCallback(() => {
    if (!searchSku.trim()) {
      setFilteredProducts(products);
      return;
    }
    const filtered = products.filter(p => 
      p.sku?.toLowerCase().includes(searchSku.toLowerCase()) ||
      p.name?.toLowerCase().includes(searchSku.toLowerCase())
    );
    setFilteredProducts(filtered);
  }, [searchSku, products]);

  useEffect(() => {
    filterProducts();
  }, [filterProducts]);

  useEffect(() => {
    fetchProducts();
  }, []);

  const fetchProducts = async () => {
    try {
      const token = localStorage.getItem('token');
      const response = await fetch('http://localhost:5284/api/Products', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await response.json();
      setProducts(data || []);
    } catch (err) {
      setError('Failed to load products');
    }
  };

  const addProduct = (product) => {
    setSelectedItems([...selectedItems, { ...product, key: Date.now() }]);
  };

  const removeProduct = (key) => {
    setSelectedItems(selectedItems.filter(item => item.key !== key));
  };

  const handleCheckout = async () => {
    if (!customerId) {
      setError('Please enter customer ID');
      return;
    }
    if (selectedItems.length === 0) {
      setError('Please select at least one product');
      return;
    }
    if (hasExchange && !exchangeWeight) {
      setError('Please enter exchange weight');
      return;
    }

    setLoading(true);
    setError('');
    setSuccess('');

    try {
      const inv = await processSale(
        selectedItems,
        customerId,
        paymentMethod,
        discount,
        hasExchange,
        hasExchange ? {
          material: exchangeMaterial,
          purity: exchangePurity,
          receivedWeight: parseFloat(exchangeWeight),
          lossPercentage: exchangeLoss
        } : null
      );

      setInvoiceNo(inv);
      setSuccess(`✓ Sale completed! Invoice: ${inv}`);
      setSelectedItems([]);
      setCustomerId('');
      setDiscount(0);
    } catch (err) {
      setError(err.message || 'Checkout failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 p-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold">Sales Checkout</h1>
        <button
          onClick={onDashboardClick}
          className="px-4 py-2 bg-slate-600 text-white rounded-lg hover:bg-slate-700"
        >
          ← Back
        </button>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg flex gap-3">
          <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
          <p className="text-red-700">{error}</p>
        </div>
      )}

      {success && (
        <div className="mb-6 p-4 bg-green-50 border border-green-200 rounded-lg">
          <p className="text-green-700 font-semibold mb-3">{success}</p>
          <button
            onClick={() => downloadPdf(invoiceNo)}
            className="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-700 flex items-center gap-2"
          >
            <Download className="w-4 h-4" /> Download PDF
          </button>
        </div>
      )}

      <div className="grid grid-cols-3 gap-8">
        <div className="col-span-2">
          <div className="mb-4 relative">
            <Search className="absolute left-3 top-3 w-5 h-5 text-slate-400" />
            <input
              type="text"
              placeholder="Search by SKU..."
              value={searchSku}
              onChange={(e) => setSearchSku(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-slate-200 rounded-lg"
            />
          </div>
          <div className="bg-white rounded-lg shadow max-h-96 overflow-y-auto">
            {filteredProducts.length === 0 ? (
              <p className="p-4 text-slate-500">No products</p>
            ) : (
              filteredProducts.map(product => (
                <div key={product.id} className="border-b p-4 flex justify-between items-center">
                  <div>
                    <h3 className="font-semibold">{product.name}</h3>
                    <p className="text-sm text-slate-600">SKU: {product.sku} | Weight: {product.grossWeight}g</p>
                    <p className="text-sm text-slate-600">Making: ৳{product.makingCharge}</p>
                  </div>
                  <button
                    onClick={() => addProduct(product)}
                    className="px-3 py-1 bg-amber-600 text-white rounded hover:bg-amber-700"
                  >
                    <Plus className="w-4 h-4" />
                  </button>
                </div>
              ))
            )}
          </div>
        </div>

        <div className="col-span-1">
          <div className="bg-white rounded-lg shadow p-6 sticky top-8">
            <h2 className="text-lg font-bold mb-4">Cart ({selectedItems.length})</h2>

            <div className="space-y-2 max-h-32 overflow-y-auto mb-4 border-b pb-4">
              {selectedItems.map(item => (
                <div key={item.key} className="flex justify-between items-center text-sm">
                  <span>{item.name}</span>
                  <button onClick={() => removeProduct(item.key)} className="text-red-600">
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              ))}
            </div>

            <div className="space-y-3 mb-4">
              <input
                type="text"
                placeholder="Customer ID"
                value={customerId}
                onChange={(e) => setCustomerId(e.target.value)}
                className="w-full px-3 py-2 border border-slate-200 rounded text-sm"
              />

              <div>
                <label className="text-sm font-medium">Discount %</label>
                <input
                  type="number"
                  value={discount}
                  onChange={(e) => setDiscount(Number(e.target.value))}
                  min="0" max="10"
                  className="w-full px-3 py-2 border border-slate-200 rounded text-sm"
                />
              </div>

              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value)}
                className="w-full px-3 py-2 border border-slate-200 rounded text-sm"
              >
                <option value="Cash">Cash</option>
                <option value="Card">Card</option>
                <option value="Check">Check</option>
              </select>

              <div className="border-t pt-4">
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={hasExchange}
                    onChange={(e) => setHasExchange(e.target.checked)}
                  />
                  Has Exchange
                </label>
              </div>

              {hasExchange && (
                <div className="space-y-2 p-3 bg-amber-50 rounded">
                  <input
                    type="text"
                    placeholder="Material"
                    value={exchangeMaterial}
                    onChange={(e) => setExchangeMaterial(e.target.value)}
                    className="w-full px-2 py-1 border border-slate-200 rounded text-sm"
                  />
                  <input
                    type="text"
                    placeholder="Purity"
                    value={exchangePurity}
                    onChange={(e) => setExchangePurity(e.target.value)}
                    className="w-full px-2 py-1 border border-slate-200 rounded text-sm"
                  />
                  <input
                    type="number"
                    placeholder="Weight (grams)"
                    value={exchangeWeight}
                    onChange={(e) => setExchangeWeight(e.target.value)}
                    className="w-full px-2 py-1 border border-slate-200 rounded text-sm"
                  />
                  <input
                    type="number"
                    placeholder="Loss %"
                    value={exchangeLoss}
                    onChange={(e) => setExchangeLoss(Number(e.target.value))}
                    className="w-full px-2 py-1 border border-slate-200 rounded text-sm"
                  />
                </div>
              )}
            </div>

            <button
              onClick={handleCheckout}
              disabled={loading || selectedItems.length === 0}
              className="w-full py-2 bg-amber-600 text-white font-bold rounded hover:bg-amber-700 disabled:opacity-50"
            >
              {loading ? 'Processing...' : 'Complete Sale'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}