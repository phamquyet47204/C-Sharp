import React, { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Plus, Search, Edit2, Trash2, Eye, ExternalLink, QrCode, X, Globe, MapPin } from 'lucide-react';
import Swal from 'sweetalert2';
import api from '../services/api';

const PoiManager = () => {
  const [pois, setPois] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [activeCategory, setActiveCategory] = useState('ALL');
  const [selectedPoi, setSelectedPoi] = useState(null);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [activeLangTab, setActiveLangTab] = useState('vi');
  const navigate = useNavigate();

  const categoryOptions = [
    { code: 'ALL', label: 'Tất cả' },
    { code: 'FOOD_SNAIL', label: 'Ốc & Hải sản' },
    { code: 'FOOD_BBQ', label: 'Đồ nướng & Lẩu' },
    { code: 'FOOD_STREET', label: 'Ăn vặt' },
    { code: 'PHOTO_SPOT', label: 'Check-in' },
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
      case 'PHOTO_SPOT':
        return 'Check-in & Sống ảo';
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
      case 'PHOTO_SPOT':
        return 'bg-rose-100 text-rose-700';
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

  const handleDelete = async (poiId, poiName) => {
    const result = await Swal.fire({
      title: 'Xác nhận xóa?',
      text: `Bạn có chắc chắn muốn xóa "${poiName}"? Hành động này không thể hoàn tác!`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#ff5a5f', // coral-500
      cancelButtonColor: '#9ca3af', // gray-400
      confirmButtonText: 'Vâng, xóa nó!',
      cancelButtonText: 'Hủy',
      background: '#ffffff',
      borderRadius: '24px',
      customClass: {
        popup: 'rounded-3xl shadow-xl border-none',
        confirmButton: 'rounded-xl px-6 py-2.5 font-semibold text-white',
        cancelButton: 'rounded-xl px-6 py-2.5 font-semibold text-gray-600'
      }
    });

    if (result.isConfirmed) {
      try {
        await api.delete(`/admin/pois/${poiId}`);
        // Cập nhật state local để UI thay đổi ngay lập tức
        setPois(prev => prev.filter(p => p.id !== poiId));
        
        Swal.fire({
          title: 'Đã xóa!',
          text: 'Địa điểm đã được loại bỏ khỏi hệ thống.',
          icon: 'success',
          timer: 2000,
          showConfirmButton: false,
          borderRadius: '24px'
        });
      } catch (err) {
        console.error('Lỗi khi xóa POI:', err);
        Swal.fire({
          title: 'Thất bại',
          text: 'Không thể xóa địa điểm này. Vui lòng thử lại sau.',
          icon: 'error',
          confirmButtonColor: '#ff5a5f',
          borderRadius: '24px'
        });
      }
    }
  };

  const handleView = async (poiId) => {
    setIsDetailLoading(true);
    try {
      const res = await api.get(`/admin/pois/${poiId}`);
      setSelectedPoi(res.data);
      setActiveLangTab('vi');
    } catch (err) {
      console.error('Lỗi khi tải chi tiết POI:', err);
      Swal.fire({
        title: 'Lỗi',
        text: 'Không thể tải thông tin chi tiết.',
        icon: 'error',
        confirmButtonColor: '#ff5a5f'
      });
    } finally {
      setIsDetailLoading(false);
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
                        <button 
                          onClick={() => handleView(poi.id)}
                          className="p-2 text-gray-400 hover:text-emerald-600 hover:bg-emerald-50 rounded-xl transition-colors"
                          title="Xem chi tiết"
                        >
                          <Eye size={18} />
                        </button>
                        <Link to={`/pois/${poi.id}`} className="p-2 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-xl transition-colors">
                          <Edit2 size={18} />
                        </Link>
                        <button 
                          onClick={() => handleDelete(poi.id, poi.name)}
                          className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-colors"
                          title="Xóa địa điểm"
                        >
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
      {/* Detail View Modal */}
      {selectedPoi && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          {/* Backdrop */}
          <div 
            className="absolute inset-0 bg-slate-900/60 backdrop-blur-sm transition-opacity" 
            onClick={() => setSelectedPoi(null)}
          ></div>
          
          {/* Modal Container */}
          <div className="relative bg-white w-full max-w-4xl max-h-[90vh] rounded-[2.5rem] shadow-2xl overflow-hidden flex flex-col animate-in zoom-in-95 duration-200">
            {/* Header */}
            <div className="p-6 border-b border-gray-100 flex items-center justify-between bg-white sticky top-0 z-10">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-2xl bg-coral-50 flex items-center justify-center text-coral-500">
                  <Eye size={24} />
                </div>
                <div>
                  <h3 className="text-xl font-bold text-gray-900">Chi tiết địa điểm</h3>
                  <p className="text-sm text-gray-500">ID: #{selectedPoi.id}</p>
                </div>
              </div>
              <button 
                onClick={() => setSelectedPoi(null)}
                className="p-2 hover:bg-gray-100 rounded-xl text-gray-400 hover:text-gray-600 transition-colors"
              >
                <X size={24} />
              </button>
            </div>

            {/* Content Scroll Area */}
            <div className="overflow-y-auto p-6 md:p-8 flex-1">
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
                {/* Left Column: Visuals */}
                <div className="lg:col-span-5 space-y-6">
                  {/* POI Main Image */}
                  <div className="space-y-2">
                    <label className="text-[10px] uppercase font-bold tracking-widest text-gray-400">Ảnh đại diện</label>
                    <div className="aspect-video w-full rounded-3xl overflow-hidden border border-gray-200 shadow-sm relative">
                      {selectedPoi.imageUrl ? (
                        <img 
                          src={resolveImageUrl(selectedPoi.imageUrl)} 
                          className="w-full h-full object-cover" 
                          alt="POI Main" 
                        />
                      ) : (
                        <div className="w-full h-full bg-gray-50 flex items-center justify-center text-gray-300">Không có ảnh</div>
                      )}
                    </div>
                  </div>

                  {/* QR Core Info */}
                  <div className="bg-slate-50 rounded-3xl p-6 border border-slate-100">
                    <div className="flex items-center gap-2 mb-4">
                      <QrCode className="text-slate-700" size={20} />
                      <h4 className="font-bold text-slate-800">Mã QR chính thức</h4>
                    </div>
                    <div className="bg-white p-4 rounded-2xl border border-slate-200 flex flex-col items-center gap-4 shadow-sm">
                      <img 
                        src={`${getBackendOrigin()}/api/qr/${selectedPoi.qrToken}/png`} 
                        className="w-40 h-40 object-contain" 
                        alt="QR Code" 
                        onError={(e) => { e.target.style.display = 'none'; }}
                      />
                      <div className="text-center">
                        <p className="text-[10px] text-gray-400 font-mono mb-1 uppercase tracking-tighter">{selectedPoi.qrToken}</p>
                        <a 
                          href={selectedPoi.qrLink} 
                          target="_blank" 
                          rel="noreferrer"
                          className="text-xs text-blue-600 hover:underline flex items-center justify-center gap-1 font-semibold"
                        >
                          Mở trang Landing <ExternalLink size={12} />
                        </a>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Right Column: Localized Data */}
                <div className="lg:col-span-7 space-y-6">
                  {/* Tabs */}
                  <div className="flex gap-2 p-1 bg-gray-100 rounded-2xl w-fit">
                    {['vi', 'en', 'ja'].map(lang => (
                      <button
                        key={lang}
                        onClick={() => setActiveLangTab(lang)}
                        className={`px-4 py-2 rounded-xl text-xs font-bold transition-all ${
                          activeLangTab === lang 
                            ? 'bg-white text-coral-600 shadow-sm' 
                            : 'text-gray-500 hover:text-gray-700'
                        }`}
                      >
                        {lang === 'vi' ? '🇻🇳 TV' : lang === 'en' ? '🇬🇧 EN' : '🇯🇵 JA'}
                      </button>
                    ))}
                  </div>

                  <div className="space-y-4 animate-in fade-in slide-in-from-bottom-2 duration-300">
                    <div className="space-y-1">
                      <label className="text-[10px] uppercase font-bold tracking-widest text-gray-400">Tên địa điểm</label>
                      <h4 className="text-2xl font-black text-gray-900 leading-tight">
                        {selectedPoi[activeLangTab]?.name || <span className="text-gray-300 italic font-normal">Chưa cập nhật tên</span>}
                      </h4>
                    </div>

                    <div className="space-y-1">
                      <label className="text-[10px] uppercase font-bold tracking-widest text-gray-400">Mô tả giới thiệu</label>
                      <div className="text-gray-600 leading-relaxed bg-gray-50 p-4 rounded-2xl border border-gray-100 italic min-h-[100px]">
                        {selectedPoi[activeLangTab]?.description || 'Không có mô tả cho ngôn ngữ này.'}
                      </div>
                    </div>

                    <div className="grid grid-cols-2 gap-4 pt-4 border-t border-gray-100">
                      <div className="space-y-1">
                        <label className="text-[10px] uppercase font-bold tracking-widest text-gray-400 flex items-center gap-1">
                          <MapPin size={10} /> Tọa độ
                        </label>
                        <p className="text-sm font-mono text-gray-500">{selectedPoi.lat?.toFixed(6)}, {selectedPoi.lng?.toFixed(6)}</p>
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] uppercase font-bold tracking-widest text-gray-400 flex items-center gap-1">
                          <Globe size={10} /> Danh mục
                        </label>
                        <p className="text-sm font-semibold text-gray-700">{toCategoryLabel(selectedPoi.categoryCode)}</p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Footer Actions */}
            <div className="p-6 bg-gray-50 border-t border-gray-100 flex justify-between items-center">
              <button 
                onClick={() => setSelectedPoi(null)}
                className="px-6 py-3 text-gray-600 font-bold hover:bg-gray-200 rounded-2xl transition-all"
              >
                Đóng
              </button>
              <button 
                onClick={() => {
                  setSelectedPoi(null);
                  navigate(`/pois/${selectedPoi.id}`);
                }}
                className="bg-coral-500 hover:bg-coral-600 text-white px-8 py-3 rounded-2xl font-bold flex items-center gap-2 transition-all shadow-lg shadow-coral-500/20"
              >
                <Edit2 size={18} />
                Chỉnh sửa thông tin
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Detail Loading Overlay */}
      {isDetailLoading && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/20 backdrop-blur-[2px]">
          <div className="bg-white px-6 py-4 rounded-2xl shadow-xl flex items-center gap-3 font-semibold text-gray-600">
            <div className="w-5 h-5 border-2 border-coral-500 border-t-transparent rounded-full animate-spin"></div>
            Đang tải dữ liệu...
          </div>
        </div>
      )}
    </div>
  );
};

export default PoiManager;
