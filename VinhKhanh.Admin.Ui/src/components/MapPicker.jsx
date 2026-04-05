import React, { useState, useEffect } from 'react';
import { MapContainer, TileLayer, Marker, Circle, useMapEvents } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';

const defaultCenter = {
  lat: 10.760, // Vinh Khanh center
  lng: 106.702
};

class MapErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null, errorInfo: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    this.setState({ error, errorInfo });
    console.error("Map Error caught in boundary:", error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="h-[400px] w-full bg-red-50 text-red-600 p-4 rounded-xl overflow-auto border border-red-300">
          <h2 className="font-bold text-lg mb-2">Lỗi kĩ thuật từ Bản đồ:</h2>
          <pre className="text-xs">{this.state.error && this.state.error.toString()}</pre>
          <pre className="text-xs mt-2 text-red-400">{this.state.errorInfo && this.state.errorInfo.componentStack}</pre>
        </div>
      );
    }
    return this.props.children;
  }
}

const LocationMarker = ({ position, setPosition, onChange }) => {
  useMapEvents({
    click(e) {
      const newPos = { lat: e.latlng.lat, lng: e.latlng.lng };
      setPosition(newPos);
      if (onChange) {
        onChange(newPos);
      }
    },
  });

  return position === null ? null : (
    <Marker position={position}></Marker>
  );
};

const MapPickerInner = ({ latitude, longitude, radius, onChange }) => {
  const [position, setPosition] = useState(
    latitude && longitude ? { lat: latitude, lng: longitude } : defaultCenter
  );

  useEffect(() => {
    if (latitude && longitude) {
      setPosition({ lat: latitude, lng: longitude });
    }
  }, [latitude, longitude]);

  return (
    <div className="h-[400px] w-full rounded-3xl overflow-hidden border border-gray-200 shadow-xl relative z-0">
      <MapContainer center={defaultCenter} zoom={16} scrollWheelZoom={true} style={{ height: '100%', width: '100%', zIndex: 0 }}>
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <LocationMarker position={position} setPosition={setPosition} onChange={onChange} />
        {radius && (
          <Circle
            center={position}
            radius={radius}
            pathOptions={{
              color: '#FF7F50',
              fillColor: '#FF7F50',
              fillOpacity: 0.35,
            }}
          />
        )}
      </MapContainer>
      <div className="absolute bottom-6 left-6 z-[400] bg-white/90 backdrop-blur-md px-4 py-2 rounded-2xl shadow-lg border border-gray-100 text-sm font-semibold text-gray-800 flex items-center gap-2">
        <span className="w-2 h-2 rounded-full bg-green-500 flex-shrink-0 animate-pulse"></span>
        <span>Lat: {position.lat.toFixed(5)}</span>
        <span className="text-gray-300">|</span>
        <span>Lng: {position.lng.toFixed(5)}</span>
      </div>
    </div>
  );
};

const MapPicker = (props) => (
  <MapErrorBoundary>
    <MapPickerInner {...props} />
  </MapErrorBoundary>
);

export default React.memo(MapPicker);
