import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import {
  ArrowRight,
  User,
  Lock,
  Mail,
  Store,
  CheckCircle2,
} from 'lucide-react';
import api from '../services/api';

const Register = () => {
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMsg, setErrorMsg] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);
  const navigate = useNavigate();

  const handleRegister = async (e) => {
    e.preventDefault();
    setErrorMsg('');

    if (!fullName || !email || !password) {
      setErrorMsg("Vui lòng nhập đầy đủ thông tin!");
      return;
    }

    if (password.length < 6) {
      setErrorMsg("Mật khẩu phải có ít nhất 6 ký tự.");
      return;
    }

    setIsLoading(true);

    try {
      await api.post('/auth/register-shop', {
        fullName: fullName.trim(),
        email: email.trim(),
        password,
      });

      setIsSuccess(true);
      setTimeout(() => navigate('/login'), 5000);
    } catch (err) {
      const responseMessage = err.response?.data;
      const message = typeof responseMessage === 'string'
        ? responseMessage
        : responseMessage?.message;

      setErrorMsg(message || 'Không thể đăng ký lúc này. Vui lòng thử lại sau.');
      setIsLoading(false);
    }
  };

  if (isSuccess) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[radial-gradient(circle_at_top_left,_#fff1ec,_transparent_35%),linear-gradient(180deg,_#fffaf7,_#fff)] p-4">
        <div className="max-w-md w-full bg-white rounded-[2rem] p-10 shadow-[0_20px_80px_rgba(15,23,42,0.12)] text-center">
          <div className="inline-flex h-20 w-20 items-center justify-center rounded-full bg-green-50 text-green-500 mb-6">
            <CheckCircle2 size={40} />
          </div>
          <h2 className="text-2xl font-black text-gray-900 mb-4">Đăng ký thành công!</h2>
          <p className="text-gray-600 mb-8 leading-relaxed">
            Yêu cầu đối tác của bạn đã được gửi đi. Vui lòng chờ Admin phê duyệt tài khoản trước khi có thể đăng nhập.
          </p>
          <button
            onClick={() => navigate('/login')}
            className="w-full bg-coral-500 text-white font-bold py-3.5 rounded-2xl hover:bg-coral-600 transition shadow-lg shadow-coral-500/25"
          >
            Quay lại trang đăng nhập
          </button>
          <p className="mt-4 text-xs text-gray-400">Tự động chuyển hướng sau vài giây...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen overflow-hidden bg-[radial-gradient(circle_at_top_left,_#fff1ec,_transparent_35%),radial-gradient(circle_at_top_right,_#fff7ed,_transparent_25%),linear-gradient(180deg,_#fffaf7,_#fff)] text-gray-900">
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute left-[-8rem] top-[-6rem] h-72 w-72 rounded-full bg-coral-100/70 blur-3xl" />
        <div className="absolute right-[-5rem] top-24 h-80 w-80 rounded-full bg-amber-100/70 blur-3xl" />
      </div>

      <div className="relative mx-auto flex min-h-screen w-full max-w-2xl items-center px-4 py-10 sm:px-6 lg:px-8">
        <section className="w-full">
          <div className="rounded-[2rem] border border-white/80 bg-white/85 p-6 shadow-[0_20px_80px_rgba(15,23,42,0.12)] backdrop-blur-xl sm:p-8 lg:p-10">
            <div className="mb-8 flex items-center justify-between gap-4">
              <div>
                <div className="mb-3 inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-coral-500 text-white shadow-lg shadow-coral-500/25">
                  <Store className="h-7 w-7" />
                </div>
                <h2 className="text-2xl font-black tracking-tight text-gray-900 sm:text-3xl">
                  Đăng ký đối tác
                </h2>
                <p className="mt-2 text-sm leading-6 text-gray-600">
                  Trở thành chủ quán tại Vĩnh Khánh Food Street.
                </p>
              </div>
            </div>

            {errorMsg && (
              <div className="mb-6 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-medium text-red-700 shadow-sm">
                {errorMsg}
              </div>
            )}

            <form className="space-y-5" onSubmit={handleRegister}>
              <div>
                <label className="mb-2 block text-sm font-semibold text-gray-700">
                  Họ và tên
                </label>
                <div className="relative">
                  <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4 text-coral-500">
                    <User className="h-4 w-4" />
                  </div>
                  <input
                    type="text"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    className="block w-full rounded-2xl border border-gray-200 bg-gray-50 py-3 pl-11 pr-4 text-sm font-medium text-gray-900 outline-none transition focus:border-coral-400 focus:bg-white focus:ring-4 focus:ring-coral-500/10"
                    placeholder="Nguyễn Văn A"
                  />
                </div>
              </div>

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
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="block w-full rounded-2xl border border-gray-200 bg-gray-50 py-3 pl-11 pr-4 text-sm font-medium text-gray-900 outline-none transition focus:border-coral-400 focus:bg-white focus:ring-4 focus:ring-coral-500/10"
                    placeholder="email@example.com"
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
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="block w-full rounded-2xl border border-gray-200 bg-gray-50 py-3 pl-11 pr-4 text-sm font-medium text-gray-900 outline-none transition focus:border-coral-400 focus:bg-white focus:ring-4 focus:ring-coral-500/10"
                    placeholder="••••••••"
                  />
                </div>
                <p className="mt-2 text-xs text-gray-400 italic">
                  * Tài khoản của bạn sẽ ở trạng thái chờ duyệt sau khi đăng ký.
                </p>
              </div>

              <button
                type="submit"
                disabled={isLoading}
                className="group inline-flex w-full items-center justify-center gap-2 rounded-2xl bg-coral-500 px-4 py-3.5 text-sm font-bold text-white shadow-lg shadow-coral-500/25 transition hover:bg-coral-600 focus:outline-none focus:ring-4 focus:ring-coral-500/25 disabled:cursor-not-allowed disabled:bg-gray-400"
              >
                {isLoading ? 'Đang xử lý...' : 'Gửi yêu cầu đăng ký'}
                {!isLoading && <ArrowRight className="h-4 w-4 transition group-hover:translate-x-0.5" />}
              </button>

              <div className="text-center mt-6">
                <span className="text-sm text-gray-500">Đã có tài khoản? </span>
                <Link to="/login" className="text-sm font-bold text-coral-500 hover:text-coral-600 transition">
                  Đăng nhập tại đây
                </Link>
              </div>
            </form>
          </div>
        </section>
      </div>
    </div>
  );
};

export default Register;
