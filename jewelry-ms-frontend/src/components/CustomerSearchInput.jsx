// src/components/CustomerSearchInput.jsx
// Reusable customer search with live dropdown.
// Usage:
//   <CustomerSearchInput
//     onSelect={(customer) => setCustomerId(customer.id)}
//     selectedCustomer={selectedCustomer}
//     onClear={() => { setCustomerId(''); setSelectedCustomer(null); }}
//   />

import { useState, useEffect, useRef } from 'react';
import { Search, X, User, Phone, CreditCard } from 'lucide-react';
import { customerAPI } from '../services/api';

export default function CustomerSearchInput({ onSelect, selectedCustomer, onClear }) {
  const [query, setQuery]       = useState('');
  const [results, setResults]   = useState([]);
  const [loading, setLoading]   = useState(false);
  const [showDrop, setShowDrop] = useState(false);
  const debounceRef             = useRef(null);
  const wrapperRef              = useRef(null);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handler = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target))
        setShowDrop(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  // Debounced search — fires 350ms after user stops typing
  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (query.trim().length < 2) {
      setResults([]);
      setShowDrop(false);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      setLoading(true);
      try {
        const data = await customerAPI.search(query);
        setResults(data);
        setShowDrop(true);
      } catch {
        setResults([]);
      } finally {
        setLoading(false);
      }
    }, 350);
    return () => clearTimeout(debounceRef.current);
  }, [query]);

  const handleSelect = (customer) => {
    onSelect(customer);
    setQuery('');
    setResults([]);
    setShowDrop(false);
  };

  const handleClear = () => {
    setQuery('');
    setResults([]);
    setShowDrop(false);
    onClear();
  };

  // ── If a customer is already selected, show their card ───────────────────────
  if (selectedCustomer) {
    return (
      <div
        data-testid="customer-selected-card"
        className="flex items-start justify-between p-3 bg-amber-50 border border-amber-200 rounded-lg"
      >
        <div className="flex gap-2">
          <User className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
          <div className="text-sm">
            <p data-testid="customer-selected-name" className="font-semibold text-slate-800">
              {selectedCustomer.fullName}
            </p>
            {selectedCustomer.contactNumber && (
              <p data-testid="customer-selected-phone" className="text-slate-500 flex items-center gap-1">
                <Phone className="w-3 h-3" /> {selectedCustomer.contactNumber}
              </p>
            )}
            {selectedCustomer.nidNumber && (
              <p data-testid="customer-selected-nid" className="text-slate-500 flex items-center gap-1">
                <CreditCard className="w-3 h-3" /> NID: {selectedCustomer.nidNumber}
              </p>
            )}
          </div>
        </div>
        <button
          data-testid="customer-clear-btn"
          onClick={handleClear}
          className="text-slate-400 hover:text-red-500 transition-colors"
          title="Change customer"
        >
          <X className="w-4 h-4" />
        </button>
      </div>
    );
  }

  // ── Search input + dropdown ───────────────────────────────────────────────────
  return (
    <div ref={wrapperRef} className="relative">
      <div className="relative">
        <Search className="absolute left-3 top-2.5 w-4 h-4 text-slate-400" />
        <input
          type="text"
          id="customer-search"
          name="customerSearch"
          data-testid="customer-search-input"
          placeholder="Search by name, phone, or NID..."
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onFocus={() => results.length > 0 && setShowDrop(true)}
          className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
        />
        {loading && (
          <div
            data-testid="customer-search-loading"
            className="absolute right-3 top-2.5 w-4 h-4 border-2 border-amber-400 border-t-transparent rounded-full animate-spin"
          />
        )}
      </div>

      {showDrop && (
        <div
          data-testid="customer-dropdown"
          className="absolute z-50 mt-1 w-full bg-white border border-slate-200 rounded-lg shadow-lg max-h-56 overflow-y-auto"
        >
          {results.length === 0 ? (
            <p data-testid="customer-no-results" className="p-3 text-sm text-slate-500 text-center">
              No customers found
            </p>
          ) : (
            results.map((c) => (
              <button
                key={c.id}
                data-testid={`customer-result-${c.id}`}
                onClick={() => handleSelect(c)}
                className="w-full text-left px-4 py-3 hover:bg-amber-50 border-b last:border-b-0 transition-colors"
              >
                <p className="font-semibold text-sm text-slate-800">{c.fullName}</p>
                <div className="flex gap-4 mt-0.5">
                  {c.contactNumber && (
                    <span className="text-xs text-slate-500 flex items-center gap-1">
                      <Phone className="w-3 h-3" /> {c.contactNumber}
                    </span>
                  )}
                  {c.nidNumber && (
                    <span className="text-xs text-slate-500 flex items-center gap-1">
                      <CreditCard className="w-3 h-3" /> {c.nidNumber}
                    </span>
                  )}
                </div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}