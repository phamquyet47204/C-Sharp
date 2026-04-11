import { useState, useEffect } from 'react';
import { UserPlus, Trash2, CheckCircle, Clock } from 'lucide-react';
import api from '../services/api';
import Toast, { useToast } from '../components/Toast';
import ConfirmDialog, { useConfirm } from '../components/ConfirmDialog';

// Form tạo chủ quán mới
function CreateShopOwnerForm({ onCreated }) {
  const [form, setForm] = useState({ email: '', password: '', fullName: '', phoneNumber: '' });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSaving(true);
    try {
      await api.post('/admin/shop-owners', form);
      setForm({ email: '', password: '', fullName: '', phoneNumber: '' });
      onCreated();
    } catch (err) {
      setError(err?.response?.data?.error || 'Tạo tài khoản thất bại.');
    } finally {
      setSaving(false);
    }
  };

  const field = (label, key, type = 'text', placeholder = '') => (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">{label}</label>
      <input
        type={type}
        required={key !== 'phoneNumber'}
        value={form[key]}
        onChange={e => setForm(f => ({ ...f, [key]: e.target.value }))}
        placeholder={placeholder}
        className="w-full rounded-xl border border-gray-200 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
      />
    </div>
  );

  return (
    <form onSubmit={handleSubmit} className="bg-white rounded-3xl border border-gray-100 shadow-sm p-6 space-y-4">
      <div className="flex items-center gap-2 mb-2">
        <UserPlus size={20} className="text-orange-500" />
        <h3 className="text-lg font-bold text-gray-900">Tạo chủ quán mới</h3>
      </div>
      <p className="text-xs text-gray-400 -mt-2">Tài khoản được tạo bởi Admin sẽ được kích hoạt ngay lập tức.</p>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {field('Họ tên *', 'fullName', 'text', 'Nguyễn Văn A')}
        {field('Email *', 'email', 'email', 'chuquan@example.com')}
        {field('Mật khẩu *', 'password', 'password', 'Ít nhất 6 ký tự')}
        {field('Số điện thoại', 'phoneNumber', 'tel', '0901234567')}
      </div>

      {error && (
        <p className="text-sm text-red-600 bg-red-50 rounded-xl px-4 py-2">{error}</p>
      )}

      <button
        type="submit"
        disabled={saving}
        className="rounded-xl bg-orange-500 px-6 py-2.5 text-sm font-semibold text-white hover:bg-orange-600 disabled:opacity-50 transition-colors"
      >
        {saving ? 'Đang tạo...' : 'Tạo tài khoản'}
      </button>
    </form>
  );
}

// Danh sách chủ quán
export default function ShopOwners() {
  const [owners, setOwners] = useState([]);
  const [loading, setLoading] = useState(true);
  const { toast, show } = useToast();
  const { confirm, confirmProps } = useConfirm();

  const fetchOwners = async () => {
    try {
      const res = await api.get('/admin/shop-owners');
      setOwners(res.data);
    } catch {
      show('Không tải được danh sách chủ quán.', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchOwners(); }, []);

  const handleApprove = async (owner) => {
    try {
      await api.post(`/admin/shop-owners/${owner.id}/approve`);
      setOwners(prev => prev.map(o => o.id === owner.id ? { ...o, isApproved: true } : o));
      show(`Đã duyệt tài khoản ${owner.email}.`, 'success');
    } catch (err) {
      show(err?.response?.data?.error || 'Duyệt thất bại.', 'error');
    }
  };

  const handleDelete = async (owner) => {
    const ok = await confirm({
      title: 'Xóa chủ quán',
      message: `Xóa tài khoản "${owner.fullName || owner.email}"? Hành động này không thể hoàn tác.`,
    });
    if (!ok) return;
    try {
      await api.delete(`/admin/shop-owners/${owner.id}`);
      setOwners(prev => prev.filter(o => o.id !== owner.id));
      show(`Đã xóa tài khoản ${owner.email}.`, 'success');
    } catch (err) {
      show(err?.response?.data?.error || 'Xóa thất bại.', 'error');
    }
  };

  const approved = owners.filter(o => o.isApproved);
  const pending = owners.filter(o => !o.isApproved);

  return (
    <section className="space-y-6">
      <Toast toast={toast} />
      <ConfirmDialog {...confirmProps} confirmLabel="Xóa" />

      <header>
        <h2 className="text-3xl font-bold text-gray-900">Quản lý chủ quán</h2>
        <p className="text-sm text-gray-500 mt-1">Tạo tài khoản và quản lý danh sách chủ quán.</p>
      </header>

      <CreateShopOwnerForm onCreated={() => { fetchOwners(); show('Tạo tài khoản thành công!', 'success'); }} />

      {/* Chờ duyệt */}
      {pending.length > 0 && (
        <div className="bg-white rounded-3xl border border-yellow-100 shadow-sm overflow-hidden">
          <div className="px-6 py-4 border-b border-yellow-100 bg-yellow-50/50 flex items-center gap-2">
            <Clock size={16} className="text-yellow-600" />
            <h3 className="font-semibold text-yellow-800">Chờ duyệt ({pending.length})</h3>
          </div>
          <div className="divide-y divide-gray-100">
            {pending.map(o => (
              <OwnerRow key={o.id} owner={o} onApprove={handleApprove} onDelete={handleDelete} />
            ))}
          </div>
        </div>
      )}

      {/* Đã duyệt */}
      <div className="bg-white rounded-3xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-2">
          <CheckCircle size={16} className="text-green-600" />
          <h3 className="font-semibold text-gray-800">Đã kích hoạt ({approved.length})</h3>
        </div>
        {loading ? (
          <p className="px-6 py-8 text-sm text-gray-400">Đang tải...</p>
        ) : approved.length === 0 ? (
          <p className="px-6 py-8 text-sm text-gray-400">Chưa có chủ quán nào.</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {approved.map(o => (
              <OwnerRow key={o.id} owner={o} onDelete={handleDelete} />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function OwnerRow({ owner, onApprove, onDelete }) {
  return (
    <div className="flex items-center gap-4 px-6 py-4 hover:bg-gray-50 transition-colors">
      {/* Avatar */}
      <div className="w-10 h-10 rounded-2xl bg-orange-100 flex items-center justify-center text-orange-600 font-bold text-sm flex-shrink-0">
        {(owner.fullName || owner.email)[0].toUpperCase()}
      </div>

      <div className="flex-1 min-w-0">
        <p className="font-semibold text-gray-900 truncate">{owner.fullName || '—'}</p>
        <p className="text-sm text-gray-500 truncate">{owner.email}</p>
        {owner.phoneNumber && <p className="text-xs text-gray-400">{owner.phoneNumber}</p>}
      </div>

      <span className={`text-xs px-3 py-1 rounded-full font-semibold flex-shrink-0 ${
        owner.isApproved ? 'bg-green-100 text-green-700' : 'bg-yellow-100 text-yellow-700'
      }`}>
        {owner.isApproved ? 'Đã duyệt' : 'Chờ duyệt'}
      </span>

      <div className="flex items-center gap-2 flex-shrink-0">
        {!owner.isApproved && onApprove && (
          <button
            onClick={() => onApprove(owner)}
            className="p-2 text-gray-400 hover:text-green-600 hover:bg-green-50 rounded-xl transition-colors"
            title="Duyệt tài khoản"
          >
            <CheckCircle size={18} />
          </button>
        )}
        <button
          onClick={() => onDelete(owner)}
          className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-colors"
          title="Xóa tài khoản"
        >
          <Trash2 size={18} />
        </button>
      </div>
    </div>
  );
}
