import React, { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Plus, Search, Edit2, Trash2 } from 'lucide-react';
import api from '../services/api';

const PoiManager = () => {
  const [pois, setPois] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [activeCategory, setActiveCategory] = useState('ALL');
  const navigate = useNavigate();

  const categoryOptions = [
    { code: 'ALL', label: 'Tất cả' },
    { code: 'FOOD_SNAIL', label: 'Ốc & Hải sản' },
    { code: 'FOOD_BBQ', label: 'Đồ nướng & Lẩu' },
    { code: 'FOOD_STREET', label: 'Ăn vặt' },
    { code: 'DRINK', label: 'Đồ uống' },
    { code: 'UTILITY', label: 'Tiện ích' }
  ];

  const toCategoryLabel = (categoryCode) => {
    switch ((categoryCode || '').toUpperCase()) {
      case 'FOOD_SNAIL':
        return 'Ốc & Hải sản';
      case 'FOOD_BBQ':
        return 'Đồ nướng & Lẩu';
      case 'FOOD_STREET':
        return 'Ăn vặt';
      case 'DRINK':
        return 'Đồ uống';
      case 'UTILITY':
        return 'Tiện ích';
      default:
        return 'Khác';
    }
  };

  const categoryClassName = (categoryCode) => {
    switch ((categoryCode || '').toUpperCase()) {
      case 'FOOD_SNAIL':
        return 'bg-sky-100 text-sky-700';
      case 'FOOD_BBQ':
        return 'bg-orange-100 text-orange-700';
      case 'FOOD_STREET':
        return 'bg-amber-100 text-amber-700';
      case 'DRINK':
        return 'bg-emerald-100 text-emerald-700';
      case 'UTILITY':
        return 'bg-violet-100 text-violet-700';
      default:
        return 'bg-gray-100 text-gray-600';
    }
  };

  const getBackendOrigin = () => {
    const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

    if (configuredBaseUrl) {
      try {
        const resolvedUrl = new URL(configuredBaseUrl, window.location.origin);
        const normalizedPath = resolvedUrl.pathname.replace(/\/api\/?$/i, '/');
        return `${resolvedUrl.origin}${normalizedPath}`.replace(/\/$/, '');
      } catch {
        // Fall back below.
      }
    }

    if (import.meta.env.DEV) {
      return 'http://localhost:5000';
    }

    return window.location.origin;
  };

  const resolveImageUrl = (imageUrl) => {
    if (!imageUrl) {
      return '';
    }

    if (/^https?:\/\//i.test(imageUrl)) {
      return imageUrl;
    }

    const backendOrigin = getBackendOrigin();
    const normalizedPath = imageUrl.startsWith('/') ? imageUrl : `/${imageUrl}`;
    return `${backendOrigin}${normalizedPath}`;
  };

  const filteredPois = pois.filter((poi) => {
    const normalizedName = (poi.name || '').toLowerCase();
    const normalizedSearch = searchTerm.trim().toLowerCase();
    const matchesSearch = !normalizedSearch || normalizedName.includes(normalizedSearch);
    const poiCategory = (poi.categoryCode || poi.category || '').toUpperCase();
    const matchesCategory = activeCategory === 'ALL' || poiCategory === activeCategory;

    return matchesSearch && matchesCategory;
  });

  const fetchPois = async () => {
    try {
      const res = await api.get('/admin/pois');
      setPois(res.data);
    } catch (err) {
      const status = err?.response?.status;

      if (status === 401 || status === 403) {
        localStorage.removeItem('token');
        navigate('/login', { replace: true });
        return;
      }

      console.error('Lỗi lấy danh sách POI:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPois();
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold text-gray-900">Quản lý địa điểm</h2>
          <p className="text-gray-500 mt-1">Danh sách POI hiển thị thực tế từ cơ sở dữ liệu.</p>
        </div>
        <Link 
          to="/pois/new" 
          className="bg-coral-500 hover:bg-coral-600 text-white px-6 py-3 rounded-2xl font-semibold flex items-center gap-2 transition-all shadow-sm shadow-coral-500/30"
        >
          <Plus size={20} />
          <span>Thêm địa điểm mới</span>
        </Link>
      </div>

      <div className="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden">
        <div className="p-4 border-b border-gray-100 flex justify-between items-center bg-gray-50/50">
          <div className="relative w-72">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" size={18} />
            <input 
              type="text" 
              placeholder="Tìm kiếm theo tên..." 
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-gray-200 focus:outline-none focus:ring-2 focus:ring-coral-500/20 focus:border-coral-500 transition-all text-sm"
            />
          </div>
          <div className="flex items-center gap-2">
            {categoryOptions.map((category) => (
              <button
                key={category.code}
                type="button"
                onClick={() => setActiveCategory(category.code)}
                className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-colors ${
                  activeCategory === category.code
                    ? 'bg-coral-500 text-white'
                    : 'bg-white text-gray-600 border border-gray-200 hover:border-coral-300 hover:text-coral-600'
                }`}
              >
                {category.label}
              </button>
            ))}
          </div>
        </div>
        <div className="overflow-x-auto min-h-[400px]">
          {loading ? (
            <div className="flex justify-center items-center h-48 text-gray-400">Đang tải dữ liệu...</div>
          ) : filteredPois.length === 0 ? (
            <div className="flex justify-center items-center h-48 text-gray-400">Chưa có địa điểm nào trong CSDL!</div>
          ) : (
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-gray-50/50 text-gray-500 text-sm">
                  <th className="px-6 py-4 font-medium border-b border-gray-100">ID</th>
                  <th className="px-6 py-4 font-medium border-b border-gray-100">Ảnh</th>
                  <th className="px-6 py-4 font-medium border-b border-gray-100">Tên T.Việt (Gốc)</th>
                  <th className="px-6 py-4 font-medium border-b border-gray-100">Loại</th>
                  <th className="px-6 py-4 font-medium border-b border-gray-100">Tọa độ</th>
                  <th className="px-6 py-4 font-medium border-b border-gray-100">Trạng thái</th>
                  <th className="px-6 py-4 font-medium border-b border-gray-100">Chủ quán</th>
                  <th className="px-6 py-4 font-medium border-b border-gray-100 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {filteredPois.map((poi) => (
                  <tr key={poi.id} className="hover:bg-gray-50/50 transition-colors group">
                    <td className="px-6 py-4 text-gray-500 text-sm">#{poi.id}</td>
                    <td className="px-6 py-4">
                      {resolveImageUrl(poi.imageUrl) ? (
                        <img
                          src={resolveImageUrl(poi.imageUrl)}
                          alt={poi.name || 'POI image'}
                          className="w-16 h-12 rounded-xl object-cover border border-gray-200 bg-gray-50"
                          loading="lazy"
                        />
                      ) : (
                        <div className="w-16 h-12 rounded-xl border border-dashed border-gray-200 bg-gray-50 flex items-center justify-center text-[10px] text-gray-400">
                          Không ảnh
                        </div>
                      )}
                    </td>
                    <td className="px-6 py-4">
                      <span className="font-semibold text-gray-900 group-hover:text-coral-600 transition-colors">{poi.name}</span>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold ${categoryClassName(poi.categoryCode || poi.category)}`}>
                        {toCategoryLabel(poi.categoryCode || poi.category)}
                      </span>
                    </td>
                    <td className="px-6 py-4 font-mono text-sm text-gray-500">
                      {poi.lat?.toFixed(5)}, {poi.lng?.toFixed(5)}
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold ${
                        poi.isApproved ? 'bg-green-100 text-green-700' : 'bg-yellow-100 text-yellow-700'
                      }`}>
                        {poi.status || (poi.isApproved ? 'Đã duyệt' : 'Chờ duyệt')}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-500">
                      {poi.ownerName || <span className="text-gray-300">—</span>}
                    </td>
                    <td className="px-6 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Link to={`/pois/${poi.id}`} className="p-2 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-xl transition-colors">
                          <Edit2 size={18} />
                        </Link>
                        <button className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-colors">
                          <Trash2 size={18} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
};

export default PoiManager;
