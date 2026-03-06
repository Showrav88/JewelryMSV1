// src/pages/PurchasePage.jsx
import { useState } from 'react';
import { Calculator, UserCheck, CheckCircle, AlertCircle, Download,
         ArrowLeft, ChevronRight, History, Search, Loader2, Scale } from 'lucide-react';
import CustomerSearchInput from '../components/CustomerSearchInput';
import { calculatePurchaseRate, createPurchase,
         downloadPurchaseReceipt, fetchRecentPurchases } from '../services/purchaseService';

const MATERIALS = ['Gold', 'Silver', 'Platinum'];
const PURITY_LABELS = {
  Gold:   ['24K', '22K', '21K', '18K', '14K'],
  Silver: ['999', '925', '750'],
  Platinum: ['950'],
};
const DEFAULT_STANDARD_PURITY = { Gold: 99.50, Silver: 99.50, Platinum: 95.00 };

function StepBadge({ number, label, active, done }) {
  return (
    <div className={`flex items-center gap-2 px-3 py-1.5 rounded-full text-sm font-medium transition-all
      ${done  ? 'bg-green-100 text-green-700'
      : active ? 'bg-amber-100 text-amber-700 ring-2 ring-amber-400'
               : 'bg-slate-100 text-slate-400'}`}>
      {done
        ? <CheckCircle className="w-4 h-4" />
        : <span className="w-5 h-5 rounded-full flex items-center justify-center text-xs text-white"
            style={{ background: active ? '#d97706' : '#94a3b8' }}>
            {number}
          </span>
      }
      {label}
    </div>
  );
}

function RateCard({ calc, form }) {
  return (
    <div className="bg-amber-50 border border-amber-200 rounded-xl p-5 space-y-3">
      <p className="text-xs font-bold text-amber-700 uppercase tracking-wider">Rate Preview</p>

      <div className="grid grid-cols-2 gap-y-2 text-sm">
        <span className="text-slate-500">Material</span>
        <span className="font-medium text-right">{form.baseMaterial} · {form.testedPurityLabel}</span>

        <span className="text-slate-500">Gross Weight</span>
        <span className="font-medium text-right">{form.grossWeight} g</span>

        {/* ── Net Weight — highlighted row ── */}
        <span className="text-slate-700 font-semibold flex items-center gap-1">
          <Scale className="w-3.5 h-3.5 text-amber-600" /> Net Weight
        </span>
        <span className="font-bold text-right text-amber-700">
          {calc.netWeight != null ? `${calc.netWeight} g` : '—'}
        </span>

        <span className="text-slate-500">Tested Purity</span>
        <span className="font-medium text-right">{form.testedPurity}%</span>

        <span className="text-slate-500">Standard Purity</span>
        <span className="font-medium text-right">{form.standardPurity}%</span>

        <span className="text-slate-500">Purity Difference</span>
        <span className={`font-medium text-right ${calc.purityDifference < 0 ? 'text-red-600' : 'text-green-600'}`}>
          {calc.purityDifference > 0 ? '+' : ''}{calc.purityDifference}%
        </span>

        <span className="text-slate-500">Rate / g</span>
        <span className="font-medium text-right">৳{calc.standardBuyingRatePerGram?.toLocaleString()}</span>
      </div>

      {/* Net weight explanation — dynamic by material */}
      <div className="bg-white border border-amber-100 rounded-lg px-3 py-2.5 space-y-1.5">
        <p className="text-xs font-semibold text-slate-600">Net Weight — What This Means</p>
        <div className="flex items-center gap-2 text-xs">
          <span className="px-2 py-0.5 bg-slate-100 rounded font-medium text-slate-600">
            {form.grossWeight} g (gross)
          </span>
          <span className="text-slate-300">→</span>
          <span className="px-2 py-0.5 bg-amber-100 rounded font-bold text-amber-700">
            {calc.netWeight} g (net)
          </span>
        </div>
        <p className="text-xs text-slate-500">
          If melted, your <span className="font-semibold text-slate-700">{form.grossWeight}g</span> of{' '}
          <span className="font-semibold text-slate-700">{form.testedPurityLabel}</span>{' '}
          ({form.testedPurity}% pure) yields{' '}
          <span className="font-bold text-amber-700">{calc.netWeight} g</span> of{' '}
          {form.baseMaterial === 'Platinum' ? 'pure Platinum' : form.baseMaterial === 'Silver' ? 'fine Silver' : 'fine Gold'}.
        </p>
        <p className="text-xs text-slate-400 italic">
          Net weight: ({form.testedPurity}% ÷ {form.standardPurity}%) × {form.grossWeight}g = {calc.netWeight}g
        </p>
      </div>

      <div className="border-t border-amber-200 pt-3 flex items-center justify-between">
        <span className="text-sm font-bold text-slate-700">Total to Pay Customer</span>
        <span className="text-2xl font-bold text-amber-700">
          ৳{calc.totalAmount?.toLocaleString('en-BD', { minimumFractionDigits: 2 })}
        </span>
      </div>

      <div className="bg-slate-50 rounded-lg px-3 py-2 space-y-0.5">
        <p className="text-xs font-semibold text-slate-500">Price Calculation</p>
        <p className="text-xs text-slate-400 italic">
          (৳{calc.standardBuyingRatePerGram} ÷ {form.standardPurity}%) × {form.testedPurity}% × {form.grossWeight}g
          {' = '}
          <span className="font-semibold text-amber-700">
            ৳{calc.totalAmount?.toLocaleString('en-BD', { minimumFractionDigits: 2 })}
          </span>
        </p>
      </div>
    </div>
  );
}

export default function PurchasePage({ onDashboardClick }) {
  const [activeTab, setActiveTab] = useState('new');
  const [step, setStep]           = useState(1);

  // Form
  const [baseMaterial, setBaseMaterial]             = useState('Gold');
  const [testedPurityLabel, setTestedPurityLabel]   = useState('22K');
  const [grossWeight, setGrossWeight]               = useState('');
  const [testedPurity, setTestedPurity]             = useState('');
  const [standardBuyingRate, setStandardBuyingRate] = useState('');
  const [standardPurity, setStandardPurity]         = useState(99.50);
  const [productDescription, setProductDescription] = useState('');

  // Customer
  const [selectedCustomer, setSelectedCustomer] = useState(null);
  const [customerId, setCustomerId]             = useState('');

  // Results
  const [calcResult, setCalcResult] = useState(null);
  const [receiptNo, setReceiptNo]   = useState('');

  // UI
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState('');
  const [success, setSuccess] = useState('');

  // History tab
  const [serverHistory, setServerHistory]   = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError]     = useState('');
  const [searchReceipt, setSearchReceipt]   = useState('');
  const [searchLoading, setSearchLoading]   = useState(false);
  const [searchError, setSearchError]       = useState('');

  const handleTabChange = async (tab) => {
    setActiveTab(tab);
    if (tab !== 'history') return;
    setHistoryLoading(true);
    setHistoryError('');
    try {
      const data = await fetchRecentPurchases();
      setServerHistory(data);
    } catch {
      const local = JSON.parse(localStorage.getItem('purchase_history') || '[]');
      setServerHistory(local.map(l => ({
        receiptNo:    l.receiptNo,
        createdAt:    l.createdAt,
        customerName: '—',
        baseMaterial: '—',
        totalAmount:  null,
        grossWeight:  null,
        netWeight:    null,
      })));
      setHistoryError('Could not reach server. Showing local history only.');
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleCalculate = async () => {
    if (!grossWeight || !testedPurity || !standardBuyingRate) {
      setError('Please fill in weight, tested purity and buying rate.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      const result = await calculatePurchaseRate({
        baseMaterial,
        grossWeight:               parseFloat(grossWeight),
        testedPurity:              parseFloat(testedPurity),
        testedPurityLabel,
        standardBuyingRatePerGram: parseFloat(standardBuyingRate),
        standardPurity:            parseFloat(standardPurity),
      });
      setCalcResult(result);
      setStep(2);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmCustomer = () => {
    if (!customerId) { setError('Please select a customer.'); return; }
    setError('');
    setStep(3);
  };

  const handleCreatePurchase = async () => {
    setLoading(true);
    setError('');
    try {
      const rNo = await createPurchase({
        customerId,
        baseMaterial,
        productDescription,
        grossWeight:               parseFloat(grossWeight),
        testedPurity:              parseFloat(testedPurity),
        testedPurityLabel,
        standardBuyingRatePerGram: parseFloat(standardBuyingRate),
        standardPurity:            parseFloat(standardPurity),
      });
      setReceiptNo(rNo);
      setSuccess(`✓ Purchase recorded! Receipt: ${rNo}`);
      setStep(4);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setStep(1);
    setCalcResult(null);
    setReceiptNo('');
    setSelectedCustomer(null);
    setCustomerId('');
    setGrossWeight('');
    setTestedPurity('');
    setStandardBuyingRate('');
    setProductDescription('');
    setError('');
    setSuccess('');
  };

  const handleReceiptSearch = async () => {
    if (!searchReceipt.trim()) return;
    setSearchLoading(true);
    setSearchError('');
    try {
      await downloadPurchaseReceipt(searchReceipt.trim());
    } catch {
      setSearchError(`Receipt "${searchReceipt}" not found.`);
    } finally {
      setSearchLoading(false);
    }
  };

  const handleMaterialChange = (mat) => {
    setBaseMaterial(mat);
    setTestedPurityLabel(PURITY_LABELS[mat][0]);
    setStandardPurity(DEFAULT_STANDARD_PURITY[mat]);
  };

  return (
    <div className="min-h-screen bg-slate-50 p-8">

      {/* Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Purchase Counter</h1>
          <p className="text-slate-500 text-sm mt-1">Buy gold & silver from customers</p>
        </div>
        <button onClick={onDashboardClick}
          className="flex items-center gap-2 px-4 py-2 bg-slate-600 text-white rounded-lg hover:bg-slate-700">
          <ArrowLeft className="w-4 h-4" /> Back
        </button>
      </div>

      {/* Tab switcher */}
      <div className="flex gap-2 mb-6 bg-white rounded-xl p-1 border border-slate-200 w-fit">
        <button onClick={() => handleTabChange('new')}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all
            ${activeTab === 'new' ? 'bg-amber-600 text-white' : 'text-slate-500 hover:text-slate-700'}`}>
          <Calculator className="w-4 h-4" /> New Purchase
        </button>
        <button onClick={() => handleTabChange('history')}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all
            ${activeTab === 'history' ? 'bg-amber-600 text-white' : 'text-slate-500 hover:text-slate-700'}`}>
          <History className="w-4 h-4" /> Receipt Lookup
        </button>
      </div>

      {error && activeTab === 'new' && (
        <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg flex gap-3 max-w-2xl mx-auto">
          <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0" />
          <p className="text-red-700 text-sm">{error}</p>
        </div>
      )}

      {/* ══ HISTORY TAB ════════════════════════════════════════════════════ */}
      {activeTab === 'history' && (
        <div className="max-w-2xl mx-auto space-y-5">

          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 space-y-4">
            <h2 className="font-bold text-slate-800 flex items-center gap-2">
              <Search className="w-4 h-4 text-amber-600" /> Search by Receipt Number
            </h2>
            <div className="flex gap-2">
              <input type="text" placeholder="e.g. PUR-20260302034520"
                value={searchReceipt}
                onChange={e => setSearchReceipt(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleReceiptSearch()}
                className="flex-1 px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              <button onClick={handleReceiptSearch}
                disabled={searchLoading || !searchReceipt.trim()}
                className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 text-sm font-medium flex items-center gap-2">
                <Download className="w-4 h-4" />
                {searchLoading ? 'Searching...' : 'Download'}
              </button>
            </div>
            {searchError && (
              <p className="text-sm text-red-600 flex items-center gap-1">
                <AlertCircle className="w-4 h-4" /> {searchError}
              </p>
            )}
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-bold text-slate-800">
                Purchase History
                {!historyLoading && serverHistory.length > 0 && (
                  <span className="text-xs text-slate-400 font-normal ml-2">
                    ({serverHistory.length} records)
                  </span>
                )}
              </h2>
              <button onClick={() => handleTabChange('history')}
                className="text-xs text-amber-600 hover:underline">Refresh</button>
            </div>

            {historyError && (
              <p className="text-xs text-amber-600 mb-3 flex items-center gap-1">
                <AlertCircle className="w-3 h-3" /> {historyError}
              </p>
            )}

            {historyLoading ? (
              <div className="flex items-center justify-center py-10 gap-2 text-slate-400">
                <Loader2 className="w-5 h-5 animate-spin" />
                <span className="text-sm">Loading history...</span>
              </div>
            ) : serverHistory.length === 0 ? (
              <p className="text-slate-400 text-sm text-center py-8">No purchases recorded yet.</p>
            ) : (
              <div className="space-y-2">
                {serverHistory.map((item, i) => (
                  <div key={item.receiptNo || i}
                    className="flex items-center justify-between p-3 bg-slate-50 rounded-lg hover:bg-amber-50 transition-colors">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <p className="font-medium text-sm text-slate-800">{item.receiptNo}</p>
                        {item.baseMaterial && item.baseMaterial !== '—' && (
                          <span className="text-xs px-2 py-0.5 bg-amber-100 text-amber-700 rounded-full">
                            {item.baseMaterial}
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">{item.customerName}</p>
                      <div className="flex items-center gap-3 mt-0.5 flex-wrap">
                        <p className="text-xs text-slate-400">
                          {new Date(item.createdAt).toLocaleString('en-BD', {
                            day: '2-digit', month: 'short', year: 'numeric',
                            hour: '2-digit', minute: '2-digit'
                          })}
                        </p>
                        {/* ── Gross + Net weight in history ── */}
                        {item.grossWeight != null && (
                          <p className="text-xs text-slate-400">
                            Gross: <span className="font-medium">{item.grossWeight}g</span>
                          </p>
                        )}
                        {item.netWeight != null && (
                          <p className="text-xs text-amber-600 font-semibold flex items-center gap-0.5">
                            <Scale className="w-3 h-3" /> Net: {item.netWeight}g
                          </p>
                        )}
                        {item.totalAmount != null && (
                          <p className="text-xs font-semibold text-emerald-600">
                            ৳{item.totalAmount.toLocaleString('en-BD', { minimumFractionDigits: 2 })}
                          </p>
                        )}
                      </div>
                    </div>
                    <button onClick={() => downloadPurchaseReceipt(item.receiptNo)}
                      className="ml-3 flex-shrink-0 flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-amber-700 bg-amber-100 rounded-lg hover:bg-amber-200 transition-colors">
                      <Download className="w-3.5 h-3.5" /> Receipt
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ══ NEW PURCHASE TAB ══════════════════════════════════════════════ */}
      {activeTab === 'new' && (
        <>
          <div className="flex items-center gap-2 mb-8">
            <StepBadge number="1" label="Calculate Rate" active={step === 1} done={step > 1} />
            <ChevronRight className="w-4 h-4 text-slate-300" />
            <StepBadge number="2" label="Select Customer" active={step === 2} done={step > 2} />
            <ChevronRight className="w-4 h-4 text-slate-300" />
            <StepBadge number="3" label="Confirm & Save" active={step === 3} done={step > 3} />
          </div>

          <div className="max-w-2xl mx-auto space-y-6">

            {/* ── STEP 1 ───────────────────────────────────────────────── */}
            {step === 1 && (
              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 space-y-5">
                <div className="flex items-center gap-2 mb-2">
                  <Calculator className="w-5 h-5 text-amber-600" />
                  <h2 className="font-bold text-slate-800">Item Details & Rate Calculation</h2>
                </div>

                <div>
                  <label className="text-sm font-medium text-slate-700 block mb-2">Material</label>
                  <div className="flex gap-2">
                    {MATERIALS.map(m => (
                      <button key={m} onClick={() => handleMaterialChange(m)}
                        className={`flex-1 py-2 rounded-lg border font-medium text-sm transition-all
                          ${baseMaterial === m
                            ? 'bg-amber-600 text-white border-amber-600'
                            : 'bg-white text-slate-600 border-slate-200 hover:border-amber-400'}`}>
                        {m}
                      </button>
                    ))}
                  </div>
                </div>

                <div>
                  <label className="text-sm font-medium text-slate-700 block mb-2">Purity Label</label>
                  <div className="flex gap-2 flex-wrap">
                    {PURITY_LABELS[baseMaterial].map(l => (
                      <button key={l} onClick={() => setTestedPurityLabel(l)}
                        className={`px-4 py-1.5 rounded-lg border text-sm font-medium transition-all
                          ${testedPurityLabel === l
                            ? 'bg-amber-100 text-amber-700 border-amber-400'
                            : 'bg-white text-slate-500 border-slate-200 hover:border-amber-300'}`}>
                        {l}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="text-sm font-medium text-slate-700 block mb-1">
                      Gross Weight (g) <span className="text-red-500">*</span>
                    </label>
                    <input type="number" step="0.001" placeholder="e.g. 10.500"
                      value={grossWeight} onChange={e => setGrossWeight(e.target.value)}
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                  </div>
                  <div>
                    <label className="text-sm font-medium text-slate-700 block mb-1">
                      Tested Purity (%) <span className="text-red-500">*</span>
                    </label>
                    <input type="number" step="0.001" placeholder="e.g. 91.600"
                      value={testedPurity} onChange={e => setTestedPurity(e.target.value)}
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                  </div>
                  <div>
                    <label className="text-sm font-medium text-slate-700 block mb-1">
                      Buying Rate / g (৳) <span className="text-red-500">*</span>
                    </label>
                    <input type="number" step="0.01" placeholder="e.g. 17500"
                      value={standardBuyingRate} onChange={e => setStandardBuyingRate(e.target.value)}
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                  </div>
                  <div>
                    <label className="text-sm font-medium text-slate-700 block mb-1">Standard Purity (%)</label>
                    <input type="number" step="0.01"
                      value={standardPurity} onChange={e => setStandardPurity(e.target.value)}
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                  </div>
                </div>

                <div>
                  <label className="text-sm font-medium text-slate-700 block mb-1">Description (optional)</label>
                  <input type="text" placeholder="e.g. Gold necklace, old bangle..."
                    value={productDescription} onChange={e => setProductDescription(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                </div>

                <button onClick={handleCalculate} disabled={loading}
                  className="w-full py-3 bg-amber-600 text-white font-bold rounded-xl hover:bg-amber-700 disabled:opacity-50 transition-colors flex items-center justify-center gap-2">
                  <Calculator className="w-4 h-4" />
                  {loading ? 'Calculating...' : 'Calculate Rate'}
                </button>
              </div>
            )}

            {/* ── STEP 2 ───────────────────────────────────────────────── */}
            {step === 2 && (
              <div className="space-y-4">
                {calcResult && (
                  <RateCard
                    calc={calcResult}
                    form={{ baseMaterial, testedPurityLabel, grossWeight, testedPurity, standardPurity }}
                  />
                )}
                <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 space-y-4">
                  <div className="flex items-center gap-2 mb-2">
                    <UserCheck className="w-5 h-5 text-amber-600" />
                    <h2 className="font-bold text-slate-800">Select Customer</h2>
                  </div>
                  <CustomerSearchInput
                    onSelect={(c) => { setSelectedCustomer(c); setCustomerId(c.id); setError(''); }}
                    selectedCustomer={selectedCustomer}
                    onClear={() => { setSelectedCustomer(null); setCustomerId(''); }}
                  />
                  <p className="text-xs text-slate-400">Search by name, phone number, or NID</p>
                  <div className="flex gap-3 pt-2">
                    <button onClick={() => { setStep(1); setError(''); }}
                      className="flex-1 py-2.5 border border-slate-200 text-slate-600 rounded-xl hover:bg-slate-50 text-sm font-medium">
                      ← Back
                    </button>
                    <button onClick={handleConfirmCustomer} disabled={!customerId}
                      className="flex-1 py-2.5 bg-amber-600 text-white font-bold rounded-xl hover:bg-amber-700 disabled:opacity-50 transition-colors text-sm">
                      Continue →
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* ── STEP 3 ───────────────────────────────────────────────── */}
            {step === 3 && (
              <div className="space-y-4">
                {calcResult && (
                  <RateCard
                    calc={calcResult}
                    form={{ baseMaterial, testedPurityLabel, grossWeight, testedPurity, standardPurity }}
                  />
                )}

                {/* Customer summary */}
                <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-5">
                  <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3">Customer</p>
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="font-bold text-slate-800">{selectedCustomer?.fullName}</p>
                      <p className="text-sm text-slate-500">{selectedCustomer?.contactNumber}</p>
                      {selectedCustomer?.nidNumber && (
                        <p className="text-xs text-slate-400">NID: {selectedCustomer.nidNumber}</p>
                      )}
                    </div>
                    <button onClick={() => setStep(2)} className="text-xs text-amber-600 hover:underline">
                      Change
                    </button>
                  </div>
                </div>

                {/* ── Gross + Net weight side by side ── */}
                <div className="grid grid-cols-2 gap-3">
                  <div className="bg-slate-100 rounded-xl p-4 text-center">
                    <p className="text-xs text-slate-500 mb-1">Gross Weight</p>
                    <p className="text-xl font-bold text-slate-700">{grossWeight} g</p>
                    <p className="text-xs text-slate-400 mt-1">As received</p>
                  </div>
                  <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-center">
                    <p className="text-xs text-amber-600 font-semibold mb-1 flex items-center justify-center gap-1">
                      <Scale className="w-3 h-3" /> Net Weight
                    </p>
                    <p className="text-xl font-bold text-amber-700">
                      {calcResult?.netWeight != null ? `${calcResult.netWeight} g` : '—'}
                    </p>
                    <p className="text-xs text-slate-400 mt-1">If melted (fine metal)</p>
                  </div>
                </div>

                {/* Total amount */}
                <div className="bg-slate-800 rounded-2xl p-6 text-white">
                  <p className="text-slate-400 text-sm mb-1">Total amount to pay customer</p>
                  <p className="text-4xl font-bold text-amber-400">
                    ৳{calcResult?.totalAmount?.toLocaleString('en-BD', { minimumFractionDigits: 2 })}
                  </p>
                  <p className="text-slate-400 text-xs mt-2">
                    {baseMaterial} · {testedPurityLabel} · Gross {grossWeight}g · Net {calcResult?.netWeight}g · {testedPurity}%
                  </p>
                </div>

                <div className="flex gap-3">
                  <button onClick={() => { setStep(2); setError(''); }}
                    className="flex-1 py-3 border border-slate-200 text-slate-600 rounded-xl hover:bg-slate-50 font-medium">
                    ← Back
                  </button>
                  <button onClick={handleCreatePurchase} disabled={loading}
                    className="flex-1 py-3 bg-green-600 text-white font-bold rounded-xl hover:bg-green-700 disabled:opacity-50 transition-colors">
                    {loading ? 'Saving...' : '✓ Confirm Purchase'}
                  </button>
                </div>
              </div>
            )}

            {/* ── STEP 4 — Done ────────────────────────────────────────── */}
            {step === 4 && (
              <div className="bg-white rounded-2xl shadow-sm border border-green-200 p-8 text-center space-y-5">
                <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto">
                  <CheckCircle className="w-8 h-8 text-green-600" />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-slate-800">Purchase Recorded!</h2>
                  <p className="text-slate-500 text-sm mt-1">{success}</p>
                </div>
                <div className="flex flex-col gap-3 pt-2">
                  <button onClick={() => downloadPurchaseReceipt(receiptNo)}
                    className="w-full py-3 bg-amber-600 text-white font-bold rounded-xl hover:bg-amber-700 flex items-center justify-center gap-2">
                    <Download className="w-4 h-4" /> Download Receipt ({receiptNo})
                  </button>
                  <button onClick={handleReset}
                    className="w-full py-3 border border-slate-200 text-slate-600 rounded-xl hover:bg-slate-50 font-medium">
                    + New Purchase
                  </button>
                  <button onClick={onDashboardClick}
                    className="w-full py-3 text-slate-400 hover:text-slate-600 text-sm">
                    Back to Dashboard
                  </button>
                </div>
              </div>
            )}

          </div>
        </>
      )}

    </div>
  );
}