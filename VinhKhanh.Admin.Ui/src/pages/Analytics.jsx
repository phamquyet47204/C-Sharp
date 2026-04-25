import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { MapContainer, TileLayer, CircleMarker, Tooltip as MapTooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { 
  MapPin, Headphones, Users, Trophy, Activity, Play, Pause, 
  BarChart2 as ChartIcon, TrendingUp, Store as ShopIcon, 
  LayoutDashboard 
} from 'lucide-react';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
  Tooltip as RechartsTooltip, ResponsiveContainer, Cell
} from 'recharts';
import api from '../services/api';
import { startRealtimeAnalytics, stopRealtimeAnalytics } from '../services/realtimeAnalytics';
import HeatmapLayer from '../components/HeatmapLayer';

const VINH_KHANH_CENTER = [10.7580, 106.7020];
const toDateInput = (date) => date.toISOString().slice(0, 10);
const defaultTo = toDateInput(new Date());
const defaultFrom = toDateInput(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000));

const Analytics = () => {
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(defaultTo);

  const [heatmapPoints, setHeatmapPoints] = useState([]);
  const [heatmapTotal, setHeatmapTotal] = useState(0);
  const [heatmapLoading, setHeatmapLoading] = useState(false);
  const [heatmapError, setHeatmapError] = useState('');

  const [onlineCount, setOnlineCount] = useState(0);
  const [selectedDay, setSelectedDay] = useState(defaultTo);
  const [isRealtimeMode, setIsRealtimeMode] = useState(true);

  const [historyDays, setHistoryDays] = useState([]);
  const [timelineIndex, setTimelineIndex] = useState(0);
  const [isTimelinePlaying, setIsTimelinePlaying] = useState(false);

  const [perfItems, setPerfItems] = useState([]);
  const [perfTotal, setPerfTotal] = useState(0);
  const [perfLoading, setPerfLoading] = useState(false);
  const [perfError, setPerfError] = useState('');

  const [stats, setStats] = useState({
    pois: 0,
    visits: 0,
    audioPlays: 0,
    audioPlaysToday: 0,
    visitsToday: 0,
    totalShops: 0,
    pendingOwners: 0,
    onlineCount: 0,
  });
  const [statsLoading, setStatsLoading] = useState(false);

  const timelineTimerRef = useRef(null);

  const applyRealtimePayload = useCallback((payload) => {
    if (!payload) return;
    setHeatmapPoints(payload.points ?? []);
    setHeatmapTotal(payload.total ?? 0);
    setOnlineCount(payload.onlineCount ?? 0);
  }, []);

  const fetchRealtimeSnapshot = useCallback(async () => {
    try {
      setHeatmapLoading(true);
      setHeatmapError('');
      const res = await api.get('/analytics/realtime-overview');
      applyRealtimePayload(res.data);
    } catch (err) {
      const msg =
        typeof err?.response?.data === 'string'
          ? err.response.data
          : err?.response?.data?.error || err?.message;
      setHeatmapError(msg || 'Không tải được dữ liệu realtime.');
    } finally {
      setHeatmapLoading(false);
    }
  }, [applyRealtimePayload]);

  const fetchDailyHeatmap = useCallback(async (day) => {
    try {
      setHeatmapLoading(true);
      setHeatmapError('');
      const res = await api.get('/analytics/heatmap/daily', { params: { date: day } });
      setHeatmapPoints(res.data?.points ?? []);
      setHeatmapTotal(res.data?.total ?? 0);
    } catch (err) {
      const msg =
        typeof err?.response?.data === 'string'
          ? err.response.data
          : err?.response?.data?.error || err?.message;
      setHeatmapError(msg || 'Không tải được dữ liệu heatmap theo ngày.');
    } finally {
      setHeatmapLoading(false);
    }
  }, []);

  const fetchHistory = useCallback(async () => {
    try {
      const res = await api.get('/analytics/heatmap/history', { params: { from, to } });
      const days = res.data?.days ?? [];
      setHistoryDays(days);
      if (days.length > 0) {
        setTimelineIndex(0);
      }
    } catch {
      setHistoryDays([]);
    }
  }, [from, to]);

  const fetchSummary = useCallback(async () => {
    try {
      setStatsLoading(true);
      const res = await api.get('/admin/dashboard-summary');
      const data = res.data;
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
    } catch (err) {
      console.error('Lỗi khi tải dashboard summary:', err);
    } finally {
      setStatsLoading(false);
    }
  }, []);

  const fetchContentPerf = useCallback(async () => {
    try {
      setPerfLoading(true);
      setPerfError('');
      const res = await api.get('/analytics/content-performance', {
        params: { from: `${from}T00:00:00Z`, to: `${to}T23:59:59Z`, limit: 20 },
      });
      setPerfItems(res.data?.items ?? []);
      setPerfTotal(res.data?.total ?? 0);
    } catch (err) {
      const msg =
        typeof err?.response?.data === 'string'
          ? err.response.data
          : err?.response?.data?.error || err?.message;
      setPerfError(msg || 'Không tải được dữ liệu content performance.');
    } finally {
      setPerfLoading(false);
    }
  }, [from, to]);

  useEffect(() => {
    fetchSummary();
    fetchContentPerf();
    fetchHistory();
  }, [fetchSummary, fetchContentPerf, fetchHistory]);

  useEffect(() => {
    if (!isRealtimeMode) return undefined;

    let active = true;
    fetchRealtimeSnapshot();

    startRealtimeAnalytics((payload) => {
      if (!active) return;
      applyRealtimePayload(payload);
    }).catch(() => {
      // ignore here, snapshot already handles error states
    });

    return () => {
      active = false;
      stopRealtimeAnalytics();
    };
  }, [isRealtimeMode, fetchRealtimeSnapshot, applyRealtimePayload]);

  useEffect(() => {
    if (isRealtimeMode) return;
    fetchDailyHeatmap(selectedDay);
  }, [isRealtimeMode, selectedDay, fetchDailyHeatmap]);

  useEffect(() => {
    if (!isTimelinePlaying || historyDays.length === 0) {
      if (timelineTimerRef.current) clearInterval(timelineTimerRef.current);
      return;
    }

    timelineTimerRef.current = setInterval(() => {
      setTimelineIndex((prev) => {
        const next = prev + 1;
        if (next >= historyDays.length) {
          return 0;
        }
        return next;
      });
    }, 1500);

    return () => {
      if (timelineTimerRef.current) clearInterval(timelineTimerRef.current);
    };
  }, [isTimelinePlaying, historyDays]);

  const timelineDay = historyDays[timelineIndex];
  useEffect(() => {
    if (!timelineDay || isRealtimeMode) return;
    setHeatmapPoints(timelineDay.points ?? []);
    setHeatmapTotal((timelineDay.points ?? []).length);
    setSelectedDay(timelineDay.day);
  }, [timelineDay, isRealtimeMode]);

  const handleApply = () => {
    fetchContentPerf();
    fetchHistory();
    if (!isRealtimeMode) {
      fetchDailyHeatmap(selectedDay);
    }
  };

  const getRadius = (intensity) => {
    return Math.min(30, 6 + (intensity || 0) * 1.5);
  };

  const getColor = (intensity) => {
    const val = intensity || 0;
    if (val >= 11) return '#ef4444'; // Rất đông: >= 11 người
    if (val >= 6) return '#f97316'; // Đông: 6 - 10 người
    if (val >= 3) return '#eab308'; // Vừa: 3 - 5 người
    return '#22c55e';               // Bình thường: 1 - 2 người
  };

  const getStatusText = (count) => {
    if (count >= 11) return 'Rất đông';
    if (count >= 6) return 'Đông đúc';
    if (count >= 3) return 'Vừa phải';
    return 'Thưa thớt';
  };

  const timelinePercent = useMemo(() => {
    if (!historyDays.length) return 0;
    return Math.round((timelineIndex / Math.max(1, historyDays.length - 1)) * 100);
  }, [timelineIndex, historyDays]);

  const totalPeople = useMemo(() => {
    return heatmapPoints.reduce((acc, p) => acc + (p.peopleCount || 0), 0);
  }, [heatmapPoints]);

  const hotspots = useMemo(() => {
    const groups = {};
    heatmapPoints.forEach(p => {
      const key = p.poiName || `Khu vực [${p.lat.toFixed(3)}, ${p.lng.toFixed(3)}]`;
      if (!groups[key]) {
        groups[key] = {
          name: key,
          value: 0,
          density: 0,
          lat: p.lat,
          lng: p.lng
        };
      }
      groups[key].value += (p.peopleCount || 0);
      groups[key].density = Math.max(groups[key].density, p.density || 0);
    });

    return Object.values(groups)
      .sort((a, b) => b.value - a.value)
      .slice(0, 5);
  }, [heatmapPoints]);

  return (
    <section className="space-y-8">
      <header className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h2 className="text-3xl font-bold text-gray-900 tracking-tight">Phân tích hệ thống</h2>
          <p className="text-gray-500 mt-1">Dữ liệu tổng quan và chi tiết mật độ hoạt động thời gian thực.</p>
        </div>
        <div className="flex items-center gap-2 px-4 py-2 bg-indigo-50 text-indigo-700 rounded-2xl text-sm font-bold border border-indigo-100">
          <div className="w-2 h-2 rounded-full bg-indigo-500 animate-pulse" />
          {stats.onlineCount} người đang hoạt động
        </div>
      </header>

      {/* Stats Grid - Moved from Dashboard */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        {/* Ẩn theo yêu cầu của USER
        <StatCard
          icon={<TrendingUp size={24} />}
          title="Lượt truy cập"
          value={stats.visitsToday}
          trend={`Tổng: ${stats.visits} lượt`}
          color="bg-indigo-50 text-indigo-600"
          borderColor="border-indigo-100"
        />
        */}
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
          title="Địa điểm POI"
          value={stats.pois}
          trend="Đã được khởi tạo"
          color="bg-blue-50 text-blue-600"
          borderColor="border-blue-100"
        />
        <StatCard
          icon={<ShopIcon size={24} />}
          title="Đối tác Shop"
          value={stats.totalShops}
          trend={stats.pendingOwners > 0 ? `${stats.pendingOwners} shop đang chờ duyệt` : 'Đã duyệt toàn bộ'}
          color="bg-orange-50 text-orange-600"
          borderColor="border-orange-100"
        />
      </div>

      <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-blue-50 rounded-2xl text-blue-600">
              <Activity size={24} />
            </div>
            <div>
              <h3 className="text-xl font-bold text-gray-800">Theo dõi lưu lượng</h3>
              <p className="text-xs text-gray-500">Phân tích mật độ người dùng và hiệu suất nội dung.</p>
            </div>
          </div>

          <div className="flex bg-gray-100 p-1.5 rounded-2xl">
            <button
              onClick={() => setIsRealtimeMode(true)}
              className={`flex items-center gap-2 px-5 py-2 rounded-xl text-sm font-bold transition-all ${isRealtimeMode ? 'bg-white text-emerald-600 shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}
            >
              <div className={`h-2 w-2 rounded-full ${isRealtimeMode ? 'bg-emerald-500 animate-pulse' : 'bg-gray-400'}`} />
              Realtime
            </button>
            <button
              onClick={() => setIsRealtimeMode(false)}
              className={`flex items-center gap-2 px-5 py-2 rounded-xl text-sm font-bold transition-all ${!isRealtimeMode ? 'bg-white text-blue-600 shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}
            >
              <MapPin size={16} />
              Lịch sử
            </button>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
          <div className="lg:col-span-1 rounded-2xl border border-emerald-100 bg-gradient-to-br from-emerald-50 to-white px-5 py-4 flex flex-col justify-center">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-emerald-800 uppercase tracking-wider">
                {isRealtimeMode ? 'Số người online' : 'Tổng lượng khách'}
              </span>
              <Activity size={14} className="text-emerald-500" />
            </div>
            <div className="mt-1 flex items-baseline gap-1">
              <span className="text-3xl font-black text-emerald-700">
                {(isRealtimeMode ? onlineCount : totalPeople).toLocaleString('vi-VN')}
              </span>
              <span className="text-xs text-emerald-600/70">người</span>
            </div>
          </div>

          <div className="lg:col-span-3 rounded-2xl border border-gray-100 bg-gray-50/50 p-4 flex flex-wrap items-end gap-3 transition-opacity">
            {!isRealtimeMode ? (
              <>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-bold text-gray-400 uppercase ml-1">Khoảng ngày</label>
                  <div className="flex items-center gap-2 bg-white p-1 rounded-xl border border-gray-200 shadow-sm">
                    <input
                      type="date"
                      value={from}
                      max={to}
                      onChange={(e) => setFrom(e.target.value)}
                      className="bg-transparent border-none focus:ring-0 text-sm py-1.5 px-2 text-gray-700 font-medium"
                    />
                    <div className="h-4 w-px bg-gray-200" />
                    <input
                      type="date"
                      value={to}
                      min={from}
                      max={defaultTo}
                      onChange={(e) => setTo(e.target.value)}
                      className="bg-transparent border-none focus:ring-0 text-sm py-1.5 px-2 text-gray-700 font-medium"
                    />
                  </div>
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-bold text-gray-400 uppercase ml-1">Xem ngày cụ thể</label>
                  <input
                    type="date"
                    value={selectedDay}
                    max={defaultTo}
                    onChange={(e) => setSelectedDay(e.target.value)}
                    className="rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm text-gray-800 font-medium shadow-sm focus:border-blue-300 focus:ring-4 focus:ring-blue-100 transition-all outline-none"
                  />
                </div>

                <div className="flex gap-1 mb-0.5">
                  {[
                    { label: 'Hôm nay', days: 0 },
                    { label: '7 ngày', days: 7 },
                    { label: '30 ngày', days: 30 }
                  ].map(p => (
                    <button
                      key={p.label}
                      onClick={() => {
                        const newTo = defaultTo;
                        const newFrom = toDateInput(new Date(Date.now() - p.days * 24 * 60 * 60 * 1000));
                        setFrom(newFrom);
                        setTo(newTo);
                        setSelectedDay(newTo);
                      }}
                      className="px-3 py-2 rounded-xl text-[10px] font-bold uppercase tracking-tight text-gray-500 bg-white border border-gray-200 hover:border-blue-400 hover:text-blue-600 transition-all shadow-sm"
                    >
                      {p.label}
                    </button>
                  ))}
                </div>
              </>
            ) : (
              <div className="flex-1 flex items-center justify-center py-2">
                <div className="text-sm text-gray-400 font-medium italic flex items-center gap-2">
                  <Activity size={14} className="animate-pulse" />
                  Hệ thống đang tự động cập nhật dữ liệu thời gian thực...
                </div>
              </div>
            )}

            <button
              onClick={handleApply}
              className="ml-auto flex items-center gap-2 rounded-xl bg-blue-600 px-6 py-2.5 text-sm font-bold text-white hover:bg-blue-700 hover:shadow-lg active:scale-95 transition-all"
            >
              <Users size={16} />
              Áp dụng
            </button>
          </div>
        </div>

        {!isRealtimeMode && (
          <div className="rounded-2xl border border-blue-100 bg-gradient-to-r from-blue-50 to-white p-4 space-y-4">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <div className="p-1.5 bg-blue-600 rounded-lg text-white">
                  <Play size={14} />
                </div>
                <div>
                  <span className="text-sm font-bold text-blue-900 block">Timeline lịch sử</span>
                  <span className="text-[10px] text-blue-600 font-medium uppercase tracking-wider">Phát lại diễn biến lưu lượng</span>
                </div>
              </div>
              <button
                onClick={() => setIsTimelinePlaying((v) => !v)}
                disabled={historyDays.length === 0}
                className={`inline-flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-bold transition-all shadow-sm ${isTimelinePlaying ? 'bg-amber-100 text-amber-700 border border-amber-200' : 'bg-blue-600 text-white hover:bg-blue-700'}`}
              >
                {isTimelinePlaying ? <Pause size={16} fill="currentColor" /> : <Play size={16} fill="currentColor" />}
                {isTimelinePlaying ? 'Tạm dừng' : 'Phát lại'}
              </button>
            </div>

            <div className="px-2">
              <input
                type="range"
                min={0}
                max={Math.max(0, historyDays.length - 1)}
                value={timelineIndex}
                onChange={(e) => setTimelineIndex(Number(e.target.value))}
                className="w-full accent-blue-600 h-2 bg-blue-200 rounded-lg appearance-none cursor-pointer"
                disabled={historyDays.length === 0}
              />
            </div>

            <div className="flex items-center justify-between text-[11px] font-bold text-blue-800 px-1">
              <span className="bg-white px-2 py-1 rounded-lg border border-blue-100 shadow-sm">{historyDays[0]?.day || '—'}</span>
              <div className="flex flex-col items-center">
                <span className="text-blue-900">{timelineDay?.day || '—'}</span>
                <span className="text-[9px] text-blue-400 font-black uppercase">{timelinePercent}% COMPLETED</span>
              </div>
              <span className="bg-white px-2 py-1 rounded-lg border border-blue-100 shadow-sm">{historyDays[historyDays.length - 1]?.day || '—'}</span>
            </div>
          </div>
        )}
      </div>

      <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <MapPin size={20} className="text-blue-600" />
            <h3 className="text-lg font-bold text-gray-800">Heatmap</h3>
          </div>
          {!heatmapLoading && !heatmapError && <span className="text-xs text-gray-400 bg-gray-50 px-3 py-1 rounded-full">{heatmapTotal} điểm dữ liệu</span>}
        </div>

        {heatmapError && <div className="mb-4 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm text-red-700">{heatmapError}</div>}

        <div className="relative h-80 w-full rounded-2xl overflow-hidden border border-gray-100 bg-gray-50">
          <MapContainer center={VINH_KHANH_CENTER} zoom={16} style={{ height: '100%', width: '100%' }} scrollWheelZoom={false}>
            <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />

            <HeatmapLayer
              points={heatmapPoints}
              maxDensity={10.0}
              radius={25}
              blur={15}
            />

            {heatmapPoints.map((point, idx) => (
              <CircleMarker
                key={idx}
                center={[point.lat, point.lng]}
                radius={4}
                pathOptions={{
                  color: 'white',
                  fillColor: getColor(point.intensity),
                  fillOpacity: 0.6,
                  weight: 1
                }}
              >
                <MapTooltip permanent={false} direction="top" offset={[0, -5]}>
                  <div className="p-1 min-w-[120px]">
                    <div className="flex justify-between items-center mb-1">
                      <span className="text-xs font-bold text-gray-600">Thực tế:</span>
                      <span className="text-sm font-black text-blue-600">{point.peopleCount} người</span>
                    </div>
                    <div className="flex justify-between items-center">
                      <span className="text-[10px] text-gray-400">Trạng thái:</span>
                      <span className="text-[10px] font-bold" style={{ color: getColor(point.peopleCount) }}>
                        {getStatusText(point.peopleCount)}
                      </span>
                    </div>
                  </div>
                </MapTooltip>
              </CircleMarker>
            ))}
          </MapContainer>

          {/* Loading Overlay */}
          {heatmapLoading && (
            <div className="absolute inset-0 z-[1000] flex items-center justify-center bg-white/40 backdrop-blur-[1px]">
              <div className="flex items-center gap-2 bg-white px-4 py-2 rounded-full shadow-lg border border-blue-100">
                <Activity size={16} className="text-blue-600 animate-pulse" />
                <span className="text-xs font-bold text-blue-900">Đang cập nhật...</span>
              </div>
            </div>
          )}
        </div>

        <div className="mt-3 flex items-center gap-4 text-xs text-gray-500">
          <span className="font-bold text-gray-700 uppercase tracking-wider">Mật độ người (Ước tính):</span>
          {[
            { color: '#22c55e', label: '1 - 2 người' },
            { color: '#eab308', label: '3 - 5 người' },
            { color: '#f97316', label: '6 - 10 người' },
            { color: '#ef4444', label: '≥ 11 người' },
          ].map(({ color, label }) => (
            <span key={label} className="flex items-center gap-1.5 bg-gray-50 px-2 py-1 rounded-lg border border-gray-100">
              <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ backgroundColor: color }} />
              {label}
            </span>
          ))}
        </div>
      </div>

      {/* Hotspots Analysis Section */ }
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm">
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center gap-2">
              <div className="p-2 bg-indigo-50 rounded-xl text-indigo-600">
                <ChartIcon size={20} />
              </div>
              <h3 className="text-lg font-bold text-gray-800">Top khu vực đông đúc</h3>
            </div>
            {!heatmapLoading && hotspots.length > 0 && (
              <span className="text-[10px] font-bold text-gray-400 bg-gray-50 px-2 py-1 rounded-lg border border-gray-100 uppercase">Dựa trên {heatmapTotal} điểm</span>
            )}
          </div>
          <div className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={hotspots} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fontSize: 10, fontWeight: 700, fill: '#64748b' }} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 10, fontWeight: 700, fill: '#64748b' }} />
                <RechartsTooltip 
                  cursor={{ fill: '#f8fafc' }}
                  contentStyle={{ borderRadius: '16px', border: 'none', boxShadow: '0 20px 25px -5px rgb(0 0 0 / 0.1)', padding: '12px' }}
                  itemStyle={{ fontSize: '12px', fontWeight: 'bold' }}
                />
                <Bar dataKey="value" radius={[8, 8, 0, 0]} barSize={42} name="Số người">
                  {hotspots.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={['#6366f1', '#818cf8', '#a5b4fc', '#c7d2fe', '#e0e7ff'][index % 5]} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-2">
              <div className="p-2 bg-emerald-50 rounded-xl text-emerald-600">
                <MapPin size={20} />
              </div>
              <h3 className="text-lg font-bold text-gray-800">Chi tiết điểm nóng</h3>
            </div>
          </div>
          <div className="space-y-3 max-h-64 overflow-y-auto pr-1 custom-scrollbar">
            {hotspots.length === 0 ? (
              <div className="flex h-48 items-center justify-center text-sm text-gray-400 italic bg-gray-50 rounded-2xl border border-dashed border-gray-200">
                Không có dữ liệu hotspot tại thời điểm này
              </div>
            ) : hotspots.map((spot, idx) => (
              <div key={idx} className="flex items-center justify-between p-3.5 rounded-2xl bg-gray-50 border border-gray-100 hover:border-emerald-200 hover:bg-white transition-all duration-300 group">
                <div className="flex items-center gap-3">
                  <div className="h-9 w-9 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center font-black text-xs group-hover:scale-110 transition-transform">
                    #{idx + 1}
                  </div>
                  <div>
                    <div className="text-sm font-bold text-gray-800">{spot.name}</div>
                    <div className="text-[10px] text-gray-400 font-bold tracking-tight">Vị trí: {spot.lat.toFixed(4)}, {spot.lng.toFixed(4)}</div>
                  </div>
                </div>
                <div className="text-right">
                  <div className="text-base font-black text-emerald-600 leading-none">{spot.value} người</div>
                  <div className="text-[10px] font-bold uppercase mt-1" style={{ color: getColor(spot.value) }}>
                    {getStatusText(spot.value)}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <Trophy size={20} className="text-amber-500" />
            <h3 className="text-lg font-bold text-gray-800">Hiệu suất nội dung</h3>
          </div>
          {!perfLoading && !perfError && <span className="text-xs text-gray-400 bg-gray-50 px-3 py-1 rounded-full">{perfTotal} POI</span>}
        </div>

        {perfError && <div className="mb-4 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm text-red-700">{perfError}</div>}

        {perfLoading ? (
          <div className="flex h-40 items-center justify-center rounded-2xl bg-gray-50 text-sm text-gray-400">Đang tải dữ liệu...</div>
        ) : perfItems.length === 0 && !perfError ? (
          <div className="flex h-40 items-center justify-center rounded-2xl border border-dashed border-gray-200 bg-gray-50 text-sm text-gray-400">Không có dữ liệu trong khoảng thời gian này.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100">
                  <th className="pb-3 text-left text-xs font-semibold text-gray-400 uppercase tracking-wide w-12">Xếp hạng</th>
                  <th className="pb-3 text-left text-xs font-semibold text-gray-400 uppercase tracking-wide">Tên POI</th>

                  <th className="pb-3 text-right text-xs font-semibold text-gray-400 uppercase tracking-wide">
                    <span className="flex items-center justify-end gap-1"><Headphones size={12} /> Lượt nghe TTS</span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {perfItems.map((item) => (
                  <tr key={item.poiId} className="hover:bg-gray-50 transition-colors">
                    <td className="py-3 pr-4"><RankBadge rank={item.rank} /></td>
                    <td className="py-3 font-medium text-gray-800">{item.poiName}</td>

                    <td className="py-3 text-right text-gray-600">{item.totalNarrations.toLocaleString('vi-VN')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section >
  );
};

const StatCard = ({ icon, title, value, trend, color, borderColor }) => (
  <div className={`bg-white p-6 rounded-[2rem] shadow-sm border ${borderColor || 'border-gray-100'} flex items-start gap-4 transition-all duration-300 hover:shadow-md hover:-translate-y-1`}>
    <div className={`p-4 rounded-2xl ${color}`}>
      {icon}
    </div>
    <div>
      <p className="text-sm font-semibold text-gray-500 mb-1">{title}</p>
      <h3 className="text-3xl font-black text-gray-900 leading-none">{value.toLocaleString('vi-VN')}</h3>
      <p className="text-xs font-bold text-gray-400 mt-3 flex items-center gap-1 uppercase tracking-wider">
        {trend}
      </p>
    </div>
  </div>
);

const RankBadge = ({ rank }) => {
  if (rank === 1) return <span className="text-lg">🥇</span>;
  if (rank === 2) return <span className="text-lg">🥈</span>;
  if (rank === 3) return <span className="text-lg">🥉</span>;
  return <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-gray-100 text-xs font-semibold text-gray-500">{rank}</span>;
};

export default Analytics;
