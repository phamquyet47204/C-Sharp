import React, { useState, useEffect } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { LayoutDashboard, MapPin, BarChart3, Settings, LogOut, ClipboardCheck } from 'lucide-react';
import api from '../services/api';

const Sidebar = () => {
  const navigate = useNavigate();
  const [pendingCount, setPendingCount] = useState(0);

  useEffect(() => {
    const fetchPendingCount = async () => {
      try {
        const res = await api.get('/admin/pois/pending');
        setPendingCount(res.data.length);
      } catch { /* ignore */ }
    };
    fetchPendingCount();
    const interval = setInterval(fetchPendingCount, 60000);
    return () => clearInterval(interval);
  }, []);

  const menuItems = [
    { icon: <LayoutDashboard size={20} />, label: 'Dashboard', path: '/dashboard' },
    { icon: <MapPin size={20} />, label: 'Quản lý POI', path: '/pois' },
    {
      icon: <ClipboardCheck size={20} />,
      label: 'Duyệt POI',
      path: '/approvals',
      badge: pendingCount > 0 ? pendingCount : null
    },
    { icon: <BarChart3 size={20} />, label: 'Phân tích', path: '/analytics' },
    { icon: <Settings size={20} />, label: 'Cài đặt', path: '/settings' },
  ];

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('adminEmail');
    navigate('/login', { replace: true });
  };

  return (
    <aside className="w-64 h-screen bg-white shadow-sm flex flex-col fixed left-0 top-0 border-r border-gray-100">
      <div className="p-6">
        <h1 className="text-2xl font-bold bg-gradient-to-r from-coral-400 to-coral-600 bg-clip-text text-transparent">
          Vĩnh Khánh
        </h1>
        <p className="text-xs text-gray-500 mt-1 font-medium tracking-wide">DIGITAL TOUR GUIDE</p>
      </div>

      <nav className="flex-1 px-4 mt-6 space-y-2">
        {menuItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              `flex items-center gap-3 px-4 py-3 rounded-2xl transition-all duration-200 font-medium ${
                isActive
                  ? 'bg-coral-50 text-coral-600 shadow-sm'
                  : 'text-gray-600 hover:bg-gray-50 hover:text-coral-500'
              }`
            }
          >
            {item.icon}
            <span className="flex-1">{item.label}</span>
            {item.badge && (
              <span className="bg-red-500 text-white text-xs font-bold rounded-full w-5 h-5 flex items-center justify-center">
                {item.badge > 9 ? '9+' : item.badge}
              </span>
            )}
          </NavLink>
        ))}
      </nav>

      <div className="p-4 border-t border-gray-100">
        <button
          onClick={handleLogout}
          className="flex items-center gap-3 px-4 py-3 w-full text-gray-600 hover:bg-red-50 hover:text-red-600 rounded-2xl transition-all duration-200 font-medium"
        >
          <LogOut size={20} />
          <span>Đăng xuất</span>
        </button>
      </div>
    </aside>
  );
};

export default Sidebar;
