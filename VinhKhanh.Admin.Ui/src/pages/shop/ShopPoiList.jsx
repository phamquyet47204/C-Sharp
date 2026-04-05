import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../../services/api';
import Toast, { useToast } from '../../components/Toast';

const statusColors = {
  Draft: 'bg-gray-100 text-gray-600',
  Pending_Approval: 'bg-yellow-100 text-yellow-700',
  Approved: 'bg-green-100 text-green-700',
  Rejected: 'bg-red-100 text-red-700',
  Hidden: 'bg-purple-100 text-purple-700',
};

const statusLabels = {
  Draft: 'Nháp', Pending_Approval: 'Chờ duyệt',
  Approved: 'Đã duyệt', Rejected: 'Bị từ chối', Hidden: 'Đã ẩn',
};

export default function ShopPoiList() {
  const [pois, setPois] = useState([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();
  const { toast, show } = useToast();

  useEffect(() => {
    api.get('/shop/pois').then(res => setPois(res.data)).catch(console.error).finally(() => setLoading(false));
  }, []);

  const handleSubmit = async (poiId) => {
    try {
      await api.post(`/shop/pois/${poiId}/submit`);
      setPois(prev => prev.map(p => p.id === poiId ? { ...p, status: 'Pending_Approval' } : p));
      show('Đã gửi duyệt thành công!', 'success');
    } catch (err) {
      show('Lỗi: ' + (err.response?.data || err.message), 'error');
    }
  };

  const handleDelete = async (poiId) => {
    if (!window.confirm('Bạn có chắc muốn xóa POI này không?')) return;
    try {
      await api.delete(`/shop/pois/${poiId}`);
      setPois(prev => prev.filter(p => p.id !== poiId));
      show('Đã xóa POI thành công.', 'success');
    } catch (err) {
      show('Lỗi: ' + (err.response?.data || err.message), 'error');
    }
  };

  const canEdit = (status) => status !== 'Pending_Approval';
  const canDelete = (status) => status !== 'Pending_Approval';

  if (loading) return <div className="p-6 text-gray-500">Đang tải...</div>;

  return (
    <div className="p-6">
      <Toast toast={toast} />
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Quản lý POI</h1>
        <button onClick={() => navigate('/shop/pois/new')}
          className="px-4 py-2 bg-orange-500 text-white rounded-lg text-sm hover:bg-orange-600">
          + Thêm POI mới
        </button>
      </div>

      {pois.length === 0 ? (
        <div className="bg-white rounded-xl p-8 text-center text-gray-400">Chưa có POI nào</div>
      ) : (
        <div className="space-y-3">
          {pois.map(poi => (
            <div key={poi.id} className="bg-white rounded-xl shadow-sm p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="font-semibold truncate">{poi.name}</h3>
                    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${statusColors[poi.status] || 'bg-gray-100'}`}>
                      {statusLabels[poi.status] || poi.status}
                    </span>
                  </div>
                  {poi.status === 'Rejected' && poi.rejectionReason && (
                    <p className="text-sm text-red-500 mt-1">❌ Lý do từ chối: {poi.rejectionReason}</p>
                  )}
                </div>
                <div className="flex gap-2 flex-shrink-0">
                  {canEdit(poi.status) && (
                    <>
                      <button onClick={() => navigate(`/shop/pois/${poi.id}/edit`)}
                        className="px-3 py-1.5 bg-gray-100 text-gray-700 rounded-lg text-sm hover:bg-gray-200">
                        Sửa
                      </button>
                      {(poi.status === 'Draft' || poi.status === 'Rejected') && (
                        <button onClick={() => handleSubmit(poi.id)}
                          className="px-3 py-1.5 bg-orange-500 text-white rounded-lg text-sm hover:bg-orange-600">
                          Gửi duyệt
                        </button>
                      )}
                    </>
                  )}
                  {canDelete(poi.status) && (
                    <button onClick={() => handleDelete(poi.id)}
                      className="px-3 py-1.5 bg-red-100 text-red-600 rounded-lg text-sm hover:bg-red-200">
                      Xóa
                    </button>
                  )}
                  {!canEdit(poi.status) && (
                    <span className="text-xs text-gray-400 self-center">Đang chờ duyệt...</span>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
