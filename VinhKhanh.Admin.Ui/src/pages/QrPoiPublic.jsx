import React, { useEffect, useMemo, useState, useCallback } from 'react';
import { useParams } from 'react-router-dom';
import { MapContainer, TileLayer, Marker, Circle } from 'react-leaflet';
import { 
  ArrowRight, Globe, MapPin, PlayCircle, Smartphone, 
  Volume2, Download, Navigation, Languages, Info,
  Compass, ExternalLink, Headphones
} from 'lucide-react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import api from '../services/api';

// Fix Leaflet icons
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: '/marker-icon-2x.png',
  iconUrl: '/marker-icon.png',
  shadowUrl: '/marker-shadow.png',
});

const normalizeLanguage = (code) => {
  if (!code) return 'vi';
  const short = code.trim().toLowerCase().replace('_', '-').split('-')[0];
  return short === 'jp' ? 'ja' : short;
};

const getLanguageLabel = (code) => {
  switch (normalizeLanguage(code)) {
    case 'en': return 'English';
    case 'ja': return '日本語';
    default: return 'Tiếng Việt';
  }
};

const resolveImageUrl = (path) => {
  if (!path) return 'https://images.unsplash.com/photo-1549488344-1f9b00969552?q=80&w=2070&auto=format&fit=crop';
  if (path.startsWith('http')) return path;
  
  // Clean potential VITE_API_BASE_URL to get domain
  const apiBase = (import.meta.env.VITE_API_BASE_URL || '').replace(/\/api\/?$/i, '').replace(/\/$/, '');
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  
  return `${apiBase}${cleanPath}`;
};

const QrPoiPublic = () => {
  const { token } = useParams();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [payload, setPayload] = useState(null);
  const [lang, setLang] = useState('vi');
  const [speaking, setSpeaking] = useState(false);
  const [interacted, setInteracted] = useState(false);
  const [deviceType, setDeviceType] = useState('other');

  // Detect device for tailored experience
  useEffect(() => {
    const ua = navigator.userAgent.toLowerCase();
    if (ua.includes('android')) setDeviceType('android');
    else if (/iphone|ipad|ipod/.test(ua)) setDeviceType('ios');
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        setError('');
        const res = await api.get(`/qr/${encodeURIComponent(token)}`);
        const data = res.data;
        setPayload(data);

        // Advanced Language Detection: Check all browser languages
        const browserLangs = navigator.languages ? navigator.languages.map(normalizeLanguage) : [normalizeLanguage(navigator.language)];
        const available = (data?.localizations ?? []).map((x) => normalizeLanguage(x.languageCode));
        
        let bestLang = 'vi';
        for (const preferred of browserLangs) {
          if (available.includes(preferred)) {
            bestLang = preferred;
            break;
          }
        }
        setLang(bestLang);
      } catch (err) {
        const msg = typeof err?.response?.data === 'string' ? err.response.data : (err?.response?.data?.error || err?.message);
        setError(msg || 'Lỗi kết nối máy chủ.');
      } finally {
        setLoading(false);
      }
    };
    if (token) fetchData();
  }, [token]);

  const selected = useMemo(() => {
    if (!payload?.localizations?.length) return null;
    return payload.localizations.find((x) => normalizeLanguage(x.languageCode) === lang) ?? payload.localizations[0];
  }, [payload, lang]);

  const speak = useCallback((text) => {
    if (!text || !window.speechSynthesis) return;
    window.speechSynthesis.cancel();
    
    const utterance = new SpeechSynthesisUtterance(text);
    // Select voice accurately
    utterance.lang = lang === 'ja' ? 'ja-JP' : lang === 'en' ? 'en-US' : 'vi-VN';
    utterance.rate = 0.95;
    utterance.pitch = 1.0;
    
    utterance.onstart = () => setSpeaking(true);
    utterance.onend = () => setSpeaking(false);
    utterance.onerror = () => setSpeaking(false);
    
    window.speechSynthesis.speak(utterance);
  }, [lang]);

  // Handle Autoplay issues: Wait for first interaction
  const handleStartExperience = () => {
    setInteracted(true);
    if (selected?.description) {
      speak(selected.description);
    }
  };

  useEffect(() => {
    if (interacted && selected?.description) {
      speak(selected.description);
    }
  }, [selected, interacted, speak]);

  const openInApp = () => {
    if (!payload?.deepLink) return;
    const fallback = deviceType === 'ios' ? payload?.appLinks?.ios : payload?.appLinks?.android;
    window.location.href = payload.deepLink;
    setTimeout(() => { if (fallback) window.location.href = fallback; }, 1500);
  };

  const openWebMap = () => {
    if (!payload) return;
    window.open(`https://www.google.com/maps?q=${payload.lat},${payload.lng}`, '_blank');
  };

  if (loading) {
    return (
      <div className="fixed inset-0 bg-slate-950 flex flex-col items-center justify-center text-white z-50">
        <div className="relative w-24 h-24 mb-6">
          <div className="absolute inset-0 rounded-full border-b-2 border-orange-500 animate-spin" />
          <div className="absolute inset-4 rounded-full border-t-2 border-white/20 animate-spin-slow" />
        </div>
        <p className="text-orange-500 font-bold tracking-widest animate-pulse">VINH KHANH STREET</p>
        <p className="text-white/40 text-xs mt-2">Loading POI Experience...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-slate-950 flex items-center justify-center p-6 text-center">
        <div className="bg-white/5 border border-white/10 rounded-[3rem] p-10 backdrop-blur-2xl max-w-sm">
          <div className="w-20 h-20 bg-red-500/20 text-red-500 rounded-3xl mx-auto mb-6 flex items-center justify-center">
            <Info size={40} />
          </div>
          <h2 className="text-2xl font-bold text-white mb-4">Opps! Có lỗi xảy ra</h2>
          <p className="text-white/60 mb-8">{error}</p>
          <button onClick={() => window.location.reload()} className="w-full py-4 bg-white text-slate-950 rounded-2xl font-black">THỬ LẠI</button>
        </div>
      </div>
    );
  }

  const poiImage = resolveImageUrl(payload?.imageUrl);

  return (
    <div className="relative min-h-screen bg-slate-950 text-slate-100 font-sans selection:bg-orange-500 selection:text-white overflow-x-hidden">
      {/* Background Layer */}
      <div 
        className="fixed inset-0 bg-cover bg-center transition-all duration-1000 scale-105 blur-[2px]"
        style={{ backgroundImage: `url(${poiImage})` }}
      />
      <div className="fixed inset-0 bg-gradient-to-b from-slate-950/40 via-slate-950/80 to-slate-950 z-0" />

      {/* Main Content */}
      <div className="relative z-10 min-h-screen flex flex-col md:pb-10">
        
        {/* Interaction Overlay (Autoplay Bypass) */}
        {!interacted && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-6 bg-slate-950/40 backdrop-blur-sm">
            <button 
              onClick={handleStartExperience}
              className="group relative flex flex-col items-center gap-6"
            >
              <div className="relative">
                <div className="absolute inset-0 bg-orange-500 rounded-full animate-ping opacity-25" />
                <div className="relative w-24 h-24 bg-orange-500 rounded-full flex items-center justify-center text-white shadow-2xl transition group-active:scale-90">
                  <Headphones size={40} />
                </div>
              </div>
              <div className="text-center">
                <p className="text-white text-2xl font-black tracking-tight mb-1">CHẠM ĐỂ KHÁM PHÁ</p>
                <p className="text-white/60 text-sm">Hệ thống sẽ tự động thuyết minh cho bạn</p>
              </div>
            </button>
          </div>
        )}

        {/* Hero Section */}
        <section className="pt-8 pb-4 px-6 max-w-4xl mx-auto w-full">
          <div className="flex justify-between items-center mb-8">
            <div className="flex items-center gap-2 bg-white/10 backdrop-blur-md px-4 py-2 rounded-full border border-white/10">
              <Compass size={14} className="text-orange-400" />
              <span className="text-[10px] uppercase font-black tracking-widest text-white/80">POI Experience</span>
            </div>
            
            <div className="flex gap-2">
              <button 
                onClick={openInApp}
                className="w-10 h-10 rounded-full bg-white/10 flex items-center justify-center border border-white/20"
              >
                <Download size={18} />
              </button>
              <button 
                onClick={openWebMap}
                className="w-10 h-10 rounded-full bg-white/10 flex items-center justify-center border border-white/20"
              >
                <Navigation size={18} />
              </button>
            </div>
          </div>

          {/* Full Size Image Container: Adapts to the image's original aspect ratio */}
          <div className="relative w-full rounded-[3rem] overflow-hidden shadow-[0_40px_100px_rgba(0,0,0,0.5)] border border-white/10 group mb-12">
            <img 
              src={poiImage} 
              alt={selected?.name} 
              className="w-full h-auto block transition duration-1000 group-hover:scale-105" 
            />
            {/* Flexible Scrim Gradient: Stretches from the bottom to create depth and focus */}
            <div className="absolute bottom-0 left-0 right-0 h-1/2 bg-gradient-to-t from-slate-950 via-slate-950/40 to-transparent opacity-80" />
          </div>

          {/* POI Data Card */}
          <div className="bg-white/5 border border-white/10 rounded-[3rem] p-8 md:p-12 backdrop-blur-3xl shadow-2xl">
            <div className="flex flex-wrap items-center gap-3 mb-6">
              {(payload.localizations ?? []).map((loc) => {
                const code = normalizeLanguage(loc.languageCode);
                return (
                  <button
                    key={loc.languageCode}
                    onClick={() => { setLang(code); setInteracted(true); }}
                    className={`flex items-center gap-2 rounded-full px-5 py-2.5 text-xs font-bold transition-all border ${
                      code === lang 
                        ? "bg-orange-500 border-orange-500 text-white shadow-lg shadow-orange-500/20" 
                        : "bg-white/5 border-white/10 text-white/60 hover:bg-white/10"
                    }`}
                  >
                    <Languages size={14} />
                    {getLanguageLabel(code)}
                  </button>
                );
              })}
            </div>

            <h1 className="text-4xl md:text-6xl font-black leading-tight mb-6 bg-gradient-to-r from-white via-white to-white/40 bg-clip-text text-transparent">
              {selected?.name}
            </h1>

            <div className="prose prose-invert max-w-none text-white/70 leading-relaxed text-lg mb-10">
              {selected?.description}
            </div>

            <div className="flex flex-col sm:flex-row gap-4 mb-10">
              <button 
                onClick={() => speak(selected?.description)}
                className={`flex-1 py-5 rounded-2xl flex items-center justify-center gap-3 font-black text-sm transition ${speaking ? 'bg-orange-500 text-white' : 'bg-white/10 text-white hover:bg-white/20 border border-white/10'}`}
              >
                <Volume2 size={20} className={speaking ? 'animate-bounce' : ''} />
                {speaking ? 'ĐANG THUYẾT MINH...' : 'NGHE THUYẾT MINH'}
              </button>
              <button 
                onClick={openWebMap}
                className="flex-1 py-5 rounded-2xl bg-white text-slate-900 flex items-center justify-center gap-3 font-black text-sm shadow-xl"
              >
                <Navigation size={20} />
                DẪN ĐƯỜNG
              </button>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-6 pt-10 border-t border-white/10">
              <Stat icon={<MapPin className="text-orange-400" />} label="Vị trí" value="Check-in ngay" />
              <Stat icon={<Smartphone className="text-blue-400" />} label="Mobile" value="Hỗ trợ iOS/Android" />
              <Stat icon={<Globe className="text-emerald-400" />} label="Ngôn ngữ" value={getLanguageLabel(lang)} />
              <Stat icon={<Volume2 className="text-purple-400" />} label="Thuyết minh" value="Tự động" />
            </div>
          </div>
        </section>

        {/* Map Section */}
        <section className="px-6 max-w-4xl mx-auto w-full mt-10 mb-10">
          <div className="bg-white/5 border border-white/10 rounded-[3rem] p-6 backdrop-blur-3xl overflow-hidden">
             <div className="flex justify-between items-center mb-6 px-4">
                <h3 className="text-xl font-bold">Vị trí thực tế</h3>
                <button onClick={openWebMap} className="text-orange-500 text-sm font-bold flex items-center gap-1">
                  Mở Google Maps <ExternalLink size={14} />
                </button>
             </div>
             <div className="h-64 md:h-80 w-full rounded-[2.5rem] overflow-hidden border border-white/10 grayscale-[30%] opacity-80 transition hover:grayscale-0 hover:opacity-100">
                <MapContainer center={[payload.lat, payload.lng]} zoom={16} style={{ height: '100%', width: '100%' }}>
                  <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
                  <Marker position={[payload.lat, payload.lng]} />
                  <Circle center={[payload.lat, payload.lng]} radius={payload.radius || 50} pathOptions={{ color: '#f97316' }} />
                </MapContainer>
             </div>
          </div>
        </section>

        {/* Action Bar (Sticky Mobile) */}
        <div className="md:hidden sticky bottom-6 px-6 z-50">
          <div className="bg-white/10 backdrop-blur-3xl border border-white/10 rounded-full p-2 flex justify-between shadow-2xl">
             <button 
              onClick={openInApp}
              className="flex-1 py-4 bg-orange-500 text-white rounded-full font-black text-xs flex items-center justify-center gap-1.5"
             >
                <Smartphone size={16} /> MỞ TRONG APP
             </button>
             <div className="w-2" />
             <button 
              onClick={openWebMap}
              className="flex-1 py-4 bg-white text-slate-950 rounded-full font-black text-xs flex items-center justify-center gap-1.5"
             >
                <Navigation size={16} /> DẪN ĐƯỜNG
             </button>
          </div>
        </div>

        {/* Sub-Actions (Download) */}
        <section className="px-6 max-w-4xl mx-auto w-full mb-20">
           <div className="grid md:grid-cols-2 gap-6">
            <div className="bg-gradient-to-br from-indigo-600 to-indigo-900 rounded-[2.5rem] p-8 text-white relative overflow-hidden group md:col-span-2">
                <Download className="absolute -right-4 -bottom-4 w-48 h-48 opacity-10 group-hover:scale-125 transition" />
                <h4 className="text-2xl font-bold mb-2">Trải nghiệm đỉnh cao cùng App</h4>
                <p className="text-white/60 text-lg mb-8 max-w-lg">Tải ứng dụng ngay để sử dụng bản đồ trực tiếp, nhận thông báo thông minh và tính năng thuyết minh tự động khi di chuyển.</p>
                <div className="flex flex-col sm:flex-row gap-4">
                  <a href={payload?.appLinks?.android} target="_blank" rel="noreferrer" className="px-10 py-5 bg-white text-slate-900 rounded-2xl font-black text-sm flex items-center justify-center gap-3 shadow-xl">Android</a>
                  <a href={payload?.appLinks?.ios} target="_blank" rel="noreferrer" className="px-10 py-5 bg-white/10 backdrop-blur-md rounded-2xl font-black text-sm flex items-center justify-center gap-3 border border-white/10">iOS App Store</a>
                </div>
            </div>
           </div>
        </section>

      </div>
    </div>
  );
};

const Stat = ({ icon, label, value }) => (
  <div className="space-y-1">
    <div className="flex items-center gap-1.5 text-[10px] uppercase font-black tracking-widest text-white/40">
      {icon} {label}
    </div>
    <div className="text-sm font-bold text-white/80">{value}</div>
  </div>
);

export default QrPoiPublic;
