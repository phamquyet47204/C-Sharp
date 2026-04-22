import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { MapContainer, TileLayer, CircleMarker, Tooltip as MapTooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { MapPin, Headphones, Users, Trophy, Activity, Play, Pause } from 'lucide-react';
import api from '../services/api';
import { startRealtimeAnalytics, stopRealtimeAnalytics } from '../services/realtimeAnalytics';

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
    fetchContentPerf();
    fetchHistory();
  }, [fetchContentPerf, fetchHistory]);

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

  const maxIntensity = heatmapPoints.length ? Math.max(...heatmapPoints.map((p) => p.intensity)) : 1;

  const getRadius = (intensity) => {
    const normalized = intensity / maxIntensity;
    return 6 + normalized * 24;
  };

  const getColor = (intensity) => {
    const normalized = intensity / maxIntensity;
    if (normalized > 0.75) return '#ef4444';
    if (normalized > 0.5) return '#f97316';
    if (normalized > 0.25) return '#eab308';
    return '#22c55e';
  };

  const timelinePercent = useMemo(() => {
    if (!historyDays.length) return 0;
    return Math.round((timelineIndex / Math.max(1, historyDays.length - 1)) * 100);
  }, [timelineIndex, historyDays]);

  return (
    <section className="space-y-6">
      <header>
        <h2 className="text-3xl font-bold text-gray-900">Phân tích</h2>
        <p className="text-sm text-gray-500 mt-2">Heatmap lượt ghé thăm và hiệu suất nội dung theo thời gian.</p>
      </header>

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
              <span className="text-xs font-bold text-emerald-800 uppercase tracking-wider">Online (5p)</span>
              <Activity size={14} className="text-emerald-500" />
            </div>
            <div className="mt-1 flex items-baseline gap-1">
              <span className="text-3xl font-black text-emerald-700">{onlineCount.toLocaleString('vi-VN')}</span>
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
            <h3 className="text-lg font-bold text-gray-800">Heatmap lượt ghé thăm</h3>
          </div>
          {!heatmapLoading && !heatmapError && <span className="text-xs text-gray-400 bg-gray-50 px-3 py-1 rounded-full">{heatmapTotal} điểm dữ liệu</span>}
        </div>

        {heatmapError && <div className="mb-4 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm text-red-700">{heatmapError}</div>}

        {heatmapLoading ? (
          <div className="flex h-80 items-center justify-center rounded-2xl bg-gray-50 text-sm text-gray-400">Đang tải heatmap...</div>
        ) : (
          <>
            <div className="h-80 w-full rounded-2xl overflow-hidden border border-gray-100">
              <MapContainer center={VINH_KHANH_CENTER} zoom={16} style={{ height: '100%', width: '100%' }} scrollWheelZoom={false}>
                <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
                {heatmapPoints.map((point, idx) => (
                  <CircleMarker
                    key={idx}
                    center={[point.lat, point.lng]}
                    radius={getRadius(point.intensity)}
                    pathOptions={{ color: getColor(point.intensity), fillColor: getColor(point.intensity), fillOpacity: 0.5, weight: 1 }}
                  >
                    <MapTooltip>
                      <span className="text-xs">{point.lat.toFixed(5)}, {point.lng.toFixed(5)}<br />Cường độ: {point.intensity}</span>
                    </MapTooltip>
                  </CircleMarker>
                ))}
              </MapContainer>
            </div>

            <div className="mt-3 flex items-center gap-4 text-xs text-gray-500">
              <span className="font-medium">Cường độ:</span>
              {[
                { color: '#22c55e', label: 'Thấp' },
                { color: '#eab308', label: 'Trung bình' },
                { color: '#f97316', label: 'Cao' },
                { color: '#ef4444', label: 'Rất cao' },
              ].map(({ color, label }) => (
                <span key={label} className="flex items-center gap-1">
                  <span className="inline-block h-3 w-3 rounded-full" style={{ backgroundColor: color }} />
                  {label}
                </span>
              ))}
            </div>
          </>
        )}
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
                    <span className="flex items-center justify-end gap-1"><Users size={12} /> Lượt ghé thăm</span>
                  </th>
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
                    <td className="py-3 text-right text-gray-600">{item.totalVisits.toLocaleString('vi-VN')}</td>
                    <td className="py-3 text-right text-gray-600">{item.totalNarrations.toLocaleString('vi-VN')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
};

const RankBadge = ({ rank }) => {
  if (rank === 1) return <span className="text-lg">🥇</span>;
  if (rank === 2) return <span className="text-lg">🥈</span>;
  if (rank === 3) return <span className="text-lg">🥉</span>;
  return <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-gray-100 text-xs font-semibold text-gray-500">{rank}</span>;
};

export default Analytics;
