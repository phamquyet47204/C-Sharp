import React, { useState, useEffect } from 'react';
import api from '../services/api';
import Toast, { useToast } from '../components/Toast';
import ConfirmModal from '../components/ConfirmModal';
import { 
  UserCheck, 
  Clock, 
  Mail, 
  User, 
  Phone, 
  Edit2, 
  Trash2, 
  ShieldCheck, 
  ShieldAlert,
  Search,
  Filter
} from 'lucide-react';

const OwnerManager = () => {
  const [owners, setOwners] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [filterStatus, setFilterStatus] = useState('all'); // all, pending, approved
  const [modalConfig, setModalConfig] = useState({ 
    isOpen: false, 
    type: 'success', 
    title: '', 
    message: '', 
    userId: null 
  });
  const [editModal, setEditModal] = useState({
    isOpen: false,
    user: null,
    premiumOption: 'None' // None, 1Month, 6Months, 1Year
  });
  
  const { toast, show } = useToast();

  const fetchOwners = async () => {
    try {
      setLoading(true);
      const res = await api.get('/admin/users/owners');
      setOwners(res.data);
    } catch (err) {
      show('Không thể tải danh sách chủ quán.', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchOwners();
  }, []);

  const handleApprove = async (userId) => {
    try {
      await api.post(`/admin/approve-owner/${userId}`);
      show('Phê duyệt thành công!', 'success');
      fetchOwners();
    } catch (err) {
      show('Lỗi khi phê duyệt.', 'error');
    }
  };

  const handleReject = async (userId) => {
    setModalConfig({
      isOpen: true,
      type: 'danger',
      title: 'Xác nhận xóa/từ chối',
      message: 'Bạn có chắc chắn muốn xóa/từ chối tài khoản này? Hành động này không thể hoàn tác.',
      userId,
      onConfirm: async () => {
        try {
          await api.post(`/admin/users/${userId}/reject-owner`);
          show('Đã xóa tài khoản thành công.', 'info');
          fetchOwners();
        } catch (err) {
          show('Lỗi khi thực hiện.', 'error');
        } finally {
          setModalConfig(prev => ({ ...prev, isOpen: false }));
        }
      }
    });
  };

  const handleTogglePremium = async (userId) => {
    try {
      const res = await api.post(`/admin/users/${userId}/toggle-premium`);
      show(res.data.isPremium ? 'Đã bật Premium cho quán!' : 'Đã tắt Premium.', 'success');
      fetchOwners();
    } catch (err) {
      show('Cần tạo POI cho chủ quán này trước khi bật Premium.', 'error');
    }
  };

  const handleUpdate = async (e) => {
    e.preventDefault();
    const { id, fullName, phoneNumber } = editModal.user;
    const { premiumOption } = editModal;
    try {
      await api.put(`/admin/users/${id}`, { fullName, phoneNumber, premiumOption });
      show('Cập nhật thông tin thành công!', 'success');
      setEditModal({ isOpen: false, user: null, premiumOption: 'None' });
      fetchOwners();
    } catch (err) {
      show('Lỗi cập nhật.', 'error');
    }
  };

  const filteredOwners = owners.filter(o => {
    const matchSearch = (o.fullName || '').toLowerCase().includes(searchTerm.toLowerCase()) || 
                       (o.email || '').toLowerCase().includes(searchTerm.toLowerCase());
    const matchFilter = filterStatus === 'all' || 
                       (filterStatus === 'pending' && !o.isApproved) || 
                       (filterStatus === 'approved' && o.isApproved);
    return matchSearch && matchFilter;
  });

  if (loading) return (
    <div className="p-8 flex justify-center"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-coral-500"></div></div>
  );

  return (
    <div className="space-y-6">
      <Toast toast={toast} />
      <ConfirmModal {...modalConfig} onClose={() => setModalConfig(prev => ({ ...prev, isOpen: false }))} onConfirm={modalConfig.onConfirm} />
      
      {/* Edit Modal */}
      {editModal.isOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
          <div className="bg-white rounded-[2rem] w-full max-w-md p-8 shadow-2xl">
            <h2 className="text-2xl font-bold mb-6">Chỉnh sửa thông tin</h2>
            <form onSubmit={handleUpdate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Họ và tên</label>
                <input 
                  type="text" 
                  value={editModal.user.fullName} 
                  onChange={e => setEditModal({...editModal, user: {...editModal.user, fullName: e.target.value}})}
                  className="w-full px-4 py-2 rounded-xl border border-gray-200"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Số điện thoại</label>
                <input 
                  type="text" 
                  value={editModal.user.phoneNumber} 
                  onChange={e => setEditModal({...editModal, user: {...editModal.user, phoneNumber: e.target.value}})}
                  className="w-full px-4 py-2 rounded-xl border border-gray-200"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Gói Premium</label>
                <select 
                  value={editModal.premiumOption} 
                  onChange={e => setEditModal({...editModal, premiumOption: e.target.value})}
                  className="w-full px-4 py-2 rounded-xl border border-gray-200"
                >
                  <option value="None">Không (Huỷ Premium)</option>
                  <option value="1Month">1 Tháng</option>
                  <option value="6Months">6 Tháng</option>
                  <option value="1Year">1 Năm</option>
                </select>
                <p className="mt-1 text-xs text-gray-400 italic">Lưu ý: Gia hạn sẽ bắt đầu từ thời điểm hiện tại.</p>
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => setEditModal({isOpen:false})} className="flex-1 py-3 text-gray-500 font-bold">Hủy</button>
                <button type="submit" className="flex-1 py-3 bg-coral-500 text-white rounded-xl font-bold">Lưu thay đổi</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Quản lý Chủ quán</h1>
          <p className="text-gray-500 mt-1">Duyệt yêu cầu và quản lý đối tác kinh doanh.</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" size={18} />
            <input 
              type="text" 
              placeholder="Tìm kiếm..."
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              className="pl-10 pr-4 py-2 bg-white border border-gray-100 rounded-xl text-sm focus:ring-2 focus:ring-coral-500 outline-none w-64 shadow-sm"
            />
          </div>
          <select 
            value={filterStatus}
            onChange={e => setFilterStatus(e.target.value)}
            className="px-4 py-2 bg-white border border-gray-100 rounded-xl text-sm focus:ring-2 focus:ring-coral-500 outline-none shadow-sm"
          >
            <option value="all">Tất cả</option>
            <option value="pending">Chờ duyệt</option>
            <option value="approved">Đã duyệt</option>
          </select>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {filteredOwners.map(owner => (
          <div key={owner.id} className="bg-white rounded-[2rem] p-6 border border-gray-50 shadow-sm hover:shadow-md transition-all">
            <div className="flex items-center gap-4 mb-4">
              <div className={`p-3 rounded-2xl ${owner.isApproved ? 'bg-emerald-50 text-emerald-600' : 'bg-amber-50 text-amber-600'}`}>
                {owner.isApproved ? <UserCheck size={24} /> : <Clock size={24} />}
              </div>
              <div>
                <h3 className="font-bold text-gray-900 line-clamp-1">{owner.fullName}</h3>
                <span className={`text-[10px] font-black uppercase tracking-wider ${owner.isApproved ? 'text-emerald-500' : 'text-amber-500'}`}>
                  {owner.isApproved ? 'Đã kích hoạt' : 'Đang chờ duyệt'}
                </span>
              </div>
            </div>

            <div className="space-y-2 mb-6">
              <div className="flex items-center gap-2 text-sm text-gray-500">
                <Mail size={14} className="shrink-0" /> <span className="truncate">{owner.email}</span>
              </div>
              <div className="flex items-center gap-2 text-sm text-gray-500">
                <Phone size={14} className="shrink-0" /> <span>{owner.phoneNumber || 'N/A'}</span>
              </div>
              {owner.isPremium && (
                <div className="flex items-center gap-2 text-xs font-semibold text-indigo-600 bg-indigo-50 px-2 py-1 rounded-md w-fit">
                  <ShieldCheck size={12} />
                  Hết hạn: {owner.premiumExpiryDate ? new Date(owner.premiumExpiryDate).toLocaleDateString('vi-VN') : 'Không giới hạn'}
                </div>
              )}
            </div>

            <div className="flex flex-wrap gap-2 pt-4 border-t border-gray-50">
              {!owner.isApproved && (
                <button 
                  onClick={() => handleApprove(owner.id)}
                  className="px-4 py-2 bg-coral-500 text-white rounded-xl text-xs font-bold hover:bg-coral-600 transition"
                >
                  Duyệt
                </button>
              )}
              {owner.isApproved && (
                <button 
                  onClick={() => handleTogglePremium(owner.id)}
                  title="Nâng cấp/Huỷ Premium"
                  className="p-2 bg-indigo-50 text-indigo-600 rounded-xl hover:bg-indigo-100 transition"
                >
                  <ShieldCheck size={18} />
                </button>
              )}
              <button 
                onClick={() => setEditModal({isOpen: true, user: owner})}
                className="p-2 bg-gray-50 text-gray-600 rounded-xl hover:bg-gray-100 transition"
              >
                <Edit2 size={18} />
              </button>
              <button 
                onClick={() => handleReject(owner.id)}
                className="p-2 bg-red-50 text-red-600 rounded-xl hover:bg-red-100 transition ml-auto"
              >
                <Trash2 size={18} />
              </button>
            </div>
          </div>
        ))}
      </div>

      {filteredOwners.length === 0 && (
        <div className="text-center py-20 bg-white rounded-[2rem] border border-dashed border-gray-200">
          <User className="mx-auto text-gray-300 mb-4" size={48} />
          <p className="text-gray-500 font-medium">Không tìm thấy thông tin chủ quán nào.</p>
        </div>
      )}
    </div>
  );
};

export default OwnerManager;
