import React, { useEffect } from 'react';
import { AlertTriangle, CheckCircle2, X } from 'lucide-react';

const ConfirmModal = ({ 
  isOpen, 
  onClose, 
  onConfirm, 
  title, 
  message, 
  confirmText = 'Xác nhận', 
  cancelText = 'Hủy',
  type = 'success' // 'success' or 'danger'
}) => {
  // Handle Escape key
  useEffect(() => {
    const handleEsc = (e) => {
      if (e.key === 'Escape') onClose();
    };
    if (isOpen) window.addEventListener('keydown', handleEsc);
    return () => window.removeEventListener('keydown', handleEsc);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const themes = {
    success: {
      icon: <CheckCircle2 size={32} className="text-emerald-500" />,
      bg: 'bg-emerald-50',
      btn: 'bg-coral-500 hover:bg-coral-600 shadow-coral-500/25',
    },
    danger: {
      icon: <AlertTriangle size={32} className="text-red-500" />,
      bg: 'bg-red-50',
      btn: 'bg-red-500 hover:bg-red-600 shadow-red-500/25',
    }
  };

  const theme = themes[type] || themes.success;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-gray-900/40 backdrop-blur-sm transition-opacity" 
        onClick={onClose}
      />
      
      {/* Modal Content */}
      <div className="relative w-full max-w-md bg-white rounded-[2rem] shadow-2xl border border-white/20 transform transition-all animate-in zoom-in-95 duration-200">
        <div className="p-8">
          {/* Close button */}
          <button 
            onClick={onClose}
            className="absolute top-6 right-6 text-gray-400 hover:text-gray-600 transition-colors"
          >
            <X size={20} />
          </button>

          {/* Icon */}
          <div className={`${theme.bg} w-16 h-16 rounded-2xl flex items-center justify-center mb-6`}>
            {theme.icon}
          </div>

          <h3 className="text-xl font-black text-gray-900 mb-2">{title}</h3>
          <p className="text-gray-500 text-sm leading-relaxed mb-8">
            {message}
          </p>

          <div className="flex gap-3">
            <button
              onClick={onClose}
              className="flex-1 px-4 py-3 text-sm font-bold text-gray-600 bg-gray-50 hover:bg-gray-100 rounded-xl transition-colors"
            >
              {cancelText}
            </button>
            <button
              onClick={onConfirm}
              className={`flex-1 px-4 py-3 text-sm font-bold text-white rounded-xl shadow-lg transition-transform active:scale-95 ${theme.btn}`}
            >
              {confirmText}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ConfirmModal;
