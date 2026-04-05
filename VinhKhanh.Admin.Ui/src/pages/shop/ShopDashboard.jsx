import { useState, useEffect } from 'react';
import api from '../../services/api';

const statusColors = {
  Draft: 'bg-gray-100 text-gray-600',
  Pending_Approval: 'bg-yellow-100 text-yellow-700',
  Approved: 'bg-green-100 text-green-700',
  Rejected: 'bg-red-100 text-red-700',
  Hidden: 'bg-purple-100 text-purple-700',
};

const statusLabels = {
  Draft: 'Nháp',
  Pending_Approval: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Bị từ chối',
  Hidden: 'Đã ẩn',
};

export default function ShopDashboard() {
  const [analytics, setAnalytics] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get('/shop/analytics')
      .then(res => setAnalytics(res.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="p-6 text-gray-500">Đang tải...</div>;

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-6">Tổng quan quán</h1>

      {/* Stats */}
      <div className="grid grid-cols-2 gap-4 mb-6">
        <div className="bg-white rounded-xl p-5 shadow-sm">
          <p className="text-sm text-gray-500">Lượt ghé thăm (30 ngày)</p>
          <p className="text-3xl font-bold text-orange-500 mt-1">{analytics?.totalVisits ?? 0}</p>
        </div>
        <div className="bg-white rounded-xl p-5 shadow-sm">
          <p className="text-sm text-gray-500">Lượt nghe TTS (30 ngày)</p>
          <p className="text-3xl font-bold text-blue-500 mt-1">{analytics?.totalNarrations ?? 0}</p>
        </div>
      </div>

      {/* POI list */}
      <h2 className="text-lg font-semibold mb-3">Danh sách POI</h2>
      <div className="space-y-3">
        {(analytics?.pois ?? []).map(poi => (
          <div key={poi.poiId} className="bg-white rounded-xl p-4 shadow-sm flex items-center justify-between">
            <div>
              <p className="font-medium">{poi.poiName}</p>
              <p className="text-sm text-gray-500">{poi.visits} lượt ghé · {poi.narrations} lượt nghe</p>
            </div>
          </div>
        ))}
        {(analytics?.pois ?? []).length === 0 && (
          <p className="text-gray-400 text-center py-4">Chưa có POI nào</p>
        )}
      </div>
    </div>
  );
}
