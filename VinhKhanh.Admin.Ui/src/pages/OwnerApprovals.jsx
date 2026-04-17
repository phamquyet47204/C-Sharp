import { useState, useEffect } from 'react';
import api from '../services/api';
import Toast, { useToast } from '../components/Toast';
import ConfirmModal from '../components/ConfirmModal';
import { UserCheck, Clock, Mail, User } from 'lucide-react';

export default function OwnerApprovals() {
  const [owners, setOwners] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modalConfig, setModalConfig] = useState({ 
    isOpen: false, 
    type: 'success', 
    title: '', 
    message: '', 
    userId: null 
  });
  const { toast, show } = useToast();

  const fetchPending = async () => {
    try {
      const res = await api.get('/admin/users/pending-owners');
      setOwners(res.data);
    } catch (err) {
      console.error('Lỗi tải danh sách chờ duyệt:', err);
      show('Không thể tải danh sách chủ quán.', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchPending(); }, []);

  const handleApprove = async (userId) => {
    setModalConfig({
      isOpen: true,
      type: 'success',
      title: 'Phê duyệt chủ quán',
      message: 'Bạn có chắc chắn muốn duyệt tài khoản này không? Sau khi duyệt, chủ quán có thể đăng nhập vào hệ thống.',
      userId,
      onConfirm: async () => {
        try {
          await api.post(`/admin/users/${userId}/approve-owner`);
          setOwners(prev => prev.filter(o => o.id !== userId));
          show('Đã duyệt chủ quán thành công!', 'success');
        } catch (err) {
          show('Lỗi khi duyệt chủ quán: ' + (err.response?.data || err.message), 'error');
        } finally {
          setModalConfig(prev => ({ ...prev, isOpen: false }));
        }
      }
    });
  };

  const handleReject = async (userId) => {
    setModalConfig({
      isOpen: true,
      type: 'danger',
      title: 'Từ chối ứng viên',
      message: 'Bạn có chắc chắn muốn TỪ CHỐI ứng viên này? Tài khoản sẽ bị xóa khỏi hệ thống và không thể khôi phục.',
      userId,
      onConfirm: async () => {
        try {
          await api.post(`/admin/users/${userId}/reject-owner`);
          setOwners(prev => prev.filter(o => o.id !== userId));
          show('Đã từ chối ứng viên thành công.', 'info');
        } catch (err) {
          show('Lỗi khi từ chối: ' + (err.response?.data || err.message), 'error');
        } finally {
          setModalConfig(prev => ({ ...prev, isOpen: false }));
        }
      }
    });
  };

  if (loading) return (
    <div className="p-6 flex justify-center items-center h-64">
      <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-coral-500"></div>
    </div>
  );

  return (
    <div className="p-6">
      <Toast toast={toast} />
      
      <ConfirmModal
        {...modalConfig}
        onClose={() => setModalConfig(prev => ({ ...prev, isOpen: false }))}
        onConfirm={modalConfig.onConfirm}
      />
      
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Duyệt Shop Owner</h1>
          <p className="text-gray-500 text-sm mt-1">Quản lý các yêu cầu đăng ký kinh doanh mới</p>
        </div>
        <div className="bg-coral-50 px-4 py-2 rounded-lg text-coral-600 font-semibold text-sm border border-coral-100">
          {owners.length} Yêu cầu đang chờ
        </div>
      </div>

      {owners.length === 0 ? (
        <div className="bg-white rounded-2xl p-12 text-center shadow-sm border border-gray-100">
          <div className="bg-gray-50 w-16 h-16 rounded-full flex items-center justify-center mx-auto mb-4">
            <UserCheck className="text-gray-300" size={32} />
          </div>
          <h3 className="text-gray-900 font-semibold text-lg">Mọi thứ đã được xử lý!</h3>
          <p className="text-gray-400 text-sm mt-1">Không có tài khoản chủ quán nào đang chờ duyệt.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {owners.map(owner => (
            <div key={owner.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden transition-all hover:shadow-md">
              <div className="p-6">
                <div className="flex items-start justify-between mb-4">
                  <div className="bg-coral-100 p-3 rounded-xl text-coral-600">
                    <User size={24} />
                  </div>
                  <div className="flex flex-col items-end">
                    <span className="text-[10px] uppercase tracking-wider font-bold text-coral-400 mb-1">Ngày đăng ký</span>
                    <div className="flex items-center gap-1 text-gray-500 text-xs bg-gray-50 px-2 py-1 rounded-md">
                      <Clock size={12} />
                      {new Date(owner.activationDate).toLocaleDateString('vi-VN')}
                    </div>
                  </div>
                </div>

                <h3 className="font-bold text-lg text-gray-900 mb-1">{owner.fullName}</h3>
                
                <div className="flex items-center gap-2 text-gray-500 text-sm mb-6">
                  <Mail size={14} />
                  <span className="truncate">{owner.email}</span>
                </div>

                <div className="pt-4 border-t border-gray-50 flex gap-3">
                  <button 
                    onClick={() => handleApprove(owner.id)}
                    className="flex-1 bg-coral-500 hover:bg-coral-600 text-white font-bold py-2.5 rounded-xl transition-colors text-sm shadow-sm"
                  >
                    Duyệt
                  </button>
                  <button 
                    onClick={() => handleReject(owner.id)}
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
    </div>
  );
}
