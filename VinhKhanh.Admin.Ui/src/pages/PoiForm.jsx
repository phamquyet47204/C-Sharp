import React, { useEffect, useState } from 'react';
import { Save, ArrowLeft, UploadCloud, Map as MapIcon, Image as ImageIcon, Sparkles } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import MapPicker from '../components/MapPicker';
import api from '../services/api';

const PoiForm = () => {
  const navigate = useNavigate();
  const { id } = useParams();
  const isEditMode = !!id;
  const [activeTab, setActiveTab] = useState('vi');

  const categoryOptions = [
    { code: 'FOOD_SNAIL', nameVi: 'Ốc & Hải sản', nameEn: 'Snails & Seafood' },
    { code: 'FOOD_BBQ', nameVi: 'Đồ nướng & Lẩu', nameEn: 'BBQ & Hotpot' },
    { code: 'FOOD_STREET', nameVi: 'Ăn vặt', nameEn: 'Street Food' },
    { code: 'DRINK', nameVi: 'Đồ uống', nameEn: 'Drinks' },
    { code: 'UTILITY', nameVi: 'Tiện ích', nameEn: 'Utilities' }
  ];
  
  const [formData, setFormData] = useState({
    vi: { name: '', description: '' },
    en: { name: '', description: '' },
    ja: { name: '', description: '' },
    lat: 10.7601,
    lng: 106.7023,
    radius: 50,
    categoryCode: 'FOOD_STREET'
  });

  const [files, setFiles] = useState({
    image: null
  });

  const [isGenerating, setIsGenerating] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [existingImageUrl, setExistingImageUrl] = useState('');
  const [selectedImagePreviewUrl, setSelectedImagePreviewUrl] = useState('');
  const [errorMsg, setErrorMsg] = useState(null);

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

  useEffect(() => {
    if (!isEditMode) {
      return;
    }

    const fetchDetail = async () => {
      setIsLoadingDetail(true);
      try {
        const response = await api.get(`/admin/pois/${id}`);
        const detail = response.data;

        setFormData({
          vi: {
            name: detail?.vi?.name || '',
            description: detail?.vi?.description || ''
          },
          en: {
            name: detail?.en?.name || '',
            description: detail?.en?.description || ''
          },
          ja: {
            name: detail?.ja?.name || '',
            description: detail?.ja?.description || ''
          },
          lat: detail?.lat ?? 10.7601,
          lng: detail?.lng ?? 106.7023,
          radius: detail?.radius ?? 50,
          categoryCode: detail?.categoryCode || 'FOOD_STREET'
        });

        setExistingImageUrl(resolveImageUrl(detail?.imageUrl));
      } catch (error) {
        showError('Không tải được dữ liệu POI để chỉnh sửa.');
      } finally {
        setIsLoadingDetail(false);
      }
    };

    fetchDetail();
  }, [id, isEditMode]);

  useEffect(() => {
    return () => {
      if (selectedImagePreviewUrl) {
        URL.revokeObjectURL(selectedImagePreviewUrl);
      }
    };
  }, [selectedImagePreviewUrl]);

  const showError = (msg) => {
    setErrorMsg(msg);
    setTimeout(() => setErrorMsg(null), 4000); // Tự tắt sau 4s
  };

  const handleLangChange = (lang, field, value) => {
    setFormData(prev => ({
      ...prev,
      [lang]: { ...prev[lang], [field]: value }
    }));
  };

  const handleFileChange = (field, file) => {
    if (selectedImagePreviewUrl) {
      URL.revokeObjectURL(selectedImagePreviewUrl);
      setSelectedImagePreviewUrl('');
    }

    if (field === 'image' && file) {
      setSelectedImagePreviewUrl(URL.createObjectURL(file));
    }

    setFiles(prev => ({ ...prev, [field]: file }));
  };

  const handleGenerateAI = async () => {
    if (!formData.vi.name || !formData.vi.description) {
      showError("Vui lòng nhập tên quán và mô tả Tiếng Việt trước khi dùng AI!");
      return;
    }

    setIsGenerating(true);
    try {
      const response = await api.post('/admin/ai/generate', {
        name: formData.vi.name,
        description: formData.vi.description
      });

      const { en, ja } = response.data;
      
      setFormData(prev => ({
        ...prev,
        en: { name: en.name || '', description: en.description || '' },
        ja: { name: ja.name || '', description: ja.description || '' }
      }));

      setActiveTab('en');
    } catch (error) {
      const status = error?.response?.status;
      const apiMessage = typeof error?.response?.data === 'string'
        ? error.response.data
        : (error?.response?.data?.message || error?.response?.data?.title);

      if (status === 401) {
        showError('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
      } else if (status === 403) {
        showError('Tài khoản hiện tại không có quyền Admin để dùng tính năng AI dịch thuật.');
      } else if (status === 400) {
        showError(apiMessage || 'Dữ liệu đầu vào chưa hợp lệ để dịch.');
      } else {
        showError(apiMessage || 'Lỗi khi gọi AI dịch thuật. Vui lòng kiểm tra cấu hình Gemini API ở backend.');
      }
    } finally {
      setIsGenerating(false);
    }
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      const data = new FormData();
      data.append('Lat', formData.lat);
      data.append('Lng', formData.lng);
      data.append('Radius', formData.radius);
      data.append('CategoryCode', formData.categoryCode);
      
      data.append('NameVi', formData.vi.name);
      data.append('DescVi', formData.vi.description);
      data.append('NameEn', formData.en.name);
      data.append('DescEn', formData.en.description);
      data.append('NameJa', formData.ja.name);
      data.append('DescJa', formData.ja.description);

      if (files.image) data.append('Image', files.image);

      const endpoint = isEditMode ? `/admin/pois/${id}` : '/admin/pois';
      const method = isEditMode ? 'put' : 'post';

      const res = await api({
        method,
        url: endpoint,
        data,
        headers: { 'Content-Type': 'multipart/form-data' }
      });

      if (res.data.success) {
        navigate('/pois');
      }
    } catch (error) {
      showError(isEditMode
        ? 'Lỗi khi cập nhật dữ liệu. Đảm bảo Backend .NET đang chạy.'
        : 'Lỗi khi lưu dữ liệu. Đảm bảo Backend .NET đang chạy.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="max-w-5xl mx-auto space-y-6 pb-20 relative">
      {/* Thông báo lỗi dạng Popup mượt mà */}
      {errorMsg && (
        <div className="fixed top-8 left-1/2 -translate-x-1/2 z-50 bg-red-50 text-red-600 px-6 py-3 rounded-2xl shadow-lg border border-red-100 font-medium flex items-center gap-3 animate-in fade-in slide-in-from-top-4 duration-300">
          <div className="w-2 h-2 rounded-full bg-red-500 animate-pulse"></div>
          {errorMsg}
        </div>
      )}

      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <Link to="/pois" className="p-2 hover:bg-white rounded-xl text-gray-500 hover:text-coral-600 transition-colors shadow-sm">
            <ArrowLeft size={20} />
          </Link>
          <div>
            <h2 className="text-3xl font-bold text-gray-900">
              {isEditMode ? 'Chỉnh sửa địa điểm' : 'Thêm địa điểm mới'}
            </h2>
            <p className="text-gray-500 mt-1">Sử dụng AI để dịch thuật & tự động hóa nội dung đa ngôn ngữ.</p>
          </div>
        </div>
        <button 
          onClick={handleSave}
          disabled={isSaving}
          className={`${isSaving ? 'bg-gray-400' : 'bg-coral-500 hover:bg-coral-600 shadow-coral-500/30'} text-white px-8 py-3 rounded-2xl font-semibold flex items-center justify-center gap-2 transition-all shadow-sm w-full md:w-auto`}
        >
          <Save size={20} />
          <span>{isSaving ? 'Đang lưu...' : 'Lưu thay đổi'}</span>
        </button>
      </div>

      {isLoadingDetail && (
        <div className="rounded-2xl border border-coral-100 bg-coral-50 px-4 py-3 text-sm font-medium text-coral-700">
          Đang tải dữ liệu POI...
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <div className="flex flex-wrap items-center gap-4 border-b border-gray-100 pb-4 mb-6 pt-2">
              <div className="flex gap-2">
                <button onClick={() => setActiveTab('vi')} className={`px-4 py-2 rounded-xl font-semibold transition-colors ${activeTab === 'vi' ? 'bg-coral-50 text-coral-600' : 'text-gray-500 hover:bg-gray-50'}`}>
                  🇻🇳 Tiếng Việt
                </button>
                <button onClick={() => setActiveTab('en')} className={`px-4 py-2 rounded-xl font-semibold transition-colors ${activeTab === 'en' ? 'bg-coral-50 text-coral-600' : 'text-gray-500 hover:bg-gray-50'}`}>
                  🇬🇧 Tiếng Anh
                </button>
                <button onClick={() => setActiveTab('ja')} className={`px-4 py-2 rounded-xl font-semibold transition-colors ${activeTab === 'ja' ? 'bg-coral-50 text-coral-600' : 'text-gray-500 hover:bg-gray-50'}`}>
                  🇯🇵 Tiếng Nhật
                </button>
              </div>
              
              <div className="ml-auto">
                <button 
                  onClick={handleGenerateAI}
                  disabled={isGenerating}
                  className={`${isGenerating ? 'bg-gray-100 text-gray-400' : 'bg-indigo-50 text-indigo-600 hover:bg-indigo-100 border border-indigo-200'} px-4 py-2 rounded-xl text-sm font-bold flex items-center gap-2 transition-all shadow-sm`}
                >
                  <Sparkles size={16} className={isGenerating ? "animate-spin" : ""} />
                  {isGenerating ? 'AI đang dịch...' : '🪄 AI Generate Content'}
                </button>
              </div>
            </div>

            <div className="space-y-5">
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">Tên địa điểm ({activeTab.toUpperCase()})</label>
                <input 
                  type="text" 
                  value={formData[activeTab].name}
                  onChange={(e) => handleLangChange(activeTab, 'name', e.target.value)}
                  className="w-full px-4 py-3 bg-gray-50 border-transparent focus:border-coral-500 focus:bg-white focus:ring-4 focus:ring-coral-500/10 rounded-2xl transition-all"
                  placeholder="Nhập tên quán..."
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">Mô tả chi tiết ({activeTab.toUpperCase()})</label>
                <textarea 
                  rows={5}
                  value={formData[activeTab].description}
                  onChange={(e) => handleLangChange(activeTab, 'description', e.target.value)}
                  className="w-full px-4 py-3 bg-gray-50 border-transparent focus:border-coral-500 focus:bg-white focus:ring-4 focus:ring-coral-500/10 rounded-2xl transition-all resize-none"
                  placeholder="Nhập mô tả món ăn, không gian, giờ mở cửa..."
                />
              </div>
            </div>
          </div>

          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center gap-2 mb-6">
              <MapIcon className="text-coral-500" size={24} />
              <h3 className="text-xl font-bold text-gray-900">Bản đồ & Tọa độ</h3>
            </div>
            <MapPicker 
              latitude={formData.lat}
              longitude={formData.lng}
              radius={formData.radius}
              onChange={(pos) => setFormData(p => ({...p, lat: pos.lat, lng: pos.lng}))}
            />
          </div>
        </div>

        <div className="space-y-6">
          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <h3 className="text-lg font-bold text-gray-900 mb-4">Phân loại địa điểm</h3>
            <label className="block text-sm font-semibold text-gray-700 mb-2">Loại POI</label>
            <select
              value={formData.categoryCode}
              onChange={(e) => setFormData((prev) => ({ ...prev, categoryCode: e.target.value }))}
              className="w-full px-4 py-3 bg-gray-50 border-transparent focus:border-coral-500 focus:bg-white focus:ring-4 focus:ring-coral-500/10 rounded-2xl transition-all"
            >
              {categoryOptions.map((option) => (
                <option key={option.code} value={option.code}>
                  {option.nameVi} ({option.nameEn})
                </option>
              ))}
            </select>
          </div>

          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center gap-2 mb-6">
              <ImageIcon className="text-emerald-500" size={24} />
              <h3 className="text-lg font-bold text-gray-900">Hình đại diện</h3>
            </div>
            <label className="border-2 border-dashed border-gray-200 rounded-2xl p-8 text-center hover:bg-gray-50 transition-colors cursor-pointer group block">
              <input type="file" className="hidden" accept="image/png, image/jpeg" onChange={e => handleFileChange('image', e.target.files[0])} />
              <UploadCloud className="mx-auto text-gray-400 group-hover:text-emerald-500 mb-3" size={32} />
              <p className="text-sm text-gray-600 font-medium">{files.image ? files.image.name : 'Click để tải ảnh lên'}</p>
              <p className="text-xs text-gray-400 mt-1">PNG, JPG (Max 5MB)</p>
            </label>

            {(selectedImagePreviewUrl || existingImageUrl) && (
              <div className="mt-4">
                <p className="text-xs font-semibold text-gray-500 mb-2">Xem trước ảnh</p>
                <img
                  src={selectedImagePreviewUrl || existingImageUrl}
                  alt="POI preview"
                  className="w-full h-40 object-cover rounded-2xl border border-gray-200"
                />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default PoiForm;
