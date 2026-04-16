import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { LayoutDashboard, MapPin, LogOut } from 'lucide-react';

export default function ShopLayout() {
  const navigate = useNavigate();

  const handleLogout = () => {
    localStorage.removeItem('token');
    navigate('/login', { replace: true });
  };

  return (
    <div className="flex h-screen bg-gray-50">
      {/* Sidebar ShopOwner */}
      <aside className="w-64 h-screen bg-white shadow-sm flex flex-col fixed left-0 top-0 border-r border-gray-100">
        <div className="p-6">
          <h1 className="text-xl font-bold text-orange-500">Vĩnh Khánh</h1>
          <p className="text-xs text-gray-500 mt-1">CỔNG CHỦ QUÁN</p>
        </div>
        <nav className="flex-1 px-4 mt-4 space-y-2">
          <NavLink to="/shop/dashboard"
            className={({ isActive }) =>
              `flex items-center gap-3 px-4 py-3 rounded-2xl font-medium transition-all ${isActive ? 'bg-orange-50 text-orange-600' : 'text-gray-600 hover:bg-gray-50'}`}>
            <LayoutDashboard size={20} /><span>Tổng quan</span>
          </NavLink>
          <NavLink to="/shop/pois"
            className={({ isActive }) =>
              `flex items-center gap-3 px-4 py-3 rounded-2xl font-medium transition-all ${isActive ? 'bg-orange-50 text-orange-600' : 'text-gray-600 hover:bg-gray-50'}`}>
            <MapPin size={20} /><span>Quản lý POI</span>
          </NavLink>
        </nav>
        <div className="p-4 border-t">
          <button onClick={handleLogout}
            className="flex items-center gap-3 px-4 py-3 w-full text-gray-600 hover:bg-red-50 hover:text-red-600 rounded-2xl font-medium">
            <LogOut size={20} /><span>Đăng xuất</span>
          </button>
        </div>
      </aside>
      <main className="ml-64 flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  );
}
