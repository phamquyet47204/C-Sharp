import { useState, useCallback } from 'react';
import { AlertTriangle } from 'lucide-react';

/**
 * Hook để trigger confirm dialog từ bất kỳ component nào.
 * Usage:
 *   const { confirmProps, confirm } = useConfirm();
 *   await confirm({ title: '...', message: '...' }) → true/false
 *   <ConfirmDialog {...confirmProps} />
 */
export function useConfirm() {
  const [state, setState] = useState(null); // { title, message, resolve }

  const confirm = useCallback(({ title, message }) => {
    return new Promise((resolve) => {
      setState({ title, message, resolve });
    });
  }, []);

  const handleConfirm = () => {
    state?.resolve(true);
    setState(null);
  };

  const handleCancel = () => {
    state?.resolve(false);
    setState(null);
  };

  return {
    confirm,
    confirmProps: { open: !!state, title: state?.title, message: state?.message, onConfirm: handleConfirm, onCancel: handleCancel },
  };
}

/**
 * Modal confirm dialog thay thế window.confirm.
 */
export default function ConfirmDialog({ open, title, message, onConfirm, onCancel, confirmLabel = 'Xóa', confirmClass = 'bg-red-500 hover:bg-red-600 text-white' }) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={onCancel} />

      {/* Dialog */}
      <div className="relative bg-white rounded-3xl shadow-2xl p-6 w-full max-w-sm mx-4 animate-in fade-in zoom-in-95 duration-150">
        {/* Icon */}
        <div className="flex items-center justify-center w-12 h-12 rounded-2xl bg-red-50 mx-auto mb-4">
          <AlertTriangle size={24} className="text-red-500" />
        </div>

        {/* Content */}
        <h3 className="text-lg font-bold text-gray-900 text-center mb-2">{title}</h3>
        <p className="text-sm text-gray-500 text-center leading-relaxed">{message}</p>

        {/* Actions */}
        <div className="flex gap-3 mt-6">
          <button
            onClick={onCancel}
            className="flex-1 rounded-2xl border border-gray-200 px-4 py-2.5 text-sm font-semibold text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Hủy
          </button>
          <button
            onClick={onConfirm}
            className={`flex-1 rounded-2xl px-4 py-2.5 text-sm font-semibold transition-colors ${confirmClass}`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
