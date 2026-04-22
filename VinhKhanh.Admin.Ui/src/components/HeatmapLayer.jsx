import { useEffect } from 'react';
import { useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet.heat';

const HeatmapLayer = ({ points, maxDensity = 3, radius = 45, blur = 20 }) => {
  const map = useMap();

  useEffect(() => {
    if (!map || !points || points.length === 0) return undefined;

    // Convert our points to the format [lat, lng, intensity] expected by leaflet.heat
    // Here we use 'density' (people/100m2) as the intensity weight
    const data = points.map(p => [p.lat, p.lng, p.density]);

    const heatLayer = L.heatLayer(data, {
      radius,
      blur,
      max: maxDensity, // Lowered this so sparse points (1-2 people) look brighter
      minOpacity: 0.25,
      gradient: {
        0.0: 'blue',
        0.2: 'cyan',
        0.4: 'lime',
        0.6: 'yellow',
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
