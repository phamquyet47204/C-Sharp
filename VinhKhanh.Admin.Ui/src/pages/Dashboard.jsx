import React, { useState, useEffect } from 'react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { MapPin, Users, Headphones, TrendingUp, Store, Clock } from 'lucide-react';
import api from '../services/api';

const Dashboard = () => {
  const [stats, setStats] = useState({
    pois: 0,
    visits: 0,
    audioPlays: 0,
    visitsToday: 0,
    totalShops: 0,
    pendingOwners: 0
  });
  const [activityData, setActivityData] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');

  useEffect(() => {
    const fetchDashboardSummary = async () => {
      try {
        setIsLoading(true);
        setErrorMsg('');

        const response = await api.get('/admin/dashboard-summary');
        const data = response.data || {};

        setStats({
          pois: data.poisCount ?? 0,
          visits: data.visitCount ?? 0,
          audioPlays: data.narrationCount ?? 0,
          audioPlaysToday: data.narrationCountToday ?? 0,
          visitsToday: data.visitsToday ?? 0,
          totalShops: data.totalShopsCount ?? 0,
          pendingOwners: data.pendingOwnersCount ?? 0,
          onlineCount: data.onlineCount ?? 0,
        });

        setActivityData(
          Array.isArray(data.activitySeries)
            ? data.activitySeries.map((item) => ({
              time: item.time,
              khách: item.count,
            }))
            : []
        );
      } catch (error) {
        const message = typeof error?.response?.data === 'string'
          ? error.response.data
          : error?.response?.data?.detail || error?.message;
        setErrorMsg(message || 'Không tải được dữ liệu dashboard.');
      } finally {
        setIsLoading(false);
      }
    };

    fetchDashboardSummary();
  }, []);

  return (
    <div className="space-y-8 pb-10">
      <div>
        <h2 className="text-3xl font-bold text-gray-900">Tổng quan hệ thống</h2>
        <p className="text-gray-500 mt-1">Theo dõi hiệu suất và tương tác của khách du lịch trên phố Vĩnh Khánh.</p>
      </div>

      {errorMsg && (
        <div className="rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-medium text-red-700 flex items-center gap-2">
          <div className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
          {errorMsg}
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-6">
        <StatCard
          icon={<Users size={24} />}
          title="Đang hoạt động"
          value={stats.onlineCount}
          trend="Thiết bị online (5 phút qua)"
          color="bg-rose-50 text-rose-600"
          borderColor="border-rose-100"
        />
        <StatCard
          icon={<TrendingUp size={24} />}
          title="Lượt truy cập"
          value={stats.visitsToday}
          trend={`Tổng: ${stats.visits} lượt`}
          color="bg-indigo-50 text-indigo-600"
          borderColor="border-indigo-100"
        />
        <StatCard
          icon={<Headphones size={24} />}
          title="Lượt nghe TTS"
          value={stats.audioPlaysToday}
          trend={`Tổng: ${stats.audioPlays} lượt`}
          color="bg-emerald-50 text-emerald-600"
          borderColor="border-emerald-100"
        />
        <StatCard
          icon={<MapPin size={24} />}
          title="Điểm POI"
          value={stats.pois}
          trend="Tổng số địa điểm"
          color="bg-blue-50 text-blue-600"
          borderColor="border-blue-100"
        />
        <StatCard
          icon={<Store size={24} />}
          title="Đối tác Shop"
          value={stats.totalShops}
          trend={stats.pendingOwners > 0 ? `${stats.pendingOwners} shop đang chờ duyệt` : 'Đã duyệt toàn bộ'}
          color="bg-orange-50 text-orange-600"
          borderColor="border-orange-100"
        />
      </div>

      {/* Activity Chart Section - Hidden by request */}
      {/*
      <div className="bg-white p-6 md:p-8 rounded-[2rem] shadow-sm border border-gray-100">
        <div className="flex items-center justify-between mb-8">
          <div>
            <h3 className="text-xl font-bold text-gray-900">Biểu đồ tương tác</h3>
            <p className="text-gray-500 text-sm mt-1">Lượng truy cập ghi nhận theo giờ trong 8 giờ qua</p>
          </div>
          <div className="bg-gray-50 text-gray-500 px-4 py-2 rounded-xl text-sm font-semibold border border-gray-100">
            Cập nhật tự động
          </div>
        </div>
        
        {isLoading ? (
          <div className="h-[350px] w-full flex items-center justify-center">
            <div className="flex flex-col items-center gap-3 text-gray-400">
              <div className="w-8 h-8 border-4 border-indigo-500 border-t-transparent rounded-full animate-spin"></div>
              <p className="font-medium">Đang tải dữ liệu biểu đồ...</p>
            </div>
          </div>
        ) : activityData.length > 0 ? (
          <div className="h-[350px] w-full">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={activityData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorKhach" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#6366f1" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#6366f1" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f3f4f6" />
                <XAxis 
                  dataKey="time" 
                  axisLine={false} 
                  tickLine={false} 
                  tick={{ fill: '#9ca3af', fontSize: 12, fontWeight: 500 }}
                  dy={10}
                />
                <YAxis 
                  axisLine={false} 
                  tickLine={false} 
                  tick={{ fill: '#9ca3af', fontSize: 12, fontWeight: 500 }}
                />
                <Tooltip
                  contentStyle={{ 
                    borderRadius: '16px', 
                    border: 'none', 
                    boxShadow: '0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1)',
                    padding: '12px 16px',
                    fontWeight: 'bold'
                  }}
                  itemStyle={{ color: '#6366f1' }}
                  cursor={{ stroke: '#c7d2fe', strokeWidth: 2, strokeDasharray: '4 4' }}
                />
                <Area 
                  type="monotone" 
                  dataKey="khách" 
                  stroke="#6366f1" 
                  strokeWidth={4}
                  fillOpacity={1} 
                  fill="url(#colorKhach)" 
                  activeDot={{ r: 6, fill: '#6366f1', stroke: '#ffffff', strokeWidth: 3 }}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        ) : (
          <div className="h-[350px] w-full flex items-center justify-center border-2 border-dashed border-gray-100 rounded-3xl">
            <p className="text-gray-400 font-medium">Chưa có dữ liệu thống kê trong khoảng thời gian này</p>
          </div>
        )}
      </div>
      */}

    </div>
  );
};

const StatCard = ({ icon, title, value, trend, color, borderColor }) => (
  <div className={`bg-white p-6 rounded-3xl shadow-sm border ${borderColor || 'border-gray-100'} flex items-start gap-4 transition-transform hover:-translate-y-1 duration-300`}>
    <div className={`p-4 rounded-2xl ${color}`}>
      {icon}
    </div>
    <div>
      <p className="text-gray-500 text-sm font-medium">{title}</p>
      <h4 className="text-3xl font-bold text-gray-900 mt-1">{value}</h4>
      <p className="text-xs text-gray-400 mt-2 font-medium">{trend}</p>
    </div>
  </div>
);

export default Dashboard;
