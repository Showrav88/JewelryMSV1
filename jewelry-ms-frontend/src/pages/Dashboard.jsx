export default function Dashboard({ onSalesClick }) {
  return (
    <div className="min-h-screen bg-slate-50 p-8">
      <h1 className="text-3xl font-bold mb-8">Dashboard</h1>
      
      <button
        onClick={onSalesClick}
        className="px-6 py-3 bg-amber-600 text-white rounded-lg font-semibold hover:bg-amber-700"
      >
        Go to Sales
      </button>

      <button 
        onClick={() => {
          localStorage.clear();
          window.location.reload();
        }}
        className="ml-4 px-6 py-3 bg-red-600 text-white rounded-lg font-semibold"
      >
        Logout
      </button>

       <button className="bg-amber-600 hover:bg-amber-700">Test</button>
    </div>
  );
}