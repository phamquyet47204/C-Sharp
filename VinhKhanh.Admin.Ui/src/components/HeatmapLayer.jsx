import { useEffect } from 'react';
import { useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet.heat';

const HeatmapLayer = ({ points, maxDensity = 2.2, radius = 25, blur = 15 }) => {
  const map = useMap();

  useEffect(() => {
    if (!map || !points || points.length === 0) return undefined;

    // Kỹ thuật nhân bản điểm để tạo hiệu ứng "Bán kính tăng theo mật độ"
    const expandedPoints = [];
    
    points.forEach(p => {
      const density = p.density || 1;
      
      // Luôn có điểm gốc
      expandedPoints.push([p.lat, p.lng, density]);

      // Chỉ nở vùng nhiệt khi thực sự đông (>= 3 người)
      // Giúp tránh việc 1 người trông quá "nóng"
      if (density >= 3.0) {
        const offset = 0.0001; // ~11m
        expandedPoints.push([p.lat + offset, p.lng, density * 0.5]);
        expandedPoints.push([p.lat - offset, p.lng, density * 0.5]);
        expandedPoints.push([p.lat, p.lng + offset, density * 0.5]);
        expandedPoints.push([p.lat, p.lng - offset, density * 0.5]);
      }
    });

    const heatLayer = L.heatLayer(expandedPoints, {
      radius, 
      blur,
      max: maxDensity,
      minOpacity: 0.3,
      gradient: {
        0.1: 'blue',
        0.2: 'cyan',
        0.3: 'lime',
        0.5: 'yellow',
        0.7: 'orange',
        1.0: 'red'
      }
    }).addTo(map);

    return () => {
      map.removeLayer(heatLayer);
    };
  }, [map, points, maxDensity, radius, blur]);

  return null;
};

export default HeatmapLayer;
