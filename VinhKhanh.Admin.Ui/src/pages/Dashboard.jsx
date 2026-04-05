import React, { useState, useEffect } from 'react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { MapPin, Users, Headphones, TrendingUp } from 'lucide-react';
import api from '../services/api';

const Dashboard = () => {
  const [stats, setStats] = useState({ pois: 0, visits: 0, audioPlays: 0, visitsToday: 0 });
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
          visitsToday: data.visitsToday ?? 0,
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
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-gray-900">Tổng quan</h2>
        <p className="text-gray-500 mt-1">Hiệu suất và tương tác của du khách hôm nay.</p>
      </div>

      {errorMsg && (
        <div className="rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
          {errorMsg}
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <StatCard 
          icon={<MapPin />} 
          title="Tổng số POI" 
          value={stats.pois} 
          trend="Dữ liệu trực tiếp từ database" 
          color="bg-blue-50 text-blue-600" 
        />
        <StatCard 
          icon={<Users />} 
          title="Lượt khách ghé thăm" 
          value={stats.visits} 
          trend={`Hôm nay: ${stats.visitsToday}`} 
          color="bg-coral-50 text-coral-600" 
        />
        <StatCard 
          icon={<Headphones />} 
          title="Lượt nghe thuyết minh" 
          value={stats.audioPlays} 
          trend="Tổng lượt nghe TTS toàn hệ thống" 
          color="bg-emerald-50 text-emerald-600" 
        />
      </div>

      {/* Charts */}
      <div className="bg-white p-6 rounded-3xl shadow-sm border border-gray-100">
        <div className="flex items-center justify-between mb-6">
          <h3 className="text-xl font-bold text-gray-800">Biểu đồ mật độ khách tham quan</h3>
          <span className="flex items-center gap-1 text-sm text-green-600 bg-green-50 px-3 py-1 rounded-full font-medium">
            <TrendingUp size={16} /> {isLoading ? 'Đang tải' : 'Live'}
          </span>
        </div>
        <div className="h-[300px] w-full">
          {activityData.length > 0 ? (
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={activityData} margin={{ top: 10, right: 30, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorKhach" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#FF7F50" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#FF7F50" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f3f4f6" />
                <XAxis dataKey="time" axisLine={false} tickLine={false} tick={{fill: '#9ca3af'}} />
                <YAxis axisLine={false} tickLine={false} tick={{fill: '#9ca3af'}} />
                <Tooltip 
                  contentStyle={{ borderRadius: '16px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)' }}
                />
                <Area 
                  type="monotone" 
                  dataKey="khách" 
                  stroke="#FF7F50" 
                  strokeWidth={3}
                  fillOpacity={1} 
                  fill="url(#colorKhach)" 
                />
              </AreaChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex h-full items-center justify-center rounded-3xl border border-dashed border-gray-200 bg-gray-50 text-sm text-gray-500">
              {isLoading ? 'Đang tải dữ liệu...' : 'Chưa có dữ liệu ghé thăm để hiển thị biểu đồ.'}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

const StatCard = ({ icon, title, value, trend, color }) => (
  <div className="bg-white p-6 rounded-3xl shadow-sm border border-gray-100 flex items-start gap-4 transition-transform hover:-translate-y-1 duration-300">
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
