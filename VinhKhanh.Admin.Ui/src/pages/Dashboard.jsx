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
          visitsToday: data.visitsToday ?? 0,
          totalShops: data.totalShopsCount ?? 0,
          pendingOwners: data.pendingOwnersCount ?? 0,
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
        <p className="text-gray-500 mt-1">Hiệu suất và tương tác của hệ thống.</p>
      </div>

      {errorMsg && (
        <div className="rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
          {errorMsg}
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard 
          icon={<MapPin />} 
          title="Tổng số POI" 
          value={stats.pois} 
          trend="Số lượng địa điểm" 
          color="bg-blue-50 text-blue-600" 
        />
        <StatCard 
          icon={<Store />} 
          title="Tổng số Shop" 
          value={stats.totalShops} 
          trend="Đối tác đăng ký" 
          color="bg-purple-50 text-purple-600" 
        />
        <StatCard 
          icon={<Headphones />} 
          title="Lượt nghe" 
          value={stats.audioPlays} 
          trend="Phát thuyết minh" 
          color="bg-emerald-50 text-emerald-600" 
        />
        <StatCard 
          icon={<Clock />} 
          title="Chờ duyệt" 
          value={stats.pendingOwners} 
          trend="Shop cần duyệt" 
          color="bg-amber-50 text-amber-600" 
        />
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
