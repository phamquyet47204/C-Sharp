import { useEffect } from 'react';
import { useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet.heat';

const HeatmapLayer = ({ points, maxDensity = 2.2 }) => {
  const map = useMap();

  useEffect(() => {
    if (!map || !points || points.length === 0) return undefined;

    // Kỹ thuật nhân bản điểm để tạo hiệu ứng "Bán kính tăng theo mật độ"
    const expandedPoints = [];
    
    points.forEach(p => {
      const density = p.density;
      
      // Luôn có điểm gốc
      expandedPoints.push([p.lat, p.lng, density]);

      // Nếu mật độ cao, thêm các điểm vệ tinh siêu gần để làm "nở" bán kính vùng nhiệt
      if (density > 1.5) {
        const offset = 0.00008; // Khoảng cách cực nhỏ (~8-10m)
        expandedPoints.push([p.lat + offset, p.lng, density * 0.6]);
        expandedPoints.push([p.lat - offset, p.lng, density * 0.6]);
        expandedPoints.push([p.lat, p.lng + offset, density * 0.6]);
        expandedPoints.push([p.lat, p.lng - offset, density * 0.6]);
      }

      if (density > 3.0) {
        const offset2 = 0.00015; // Lớp thứ 2 cho vùng cực đông
        expandedPoints.push([p.lat + offset2, p.lng + offset2, density * 0.4]);
        expandedPoints.push([p.lat - offset2, p.lng - offset2, density * 0.4]);
        expandedPoints.push([p.lat + offset2, p.lng - offset2, density * 0.4]);
        expandedPoints.push([p.lat - offset2, p.lng + offset2, density * 0.4]);
      }
    });

    const heatLayer = L.heatLayer(expandedPoints, {
      radius: 24, // Cố định 10px trên màn hình bất kể zoom
      blur: 8,
      max: maxDensity,
      minOpacity: 0.5,
      gradient: {
        0.1: 'blue',
        0.25: 'lime',
        0.4: 'yellow',
        0.6: 'orange',
        0.80: 'red'
      }
    }).addTo(map);

    return () => {
      map.removeLayer(heatLayer);
    };
  }, [map, points, maxDensity]);

  return null;
};

export default HeatmapLayer;
