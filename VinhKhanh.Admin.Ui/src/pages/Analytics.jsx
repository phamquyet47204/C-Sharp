import React, { useState, useEffect, useCallback } from 'react';
import { MapContainer, TileLayer, CircleMarker, Tooltip as MapTooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { MapPin, Headphones, Users, Trophy } from 'lucide-react';
import api from '../services/api';

// Vinh Khanh center coordinates
const VINH_KHANH_CENTER = [10.7580, 106.7020];

// Compute today and 30 days ago as default date range
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

  const [perfItems, setPerfItems] = useState([]);
  const [perfTotal, setPerfTotal] = useState(0);
  const [perfLoading, setPerfLoading] = useState(false);
  const [perfError, setPerfError] = useState('');

  const fetchHeatmap = useCallback(async () => {
    try {
      setHeatmapLoading(true);
      setHeatmapError('');
      const res = await api.get('/analytics/heatmap', {
        params: { from: `${from}T00:00:00Z`, to: `${to}T23:59:59Z` },
      });
      setHeatmapPoints(res.data?.points ?? []);
      setHeatmapTotal(res.data?.total ?? 0);
    } catch (err) {
      const msg =
        typeof err?.response?.data === 'string'
          ? err.response.data
          : err?.response?.data?.error || err?.message;
      setHeatmapError(msg || 'Không tải được dữ liệu heatmap.');
    } finally {
      setHeatmapLoading(false);
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
    fetchHeatmap();
    fetchContentPerf();
  }, [fetchHeatmap, fetchContentPerf]);

  const handleApply = () => {
    fetchHeatmap();
    fetchContentPerf();
  };

  // Normalize intensity to radius for circle markers
  const maxIntensity = heatmapPoints.length
    ? Math.max(...heatmapPoints.map((p) => p.intensity))
    : 1;

  const getRadius = (intensity) => {
    const normalized = intensity / maxIntensity;
    return 6 + normalized * 24; // 6px to 30px
  };

  const getColor = (intensity) => {
    const normalized = intensity / maxIntensity;
    if (normalized > 0.75) return '#ef4444'; // red
    if (normalized > 0.5) return '#f97316';  // orange
    if (normalized > 0.25) return '#eab308'; // yellow
    return '#22c55e';                         // green
  };

  return (
    <section className="space-y-6">
      <header>
        <h2 className="text-3xl font-bold text-gray-900">Phân tích</h2>
        <p className="text-sm text-gray-500 mt-2">
          Heatmap lượt ghé thăm và hiệu suất nội dung theo thời gian.
        </p>
      </header>

      {/* Date Range Picker */}
      <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm">
        <div className="flex flex-wrap items-end gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-500">Từ ngày</label>
            <input
              type="date"
              value={from}
              max={to}
              onChange={(e) => setFrom(e.target.value)}
              className="rounded-xl border border-gray-200 px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-300"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-500">Đến ngày</label>
            <input
              type="date"
              value={to}
              min={from}
              onChange={(e) => setTo(e.target.value)}
              className="rounded-xl border border-gray-200 px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-300"
            />
          </div>
          <button
            onClick={handleApply}
            className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white hover:bg-blue-700 transition-colors"
          >
            Áp dụng
          </button>
        </div>
      </div>

      {/* Heatmap Section */}
      <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <MapPin size={20} className="text-blue-600" />
            <h3 className="text-lg font-bold text-gray-800">Heatmap lượt ghé thăm</h3>
          </div>
          {!heatmapLoading && !heatmapError && (
            <span className="text-xs text-gray-400 bg-gray-50 px-3 py-1 rounded-full">
              {heatmapTotal} điểm dữ liệu
            </span>
          )}
        </div>

        {heatmapError && (
          <div className="mb-4 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm text-red-700">
            {heatmapError}
          </div>
        )}

        {heatmapLoading ? (
          <div className="flex h-80 items-center justify-center rounded-2xl bg-gray-50 text-sm text-gray-400">
            Đang tải heatmap...
          </div>
        ) : (
          <>
            <div className="h-80 w-full rounded-2xl overflow-hidden border border-gray-100">
              <MapContainer
                center={VINH_KHANH_CENTER}
                zoom={16}
                style={{ height: '100%', width: '100%' }}
                scrollWheelZoom={false}
              >
                <TileLayer
                  attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                  url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
                {heatmapPoints.map((point, idx) => (
                  <CircleMarker
                    key={idx}
                    center={[point.lat, point.lng]}
                    radius={getRadius(point.intensity)}
                    pathOptions={{
                      color: getColor(point.intensity),
                      fillColor: getColor(point.intensity),
                      fillOpacity: 0.5,
                      weight: 1,
                    }}
                  >
                    <MapTooltip>
                      <span className="text-xs">
                        {point.lat.toFixed(5)}, {point.lng.toFixed(5)}<br />
                        Cường độ: {point.intensity}
                      </span>
                    </MapTooltip>
                  </CircleMarker>
                ))}
              </MapContainer>
            </div>

            {/* Legend */}
            <div className="mt-3 flex items-center gap-4 text-xs text-gray-500">
              <span className="font-medium">Cường độ:</span>
              {[
                { color: '#22c55e', label: 'Thấp' },
                { color: '#eab308', label: 'Trung bình' },
                { color: '#f97316', label: 'Cao' },
                { color: '#ef4444', label: 'Rất cao' },
              ].map(({ color, label }) => (
                <span key={label} className="flex items-center gap-1">
                  <span
                    className="inline-block h-3 w-3 rounded-full"
                    style={{ backgroundColor: color }}
                  />
                  {label}
                </span>
              ))}
            </div>

            {heatmapPoints.length === 0 && !heatmapError && (
              <p className="mt-4 text-center text-sm text-gray-400">
                Không có dữ liệu heatmap trong khoảng thời gian này.
              </p>
            )}
          </>
        )}
      </div>

      {/* Content Performance Section */}
      <div className="rounded-3xl border border-gray-100 bg-white p-6 shadow-sm">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <Trophy size={20} className="text-amber-500" />
            <h3 className="text-lg font-bold text-gray-800">Hiệu suất nội dung</h3>
          </div>
          {!perfLoading && !perfError && (
            <span className="text-xs text-gray-400 bg-gray-50 px-3 py-1 rounded-full">
              {perfTotal} POI
            </span>
          )}
        </div>

        {perfError && (
          <div className="mb-4 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm text-red-700">
            {perfError}
          </div>
        )}

        {perfLoading ? (
          <div className="flex h-40 items-center justify-center rounded-2xl bg-gray-50 text-sm text-gray-400">
            Đang tải dữ liệu...
          </div>
        ) : perfItems.length === 0 && !perfError ? (
          <div className="flex h-40 items-center justify-center rounded-2xl border border-dashed border-gray-200 bg-gray-50 text-sm text-gray-400">
            Không có dữ liệu trong khoảng thời gian này.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100">
                  <th className="pb-3 text-left text-xs font-semibold text-gray-400 uppercase tracking-wide w-12">
                    Xếp hạng
                  </th>
                  <th className="pb-3 text-left text-xs font-semibold text-gray-400 uppercase tracking-wide">
                    Tên POI
                  </th>
                  <th className="pb-3 text-right text-xs font-semibold text-gray-400 uppercase tracking-wide">
                    <span className="flex items-center justify-end gap-1">
                      <Users size={12} /> Lượt ghé thăm
                    </span>
                  </th>
                  <th className="pb-3 text-right text-xs font-semibold text-gray-400 uppercase tracking-wide">
                    <span className="flex items-center justify-end gap-1">
                      <Headphones size={12} /> Lượt nghe TTS
                    </span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {perfItems.map((item) => (
                  <tr key={item.poiId} className="hover:bg-gray-50 transition-colors">
                    <td className="py-3 pr-4">
                      <RankBadge rank={item.rank} />
                    </td>
                    <td className="py-3 font-medium text-gray-800">{item.poiName}</td>
                    <td className="py-3 text-right text-gray-600">
                      {item.totalVisits.toLocaleString('vi-VN')}
                    </td>
                    <td className="py-3 text-right text-gray-600">
                      {item.totalNarrations.toLocaleString('vi-VN')}
                    </td>
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
  return (
    <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-gray-100 text-xs font-semibold text-gray-500">
      {rank}
    </span>
  );
};

export default Analytics;
