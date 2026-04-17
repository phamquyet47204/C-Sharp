import { useState, useEffect } from 'react';
import api from '../services/api';
import Toast, { useToast } from '../components/Toast';
import ConfirmModal from '../components/ConfirmModal';
import PoiDetailModal from '../components/PoiDetailModal';
import { ClipboardCheck, Clock, MapPin, User, CheckCircle2, X, Eye } from 'lucide-react';

export default function Approvals() {
  const [pois, setPois] = useState([]);
  const [loading, setLoading] = useState(true);
  const [confirmModal, setConfirmModal] = useState({ isOpen: false, poiId: null });
  const [detailModal, setDetailModal] = useState({ isOpen: false, poi: null });
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
    setConfirmModal({
      isOpen: true,
      poiId,
      onConfirm: async () => {
        try {
          await api.post(`/admin/pois/${poiId}/approve`);
          setPois(prev => prev.filter(p => p.id !== poiId));
          show('Đã duyệt POI thành công!', 'success');
        } catch (err) {
          show('Lỗi khi duyệt POI: ' + (err.response?.data || err.message), 'error');
        } finally {
          setConfirmModal(prev => ({ ...prev, isOpen: false }));
        }
      }
    });
  };

  const handleViewDetail = async (poiId) => {
    try {
      const res = await api.get(`/admin/pois/${poiId}`);
      // Find the summary info to combine (ownerName is not in the single POI get)
      const summary = pois.find(p => p.id === poiId);
      setDetailModal({ 
        isOpen: true, 
        poi: { ...res.data, ownerName: summary?.ownerName } 
      });
    } catch (err) {
      show('Lỗi khi tải chi tiết: ' + (err.response?.data || err.message), 'error');
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

      <ConfirmModal
        isOpen={confirmModal.isOpen}
        onClose={() => setConfirmModal(prev => ({ ...prev, isOpen: false }))}
        onConfirm={confirmModal.onConfirm}
        title="Duyệt địa điểm"
        message="Bạn có chắc chắn muốn duyệt POI này không? Sau khi duyệt, địa điểm sẽ xuất hiện trên bản đồ ứng dụng."
        type="success"
      />

      <PoiDetailModal
        isOpen={detailModal.isOpen}
        onClose={() => setDetailModal(prev => ({ ...prev, isOpen: false }))}
        poi={detailModal.poi}
      />

      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Duyệt POI</h1>
          <p className="text-gray-500 text-sm mt-1">Phê duyệt các địa điểm mới từ chủ quán</p>
        </div>
        <div className="bg-coral-50 px-4 py-2 rounded-lg text-coral-600 font-semibold text-sm border border-coral-100">
          {pois.length} Yêu cầu
        </div>
      </div>

      {pois.length === 0 ? (
        <div className="bg-white rounded-[2rem] p-12 text-center shadow-sm border border-gray-100">
          <div className="bg-gray-50 w-16 h-16 rounded-full flex items-center justify-center mx-auto mb-4">
            <ClipboardCheck className="text-gray-300" size={32} />
          </div>
          <h3 className="text-gray-900 font-semibold text-lg">Hệ thống đã sạch!</h3>
          <p className="text-gray-400 text-sm mt-1">Tất cả các POI đã được xử lý hoặc không có yêu cầu mới.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {pois.map(poi => (
            <div key={poi.id} className="bg-white rounded-[2rem] shadow-sm border border-gray-100 overflow-hidden transition-all hover:shadow-md flex flex-col">
              <div className="relative h-48">
                {poi.imageUrl ? (
                  <img src={poi.imageUrl} alt={poi.name} className="w-full h-full object-cover" />
                ) : (
                  <div className="w-full h-full bg-gray-50 flex items-center justify-center text-gray-300">
                    <MapPin size={48} />
                  </div>
                )}
                <div className="absolute top-4 right-4 bg-white/90 backdrop-blur-md px-3 py-1.5 rounded-xl text-[10px] font-bold text-coral-500 shadow-sm uppercase tracking-wider">
                  Đang chờ duyệt
                </div>
              </div>

              <div className="p-6 flex-1 flex flex-col">
                <h3 className="font-bold text-lg text-gray-900 mb-1 truncate">{poi.name}</h3>
                
                <div className="flex items-center gap-2 text-gray-500 text-xs mb-3">
                  <User size={12} />
                  <span>{poi.ownerName}</span>
                  <span className="text-gray-300">•</span>
                  <Clock size={12} />
                  <span>{new Date(poi.createdAt).toLocaleDateString('vi-VN')}</span>
                </div>

                <p className="text-gray-600 text-sm line-clamp-2 mb-6 flex-1">{poi.description}</p>

                <div className="flex gap-2 mb-4">
                  <button 
                    onClick={() => handleViewDetail(poi.id)}
                    className="flex-1 bg-gray-50 hover:bg-gray-100 text-gray-600 font-bold py-2 rounded-xl transition-colors text-xs flex items-center justify-center gap-2"
                  >
                    <Eye size={14} />
                    Xem chi tiết
                  </button>
                </div>

                <div className="pt-4 border-t border-gray-50 flex gap-3">
                  <button 
                    onClick={() => handleApprove(poi.id)}
                    className="flex-1 bg-coral-500 hover:bg-coral-600 text-white font-bold py-2.5 rounded-xl transition-colors text-sm shadow-sm"
                  >
                    Duyệt
                  </button>
                  <button 
                    onClick={() => setRejectModal({ poiId: poi.id, reason: '' })}
                    className="flex-1 bg-white hover:bg-red-50 text-red-500 border border-red-100 font-bold py-2.5 rounded-xl transition-colors text-sm"
                  >
                    Từ chối
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Rejection Modal with improved styling */}
      {rejectModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div 
            className="absolute inset-0 bg-gray-900/40 backdrop-blur-sm transition-opacity" 
            onClick={() => setRejectModal(null)}
          />
          <div className="relative w-full max-w-md bg-white rounded-[2rem] shadow-2xl border border-white/20 transform transition-all p-8 animate-in zoom-in-95 duration-200">
            <button 
              onClick={() => setRejectModal(null)}
              className="absolute top-6 right-6 text-gray-400 hover:text-gray-600 transition-colors"
            >
              <X size={20} />
            </button>

            <div className="bg-red-50 w-16 h-16 rounded-2xl flex items-center justify-center mb-6">
              <X size={32} className="text-red-500" />
            </div>

            <h3 className="text-xl font-black text-gray-900 mb-2">Lý do từ chối</h3>
            <p className="text-gray-500 text-sm leading-relaxed mb-4">
              Vui lòng cung cấp lý do chi tiết để chủ quán có thể sửa đổi và gửi lại yêu cầu.
            </p>

            <textarea
              className="w-full bg-gray-50 border border-gray-100 rounded-2xl p-4 text-sm resize-none focus:bg-white focus:ring-4 focus:ring-coral-500/10 focus:border-coral-400 transition-all outline-none"
              rows={4}
              placeholder="Nhập lý do từ chối (ít nhất 10 ký tự)..."
              value={rejectModal.reason}
              onChange={e => setRejectModal(prev => ({ ...prev, reason: e.target.value }))}
            />
            <div className="flex justify-between items-center mt-2 mb-8">
               <span className={`text-[10px] font-bold uppercase tracking-widest ${rejectModal.reason.length < 10 ? 'text-red-400' : 'text-emerald-400'}`}>
                 {rejectModal.reason.length} / 10 ký tự tối thiểu
               </span>
            </div>

            <div className="flex gap-3">
              <button 
                onClick={() => setRejectModal(null)}
                className="flex-1 px-4 py-3 text-sm font-bold text-gray-600 bg-gray-50 hover:bg-gray-100 rounded-xl transition-colors"
              >
                Hủy
              </button>
              <button 
                onClick={handleReject}
                className="flex-1 px-4 py-3 text-sm font-bold text-white bg-red-500 hover:bg-red-600 rounded-xl shadow-lg shadow-red-500/25 transition-transform active:scale-95 disabled:bg-gray-400 disabled:shadow-none"
                disabled={rejectModal.reason.length < 10}
              >
                Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
