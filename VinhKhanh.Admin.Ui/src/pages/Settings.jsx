<<<<<<< HEAD
import React from 'react';

const Settings = () => {
  return (
    <section className="space-y-6">
      <header>
        <h2 className="text-3xl font-bold text-gray-900">Cai dat</h2>
        <p className="text-sm text-gray-500 mt-2">
          Khu vuc cau hinh he thong se duoc mo rong o cac ban tiep theo.
        </p>
      </header>

      <div className="rounded-3xl border border-gray-100 bg-white p-8 shadow-sm">
        <p className="text-gray-700 leading-relaxed">
          Hien tai phan cai dat da co route rieng de tranh bi chuyen huong sai sang trang login.
          Ban co the bo sung cac cau hinh nhu ngon ngu, phan quyen va tuy chinh he thong tai day.
        </p>
=======
import React, { useState, useEffect } from 'react';
import api from '../services/api';

// Lấy role từ JWT token trong localStorage
function getRoleFromToken() {
  try {
    const token = localStorage.getItem('token');
    if (!token) return null;
    const payload = JSON.parse(atob(token.split('.')[1]));
    const roleKey = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
    return payload[roleKey] || payload.role || null;
  } catch {
    return null;
  }
}

// --- Tab đổi mật khẩu của chính mình ---
function ChangePasswordTab() {
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' });
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState(null); // { type: 'success'|'error', text }

  const handleSubmit = async (e) => {
    e.preventDefault();
    setMessage(null);

    if (form.newPassword !== form.confirmPassword) {
      setMessage({ type: 'error', text: 'Mật khẩu mới và xác nhận không khớp.' });
      return;
    }
    if (form.newPassword.length < 6) {
      setMessage({ type: 'error', text: 'Mật khẩu mới phải có ít nhất 6 ký tự.' });
      return;
    }

    setLoading(true);
    try {
      await api.post('/auth/change-password', {
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
      });
      setMessage({ type: 'success', text: 'Đổi mật khẩu thành công!' });
      setForm({ currentPassword: '', newPassword: '', confirmPassword: '' });
    } catch (err) {
      const errMsg = err.response?.data?.error || 'Đổi mật khẩu thất bại. Kiểm tra lại mật khẩu hiện tại.';
      setMessage({ type: 'error', text: errMsg });
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4 max-w-md">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Mật khẩu hiện tại</label>
        <input
          type="password"
          required
          value={form.currentPassword}
          onChange={e => setForm(f => ({ ...f, currentPassword: e.target.value }))}
          className="w-full rounded-xl border border-gray-200 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
          placeholder="Nhập mật khẩu hiện tại"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Mật khẩu mới</label>
        <input
          type="password"
          required
          value={form.newPassword}
          onChange={e => setForm(f => ({ ...f, newPassword: e.target.value }))}
          className="w-full rounded-xl border border-gray-200 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
          placeholder="Ít nhất 6 ký tự"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Xác nhận mật khẩu mới</label>
        <input
          type="password"
          required
          value={form.confirmPassword}
          onChange={e => setForm(f => ({ ...f, confirmPassword: e.target.value }))}
          className="w-full rounded-xl border border-gray-200 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
          placeholder="Nhập lại mật khẩu mới"
        />
      </div>

      {message && (
        <p className={`text-sm rounded-lg px-4 py-2 ${message.type === 'success' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-600'}`}>
          {message.text}
        </p>
      )}

      <button
        type="submit"
        disabled={loading}
        className="rounded-xl bg-orange-500 px-6 py-2.5 text-sm font-semibold text-white hover:bg-orange-600 disabled:opacity-50 transition-colors"
      >
        {loading ? 'Đang lưu...' : 'Đổi mật khẩu'}
      </button>
    </form>
  );
}

// --- Tab Admin reset mật khẩu user khác ---
function AdminResetPasswordTab() {
  const [users, setUsers] = useState([]);
  const [loadingUsers, setLoadingUsers] = useState(true);
  const [selected, setSelected] = useState(null); // user đang reset
  const [newPassword, setNewPassword] = useState('');
  const [resetting, setResetting] = useState(false);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    api.get('/admin/users')
      .then(res => setUsers(res.data))
      .catch(() => setUsers([]))
      .finally(() => setLoadingUsers(false));
  }, []);

  const handleReset = async (e) => {
    e.preventDefault();
    if (!selected) return;
    setMessage(null);

    if (newPassword.length < 6) {
      setMessage({ type: 'error', text: 'Mật khẩu mới phải có ít nhất 6 ký tự.' });
      return;
    }

    setResetting(true);
    try {
      await api.post(`/admin/users/${selected.id}/reset-password`, { newPassword });
      setMessage({ type: 'success', text: `Đã reset mật khẩu cho ${selected.email}.` });
      setNewPassword('');
      setSelected(null);
    } catch (err) {
      const errMsg = err.response?.data?.error || 'Reset mật khẩu thất bại.';
      setMessage({ type: 'error', text: errMsg });
    } finally {
      setResetting(false);
    }
  };

  return (
    <div className="space-y-6 max-w-lg">
      {/* Danh sách users */}
      <div>
        <p className="text-sm text-gray-500 mb-3">Chọn tài khoản cần reset mật khẩu:</p>
        {loadingUsers ? (
          <p className="text-sm text-gray-400">Đang tải...</p>
        ) : (
          <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
            {users.map(u => (
              <button
                key={u.id}
                type="button"
                onClick={() => { setSelected(u); setMessage(null); setNewPassword(''); }}
                className={`w-full text-left rounded-xl border px-4 py-3 text-sm transition-colors ${
                  selected?.id === u.id
                    ? 'border-orange-400 bg-orange-50'
                    : 'border-gray-200 bg-white hover:border-orange-300'
                }`}
              >
                <span className="font-medium text-gray-800">{u.fullName || u.email}</span>
                <span className="ml-2 text-xs text-gray-400">{u.email}</span>
                <span className={`ml-2 text-xs px-2 py-0.5 rounded-full ${u.role === 'Admin' ? 'bg-purple-100 text-purple-700' : 'bg-blue-100 text-blue-700'}`}>
                  {u.role}
                </span>
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Form reset */}
      {selected && (
        <form onSubmit={handleReset} className="space-y-4 border-t pt-4">
          <p className="text-sm font-medium text-gray-700">
            Reset mật khẩu cho: <span className="text-orange-600">{selected.email}</span>
          </p>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Mật khẩu mới</label>
            <input
              type="password"
              required
              value={newPassword}
              onChange={e => setNewPassword(e.target.value)}
              className="w-full rounded-xl border border-gray-200 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
              placeholder="Ít nhất 6 ký tự"
            />
          </div>

          {message && (
            <p className={`text-sm rounded-lg px-4 py-2 ${message.type === 'success' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-600'}`}>
              {message.text}
            </p>
          )}

          <div className="flex gap-3">
            <button
              type="submit"
              disabled={resetting}
              className="rounded-xl bg-orange-500 px-6 py-2.5 text-sm font-semibold text-white hover:bg-orange-600 disabled:opacity-50 transition-colors"
            >
              {resetting ? 'Đang reset...' : 'Xác nhận reset'}
            </button>
            <button
              type="button"
              onClick={() => { setSelected(null); setMessage(null); }}
              className="rounded-xl border border-gray-200 px-6 py-2.5 text-sm text-gray-600 hover:bg-gray-50 transition-colors"
            >
              Hủy
            </button>
          </div>
        </form>
      )}

      {!selected && message && (
        <p className={`text-sm rounded-lg px-4 py-2 ${message.type === 'success' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-600'}`}>
          {message.text}
        </p>
      )}
    </div>
  );
}

// --- Main Settings page ---
const Settings = () => {
  const role = getRoleFromToken();
  const isAdmin = role === 'Admin';
  const [activeTab, setActiveTab] = useState('change-password');

  const tabs = [
    { id: 'change-password', label: 'Đổi mật khẩu' },
    ...(isAdmin ? [{ id: 'reset-user', label: 'Reset mật khẩu người dùng' }] : []),
  ];

  return (
    <section className="space-y-6">
      <header>
        <h2 className="text-3xl font-bold text-gray-900">Cài đặt</h2>
        <p className="text-sm text-gray-500 mt-1">Quản lý tài khoản và bảo mật</p>
      </header>

      <div className="rounded-3xl border border-gray-100 bg-white shadow-sm overflow-hidden">
        {/* Tabs */}
        <div className="flex border-b border-gray-100">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`px-6 py-4 text-sm font-medium transition-colors ${
                activeTab === tab.id
                  ? 'border-b-2 border-orange-500 text-orange-600'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div className="p-8">
          {activeTab === 'change-password' && <ChangePasswordTab />}
          {activeTab === 'reset-user' && isAdmin && <AdminResetPasswordTab />}
        </div>
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
      </div>
    </section>
  );
};

export default Settings;
