import { useState, useEffect } from 'react';
import api from '../services/api';
import Toast, { useToast } from '../components/Toast';

export default function Approvals() {
  const [pois, setPois] = useState([]);
  const [loading, setLoading] = useState(true);
  const [rejectModal, setRejectModal] = useState(null);
  const { toast, show } = useToast();

  const fetchPending = async () => {
    try {
      const res = await api.get('/admin/pois/pending');
      setPois(res.data);
    } catch (err) {
      console.error('Lỗi tải danh sách chờ duyệt:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchPending(); }, []);

  const handleApprove = async (poiId) => {
    try {
      await api.post(`/admin/pois/${poiId}/approve`);
      setPois(prev => prev.filter(p => p.id !== poiId));
      show('Đã duyệt POI thành công!', 'success');
    } catch (err) {
      show('Lỗi khi duyệt POI: ' + (err.response?.data || err.message), 'error');
    }
  };

  const handleReject = async () => {
    if (!rejectModal) return;
    if (!rejectModal.reason || rejectModal.reason.length < 10) {
      show('Lý do từ chối phải có ít nhất 10 ký tự.', 'warn');
      return;
    }
    try {
      await api.post(`/admin/pois/${rejectModal.poiId}/reject`, { reason: rejectModal.reason });
      setPois(prev => prev.filter(p => p.id !== rejectModal.poiId));
      setRejectModal(null);
      show('Đã từ chối POI.', 'success');
    } catch (err) {
      show('Lỗi khi từ chối POI: ' + (err.response?.data?.error || err.message), 'error');
    }
  };

  if (loading) return <div className="p-6 text-gray-500">Đang tải...</div>;

  return (
    <div className="p-6">
      <Toast toast={toast} />
      <h1 className="text-2xl font-bold mb-6">Duyệt POI</h1>

      {pois.length === 0 ? (
        <div className="bg-white rounded-xl p-8 text-center text-gray-400">
          Không có POI nào đang chờ duyệt
        </div>
      ) : (
        <div className="space-y-4">
          {pois.map(poi => (
            <div key={poi.id} className="bg-white rounded-xl shadow p-4 flex gap-4">
              {poi.imageUrl && (
                <img src={poi.imageUrl} alt={poi.name}
                  className="w-24 h-24 object-cover rounded-lg flex-shrink-0" />
              )}
              <div className="flex-1 min-w-0">
                <h3 className="font-semibold text-lg truncate">{poi.name}</h3>
                <p className="text-sm text-gray-500 mb-1">Chủ quán: {poi.ownerName}</p>
                <p className="text-sm text-gray-600 line-clamp-2">{poi.description}</p>
                <p className="text-xs text-gray-400 mt-1">
                  📍 {poi.lat?.toFixed(5)}, {poi.lng?.toFixed(5)}
                </p>
              </div>
              <div className="flex flex-col gap-2 flex-shrink-0">
                <button onClick={() => handleApprove(poi.id)}
                  className="px-4 py-2 bg-green-500 text-white rounded-lg text-sm hover:bg-green-600">
                  ✓ Duyệt
                </button>
                <button onClick={() => setRejectModal({ poiId: poi.id, reason: '' })}
                  className="px-4 py-2 bg-red-500 text-white rounded-lg text-sm hover:bg-red-600">
                  ✗ Từ chối
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modal từ chối */}
      {rejectModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl p-6 w-full max-w-md shadow-xl">
            <h3 className="font-bold text-lg mb-3">Lý do từ chối</h3>
            <textarea
              className="w-full border rounded-lg p-3 text-sm resize-none"
              rows={4}
              placeholder="Nhập lý do từ chối (ít nhất 10 ký tự)..."
              value={rejectModal.reason}
              onChange={e => setRejectModal(prev => ({ ...prev, reason: e.target.value }))}
            />
            <p className="text-xs text-gray-400 mt-1">{rejectModal.reason.length} ký tự</p>
            <div className="flex gap-3 mt-4 justify-end">
              <button onClick={() => setRejectModal(null)}
                className="px-4 py-2 bg-gray-200 rounded-lg text-sm">Hủy</button>
              <button onClick={handleReject}
                className="px-4 py-2 bg-red-500 text-white rounded-lg text-sm hover:bg-red-600">
                Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
