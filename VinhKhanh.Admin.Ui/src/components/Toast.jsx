import { useState, useCallback } from 'react';

// Hook dùng chung
export function useToast() {
  const [toast, setToast] = useState(null); // { msg, type: 'error'|'success'|'warn' }

  const show = useCallback((msg, type = 'error') => {
    setToast({ msg, type });
    setTimeout(() => setToast(null), 3500);
  }, []);

  return { toast, show };
}

// Component hiển thị
const icons = {
  error:   '✕',
  success: '✓',
  warn:    '⚠',
};

const colors = {
  error:   'bg-red-50 border-red-200 text-red-700',
  success: 'bg-green-50 border-green-200 text-green-700',
  warn:    'bg-yellow-50 border-yellow-200 text-yellow-700',
};

export default function Toast({ toast }) {
  if (!toast) return null;
  return (
    <div className={`fixed top-6 left-1/2 -translate-x-1/2 z-50 flex items-center gap-3 px-5 py-3 rounded-2xl border shadow-lg font-medium text-sm transition-all ${colors[toast.type]}`}>
      <span className="text-base">{icons[toast.type]}</span>
      {toast.msg}
    </div>
  );
}
