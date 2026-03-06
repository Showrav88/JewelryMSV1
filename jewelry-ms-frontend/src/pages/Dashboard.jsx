// src/pages/Dashboard.jsx
export default function Dashboard({ onSalesClick, onPurchaseClick, onLogout }) {
  return (
    <div
      id="dashboard-page"
      data-testid="dashboard"
      className="min-h-screen bg-slate-50 p-8"
    >
      <h1 className="text-3xl font-bold mb-8">Dashboard</h1>

      <div className="flex gap-4 flex-wrap">
        <button
          id="sales-btn"
          data-testid="dashboard-sales-btn"
          onClick={onSalesClick}
          className="px-6 py-3 bg-amber-600 text-white rounded-lg font-semibold hover:bg-amber-700"
        >
          Go to Sales
        </button>

        <button
          id="purchase-btn"
          data-testid="dashboard-purchase-btn"
          onClick={onPurchaseClick}
          className="px-6 py-3 bg-emerald-600 text-white rounded-lg font-semibold hover:bg-emerald-700"
        >
          Go to Purchase
        </button>

        <button
          id="logout-btn"
          data-testid="dashboard-logout-btn"
          onClick={onLogout}
          className="px-6 py-3 bg-red-600 text-white rounded-lg font-semibold hover:bg-red-700"
        >
          Logout
        </button>
      </div>
    </div>
  );
}