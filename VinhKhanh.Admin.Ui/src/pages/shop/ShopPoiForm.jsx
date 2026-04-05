import { useState, useEffect } from 'react';
import { Save, ArrowLeft, UploadCloud, Map as MapIcon, Image as ImageIcon, Sparkles } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import MapPicker from '../../components/MapPicker';
import api from '../../services/api';

const categoryOptions = [
  { code: 'FOOD_SNAIL', nameVi: 'Ốc & Hải sản' },
  { code: 'FOOD_BBQ',   nameVi: 'Đồ nướng & Lẩu' },
  { code: 'FOOD_STREET',nameVi: 'Ăn vặt' },
  { code: 'DRINK',      nameVi: 'Đồ uống' },
  { code: 'UTILITY',    nameVi: 'Tiện ích' },
];

export default function ShopPoiForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const isEdit = Boolean(id);

  const [activeTab, setActiveTab] = useState('vi');
  const [formData, setFormData] = useState({
    vi: { name: '', description: '' },
    en: { name: '', description: '' },
    ja: { name: '', description: '' },
    lat: 10.7601, lng: 106.7023, radius: 50, categoryCode: 'FOOD_STREET',
  });
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState('');
  const [existingImageUrl, setExistingImageUrl] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);
  const [errorMsg, setErrorMsg] = useState(null);

  const showError = (msg) => { setErrorMsg(msg); setTimeout(() => setErrorMsg(null), 4000); };

  useEffect(() => {
    if (!isEdit) return;
    api.get(`/shop/pois/${id}`).then(res => {
      const d = res.data;
      setFormData({
        vi: { name: d.vi?.name || '', description: d.vi?.description || '' },
        en: { name: d.en?.name || '', description: d.en?.description || '' },
        ja: { name: d.ja?.name || '', description: d.ja?.description || '' },
        lat: d.lat ?? 10.7601, lng: d.lng ?? 106.7023,
        radius: d.radius ?? 50, categoryCode: d.categoryCode || 'FOOD_STREET',
      });
      if (d.imageUrl) setExistingImageUrl(d.imageUrl.startsWith('http') ? d.imageUrl : `http://localhost:5000${d.imageUrl}`);
    }).catch(() => showError('Không tải được dữ liệu POI.'));
  }, [id, isEdit]);

  useEffect(() => () => { if (imagePreview) URL.revokeObjectURL(imagePreview); }, [imagePreview]);

  const handleLangChange = (lang, field, value) =>
    setFormData(prev => ({ ...prev, [lang]: { ...prev[lang], [field]: value } }));

  const handleImageChange = (file) => {
    if (imagePreview) URL.revokeObjectURL(imagePreview);
    setImageFile(file);
    setImagePreview(file ? URL.createObjectURL(file) : '');
  };

  const handleGenerateAI = async () => {
    if (!formData.vi.name || !formData.vi.description) {
      showError('Vui lòng nhập tên quán và mô tả Tiếng Việt trước khi dùng AI!');
      return;
    }
    setIsGenerating(true);
    try {
      const res = await api.post('/shop/ai/generate', { name: formData.vi.name, description: formData.vi.description });
      const { en, ja } = res.data;
      setFormData(prev => ({
        ...prev,
        en: { name: en?.name || '', description: en?.description || '' },
        ja: { name: ja?.name || '', description: ja?.description || '' },
      }));
      setActiveTab('en');
    } catch (err) {
      showError('Lỗi AI dịch thuật: ' + (err.response?.data || err.message));
    } finally {
      setIsGenerating(false);
    }
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      const data = new FormData();
      data.append('lat', formData.lat);
      data.append('lng', formData.lng);
      data.append('radius', formData.radius);
      data.append('categoryCode', formData.categoryCode);
      data.append('nameVi', formData.vi.name);
      data.append('descVi', formData.vi.description);
      data.append('nameEn', formData.en.name);
      data.append('descEn', formData.en.description);
      data.append('nameJa', formData.ja.name);
      data.append('descJa', formData.ja.description);
      if (imageFile) data.append('image', imageFile);

      if (isEdit) {
        await api.put(`/shop/pois/${id}`, data, { headers: { 'Content-Type': 'multipart/form-data' } });
      } else {
        await api.post('/shop/pois', data, { headers: { 'Content-Type': 'multipart/form-data' } });
      }
      navigate('/shop/pois');
    } catch (err) {
      showError(err.response?.data?.error || err.response?.data || 'Lỗi khi lưu POI.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="max-w-5xl mx-auto space-y-6 pb-20 relative">
      {errorMsg && (
        <div className="fixed top-8 left-1/2 -translate-x-1/2 z-50 bg-red-50 text-red-600 px-6 py-3 rounded-2xl shadow-lg border border-red-100 font-medium flex items-center gap-3">
          <div className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
          {errorMsg}
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <Link to="/shop/pois" className="p-2 hover:bg-white rounded-xl text-gray-500 hover:text-orange-600 transition-colors shadow-sm">
            <ArrowLeft size={20} />
          </Link>
          <div>
            <h2 className="text-3xl font-bold text-gray-900">{isEdit ? 'Chỉnh sửa POI' : 'Thêm POI mới'}</h2>
            <p className="text-gray-500 mt-1">Dùng AI để tự động dịch nội dung sang Anh & Nhật.</p>
          </div>
        </div>
        <button onClick={handleSave} disabled={isSaving}
          className={`${isSaving ? 'bg-gray-400' : 'bg-orange-500 hover:bg-orange-600'} text-white px-8 py-3 rounded-2xl font-semibold flex items-center justify-center gap-2 transition-all shadow-sm w-full md:w-auto`}>
          <Save size={20} />
          {isSaving ? 'Đang lưu...' : 'Lưu thay đổi'}
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: Language tabs */}
        <div className="lg:col-span-2 space-y-6">
          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <div className="flex flex-wrap items-center gap-4 border-b border-gray-100 pb-4 mb-6">
              <div className="flex gap-2">
                {[
                  { key: 'vi', label: '🇻🇳 Tiếng Việt' },
                  { key: 'en', label: '🇬🇧 Tiếng Anh' },
                  { key: 'ja', label: '🇯🇵 Tiếng Nhật' },
                ].map(t => (
                  <button key={t.key} onClick={() => setActiveTab(t.key)}
                    className={`px-4 py-2 rounded-xl font-semibold transition-colors ${activeTab === t.key ? 'bg-orange-50 text-orange-600' : 'text-gray-500 hover:bg-gray-50'}`}>
                    {t.label}
                  </button>
                ))}
              </div>
              <div className="ml-auto">
                <button onClick={handleGenerateAI} disabled={isGenerating}
                  className={`${isGenerating ? 'bg-gray-100 text-gray-400' : 'bg-indigo-50 text-indigo-600 hover:bg-indigo-100 border border-indigo-200'} px-4 py-2 rounded-xl text-sm font-bold flex items-center gap-2 transition-all`}>
                  <Sparkles size={16} className={isGenerating ? 'animate-spin' : ''} />
                  {isGenerating ? 'AI đang dịch...' : '🪄 AI Generate Content'}
                </button>
              </div>
            </div>

            <div className="space-y-5">
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">
                  Tên địa điểm ({activeTab.toUpperCase()})
                </label>
                <input type="text" value={formData[activeTab].name}
                  onChange={e => handleLangChange(activeTab, 'name', e.target.value)}
                  className="w-full px-4 py-3 bg-gray-50 border-transparent focus:border-orange-500 focus:bg-white focus:ring-4 focus:ring-orange-500/10 rounded-2xl transition-all"
                  placeholder="Nhập tên quán..." />
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">
                  Mô tả chi tiết ({activeTab.toUpperCase()})
                </label>
                <textarea rows={5} value={formData[activeTab].description}
                  onChange={e => handleLangChange(activeTab, 'description', e.target.value)}
                  className="w-full px-4 py-3 bg-gray-50 border-transparent focus:border-orange-500 focus:bg-white focus:ring-4 focus:ring-orange-500/10 rounded-2xl transition-all resize-none"
                  placeholder="Nhập mô tả món ăn, không gian, giờ mở cửa..." />
              </div>
            </div>
          </div>

          {/* Map */}
          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center gap-2 mb-6">
              <MapIcon className="text-orange-500" size={24} />
              <h3 className="text-xl font-bold text-gray-900">Bản đồ & Tọa độ</h3>
            </div>
            <MapPicker
              latitude={formData.lat} longitude={formData.lng} radius={formData.radius}
              onChange={pos => setFormData(p => ({ ...p, lat: pos.lat, lng: pos.lng }))} />
          </div>
        </div>

        {/* Right: Category + Image */}
        <div className="space-y-6">
          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <h3 className="text-lg font-bold text-gray-900 mb-4">Phân loại địa điểm</h3>
            <label className="block text-sm font-semibold text-gray-700 mb-2">Loại POI</label>
            <select value={formData.categoryCode}
              onChange={e => setFormData(p => ({ ...p, categoryCode: e.target.value }))}
              className="w-full px-4 py-3 bg-gray-50 border-transparent focus:border-orange-500 focus:bg-white focus:ring-4 focus:ring-orange-500/10 rounded-2xl transition-all">
              {categoryOptions.map(o => (
                <option key={o.code} value={o.code}>{o.nameVi}</option>
              ))}
            </select>
          </div>

          <div className="bg-white rounded-3xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center gap-2 mb-6">
              <ImageIcon className="text-emerald-500" size={24} />
              <h3 className="text-lg font-bold text-gray-900">Hình đại diện</h3>
            </div>
            <label className="border-2 border-dashed border-gray-200 rounded-2xl p-8 text-center hover:bg-gray-50 transition-colors cursor-pointer group block">
              <input type="file" className="hidden" accept="image/png,image/jpeg"
                onChange={e => handleImageChange(e.target.files[0])} />
              <UploadCloud className="mx-auto text-gray-400 group-hover:text-emerald-500 mb-3" size={32} />
              <p className="text-sm text-gray-600 font-medium">{imageFile ? imageFile.name : 'Click để tải ảnh lên'}</p>
              <p className="text-xs text-gray-400 mt-1">PNG, JPG (Max 5MB)</p>
            </label>
            {(imagePreview || existingImageUrl) && (
              <div className="mt-4">
                <p className="text-xs font-semibold text-gray-500 mb-2">Xem trước ảnh</p>
                <img src={imagePreview || existingImageUrl} alt="preview"
                  className="w-full h-40 object-cover rounded-2xl border border-gray-200" />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
