import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ArrowRight,
  Eye,
  EyeOff,
  KeyRound,
  Lock,
  Mail,
} from 'lucide-react';
import api from '../services/api';

const DEFAULT_ADMIN_EMAIL = 'admin@vinhkhanh.vn';
const DEFAULT_ADMIN_PASSWORD = 'Admin123!';

const Login = () => {
  const [email, setEmail] = useState(DEFAULT_ADMIN_EMAIL);
  const [password, setPassword] = useState(DEFAULT_ADMIN_PASSWORD);
  const [errorMsg, setErrorMsg] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [rememberEmail, setRememberEmail] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const savedEmail = localStorage.getItem('adminEmail');
    if (savedEmail) {
      setEmail(savedEmail);
      setRememberEmail(true);
      return;
    }

    setEmail(DEFAULT_ADMIN_EMAIL);
    setPassword(DEFAULT_ADMIN_PASSWORD);
  }, []);

  const handleLogin = async (e) => {
    e.preventDefault();
    const normalizedEmail = email.trim();

    if (!normalizedEmail || !password) {
      setErrorMsg("Vui lòng nhập Email và Mật khẩu!");
      return;
    }

    setIsLoading(true);
    setErrorMsg('');

    try {
      const response = await api.post('/auth/login', {
        email: normalizedEmail,
        password,
      });

      const { token, roles } = response.data;
      if (token) {
        localStorage.setItem('token', token);
        if (rememberEmail) {
          localStorage.setItem('adminEmail', normalizedEmail);
        } else {
          localStorage.removeItem('adminEmail');
        }
        // Redirect theo role
        if (Array.isArray(roles) && roles.includes('ShopOwner')) {
          navigate('/shop/dashboard', { replace: true });
        } else {
          navigate('/dashboard', { replace: true });
        }
        return;
      }

      setErrorMsg('Không nhận được token đăng nhập từ hệ thống.');
    } catch (err) {
      const responseMessage = err.response?.data;
      const message = typeof responseMessage === 'string'
        ? responseMessage
        : responseMessage?.message;

      setErrorMsg(message || 'Sai tài khoản, mật khẩu hoặc đang chờ duyệt.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen overflow-hidden bg-[radial-gradient(circle_at_top_left,_#fff1ec,_transparent_35%),radial-gradient(circle_at_top_right,_#fff7ed,_transparent_25%),linear-gradient(180deg,_#fffaf7,_#fff)] text-gray-900">
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute left-[-8rem] top-[-6rem] h-72 w-72 rounded-full bg-coral-100/70 blur-3xl" />
        <div className="absolute right-[-5rem] top-24 h-80 w-80 rounded-full bg-amber-100/70 blur-3xl" />
        <div className="absolute bottom-[-7rem] left-1/3 h-72 w-72 rounded-full bg-coral-50 blur-3xl" />
      </div>

      <div className="relative mx-auto flex min-h-screen w-full max-w-2xl items-center px-4 py-10 sm:px-6 lg:px-8">
        <section className="w-full">
          <div className="rounded-[2rem] border border-white/80 bg-white/85 p-6 shadow-[0_20px_80px_rgba(15,23,42,0.12)] backdrop-blur-xl sm:p-8 lg:p-10">
            <div className="mb-8 flex items-center justify-between gap-4">
              <div>
                <div className="mb-3 inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-coral-500 text-white shadow-lg shadow-coral-500/25">
                  <KeyRound className="h-7 w-7" />
                </div>
                <h2 className="text-2xl font-black tracking-tight text-gray-900 sm:text-3xl">
                  Vinh Khanh Admin Center
                </h2>
                <p className="mt-2 text-sm leading-6 text-gray-600">
                  Đăng nhập để tiếp tục vào khu vực quản trị.
                </p>
              </div>
              <div className="hidden rounded-2xl bg-coral-50 px-3 py-2 text-sm font-semibold text-coral-700 sm:block">
                Admin only
              </div>
            </div>

            {errorMsg && (
              <div className="mb-6 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-medium text-red-700 shadow-sm">
                {errorMsg}
              </div>
            )}

            <form className="space-y-5" onSubmit={handleLogin}>
              <div>
                <label className="mb-2 block text-sm font-semibold text-gray-700">
                  Địa chỉ email
                </label>
                <div className="relative">
                  <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4 text-coral-500">
                    <Mail className="h-4 w-4" />
                  </div>
                  <input
                    type="email"
                    autoComplete="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="block w-full rounded-2xl border border-gray-200 bg-gray-50 py-3 pl-11 pr-4 text-sm font-medium text-gray-900 outline-none transition focus:border-coral-400 focus:bg-white focus:ring-4 focus:ring-coral-500/10"
                    placeholder="admin@vinhkhanh.vn"
                  />
                </div>
              </div>

              <div>
                <label className="mb-2 block text-sm font-semibold text-gray-700">
                  Mật khẩu
                </label>
                <div className="relative">
                  <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4 text-coral-500">
                    <Lock className="h-4 w-4" />
                  </div>
                  <input
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="current-password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="block w-full rounded-2xl border border-gray-200 bg-gray-50 py-3 pl-11 pr-12 text-sm font-medium text-gray-900 outline-none transition focus:border-coral-400 focus:bg-white focus:ring-4 focus:ring-coral-500/10"
                    placeholder="Nhập mật khẩu"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((currentValue) => !currentValue)}
                    className="absolute inset-y-0 right-0 flex items-center px-4 text-gray-400 transition hover:text-gray-600"
                    aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  >
                    {showPassword ? (
                      <EyeOff className="h-4 w-4" />
                    ) : (
                      <Eye className="h-4 w-4" />
                    )}
                  </button>
                </div>
              </div>

              <div className="flex items-center justify-between gap-4 text-sm">
                <label className="flex items-center gap-2 text-gray-600">
                  <input
                    type="checkbox"
                    checked={rememberEmail}
                    onChange={(e) => setRememberEmail(e.target.checked)}
                    className="h-4 w-4 rounded border-gray-300 text-coral-500 focus:ring-coral-500"
                  />
                  Ghi nhớ email đăng nhập
                </label>

                <button
                  type="button"
                  className="font-semibold text-coral-600 transition hover:text-coral-700"
                  onClick={() => setErrorMsg('Vui lòng liên hệ quản trị viên nếu cần cấp lại mật khẩu.')}
                >
                  Cần hỗ trợ?
                </button>
              </div>

              <button
                type="submit"
                disabled={isLoading}
                className="group inline-flex w-full items-center justify-center gap-2 rounded-2xl bg-coral-500 px-4 py-3.5 text-sm font-bold text-white shadow-lg shadow-coral-500/25 transition hover:bg-coral-600 focus:outline-none focus:ring-4 focus:ring-coral-500/25 disabled:cursor-not-allowed disabled:bg-gray-400"
              >
                {isLoading ? 'Đang xác thực...' : 'Truy cập hệ thống'}
                {!isLoading && <ArrowRight className="h-4 w-4 transition group-hover:translate-x-0.5" />}
              </button>
            </form>
          </div>
        </section>
      </div>
    </div>
  );
};

export default Login;
