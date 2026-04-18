import React from 'react';
import { X, MapPin, Globe, User, Languages } from 'lucide-react';
import { MapContainer, TileLayer, Marker, Circle } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

// Fix for default marker icon in Leaflet
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: '/marker-icon-2x.png',
  iconUrl: '/marker-icon.png',
  shadowUrl: '/marker-shadow.png',
});

const PoiDetailModal = ({ isOpen, onClose, poi }) => {
  if (!isOpen || !poi) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-gray-900/60 backdrop-blur-md transition-opacity" 
        onClick={onClose}
      />
      
      {/* Modal Content */}
      <div className="relative w-full max-w-4xl bg-white rounded-[2.5rem] shadow-2xl overflow-hidden flex flex-col max-h-[90vh] animate-in slide-in-from-bottom-8 duration-300">
        {/* Close button */}
        <button 
          onClick={onClose}
          className="absolute top-6 right-6 z-10 bg-white/80 backdrop-blur-md p-2 rounded-full text-gray-500 hover:text-gray-900 transition-all border border-gray-100 shadow-sm"
        >
          <X size={20} />
        </button>

        <div className="flex flex-col lg:flex-row h-full overflow-hidden">
          {/* Left Side: Map & Stats */}
          <div className="lg:w-1/2 h-64 lg:h-auto relative bg-gray-50">
            <MapContainer 
              center={[poi.lat, poi.lng]} 
              zoom={16} 
              style={{ height: '100%', width: '100%' }}
              zoomControl={false}
            >
              <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
              <Marker position={[poi.lat, poi.lng]} />
              <Circle 
                center={[poi.lat, poi.lng]} 
                radius={poi.radius || 50} 
                pathOptions={{ color: '#FF6B6B', fillColor: '#FF6B6B', fillOpacity: 0.2 }}
              />
            </MapContainer>
            
            {/* Coordinate Overlay */}
            <div className="absolute bottom-6 left-6 right-6 bg-white/90 backdrop-blur-md p-4 rounded-2xl shadow-lg border border-white/20">
              <div className="flex justify-between items-center">
                <div className="flex items-center gap-3">
                  <div className="bg-coral-100 p-2 rounded-lg text-coral-600">
                    <MapPin size={18} />
                  </div>
                  <div>
                    <p className="text-[10px] uppercase tracking-wider font-bold text-gray-400">Tọa độ</p>
                    <p className="text-sm font-semibold text-gray-900">{poi.lat.toFixed(6)}, {poi.lng.toFixed(6)}</p>
                  </div>
                </div>
                <div className="bg-coral-50 px-3 py-1 rounded-lg border border-coral-100">
                  <span className="text-xs font-bold text-coral-600">R = {poi.radius}m</span>
                </div>
              </div>
            </div>
          </div>

          {/* Right Side: Details */}
          <div className="lg:w-1/2 flex flex-col overflow-hidden bg-white">
            <div className="p-8 overflow-y-auto">
              <div className="flex items-center gap-2 mb-2">
                <span className="bg-emerald-50 text-emerald-600 text-[10px] font-bold px-2 py-0.5 rounded uppercase tracking-wider">
                  {poi.categoryCode || 'POI'}
                </span>
                <span className="text-gray-300">•</span>
                <div className="flex items-center gap-1 text-gray-400 text-xs">
                  <Globe size={12} />
                  <span>Đa ngôn ngữ</span>
                </div>
              </div>

              <h2 className="text-3xl font-black text-gray-900 mb-6 font-primary">{poi.vi?.name || 'Chưa định danh'}</h2>

              {/* Language Tabs Content */}
              <div className="space-y-8">
                {/* Vietnamese */}
                <section>
                  <div className="flex items-center gap-2 mb-3 text-coral-500">
                    <div className="bg-coral-50 p-1.5 rounded-lg">
                      <Languages size={14} />
                    </div>
                    <h4 className="text-xs font-black uppercase tracking-widest">Tiếng Việt</h4>
                  </div>
                  <div className="bg-gray-50 rounded-2xl p-4 border border-gray-100">
                    <p className="text-sm text-gray-700 leading-relaxed italic border-b border-gray-200 pb-2 mb-2">
                       {poi.vi?.name}
                    </p>
                    <p className="text-sm text-gray-600 leading-relaxed">
                      {poi.vi?.description || 'Không có mô tả.'}
                    </p>
                  </div>
                </section>

                {/* English */}
                <section>
                  <div className="flex items-center gap-2 mb-3 text-emerald-500">
                    <div className="bg-emerald-50 p-1.5 rounded-lg">
                      <Languages size={14} />
                    </div>
                    <h4 className="text-xs font-black uppercase tracking-widest">English</h4>
                  </div>
                  <div className="bg-gray-50 rounded-2xl p-4 border border-gray-100">
                    <p className="text-sm text-gray-700 leading-relaxed italic border-b border-gray-200 pb-2 mb-2">
                       {poi.en?.name || '(Empty)'}
                    </p>
                    <p className="text-sm text-gray-600 leading-relaxed">
                      {poi.en?.description || 'No English description provided.'}
                    </p>
                  </div>
                </section>

                {/* Japanese */}
                <section>
                  <div className="flex items-center gap-2 mb-3 text-blue-500">
                    <div className="bg-blue-50 p-1.5 rounded-lg">
                      <Languages size={14} />
                    </div>
                    <h4 className="text-xs font-black uppercase tracking-widest">Japanese (日本語)</h4>
                  </div>
                  <div className="bg-gray-50 rounded-2xl p-4 border border-gray-100">
                    <p className="text-sm text-gray-700 leading-relaxed italic border-b border-gray-200 pb-2 mb-2">
                       {poi.ja?.name || '(Empty)'}
                    </p>
                    <p className="text-sm text-gray-600 leading-relaxed">
                      {poi.ja?.description || 'No Japanese description provided.'}
                    </p>
                  </div>
                </section>
              </div>
            </div>

            {/* Footer: Shop Owner Info */}
            <div className="mt-auto p-8 bg-gray-50 border-t border-gray-100">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="bg-white w-10 h-10 rounded-full flex items-center justify-center border border-gray-200 text-gray-400">
                    <User size={20} />
                  </div>
                  <div>
                    <p className="text-[10px] font-bold text-gray-400 uppercase tracking-tighter">Gửi bởi Chủ quán</p>
                    <p className="text-sm font-bold text-gray-900">{poi.ownerName || 'N/A'}</p>
                  </div>
                </div>
                <div className="text-right">
                   <p className="text-[10px] font-bold text-gray-400 uppercase tracking-tighter">Mã QR</p>
                   <p className="text-[10px] font-mono text-coral-500">{poi.qrToken || 'PENDING'}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default PoiDetailModal;
