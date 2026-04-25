import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import PoiManager from './pages/PoiManager';
import Dashboard from './pages/Dashboard';
import PoiForm from './pages/PoiForm';
import Login from './pages/Login';
import Analytics from './pages/Analytics';
import Settings from './pages/Settings';
import Approvals from './pages/Approvals';
import OwnerManager from './pages/OwnerManager';
import ShopLayout from './pages/shop/ShopLayout';
import ShopPoiList from './pages/shop/ShopPoiList';
import ShopPoiForm from './pages/shop/ShopPoiForm';
import QrPoiPublic from './pages/QrPoiPublic';

function App() {
  return (
    <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/poi/qr/:token" element={<QrPoiPublic />} />
        <Route path="/" element={<Navigate to="/login" replace />} />

        {/* Admin routes */}
        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<Layout />}>
            <Route index element={<Navigate to="/analytics" replace />} />
            <Route path="dashboard" element={<Dashboard />} />
            <Route path="pois" element={<PoiManager />} />
            <Route path="pois/new" element={<PoiForm />} />
            <Route path="pois/:id" element={<PoiForm />} />
            <Route path="approvals" element={<Approvals />} />
            <Route path="owners" element={<OwnerManager />} />
            <Route path="analytics" element={<Analytics />} />
            <Route path="settings" element={<Settings />} />
            <Route path="*" element={<Navigate to="/analytics" replace />} />
          </Route>
        </Route>

        {/* ShopOwner routes */}
        <Route element={<ProtectedRoute />}>
          <Route path="/shop" element={<ShopLayout />}>
            <Route index element={<Navigate to="/shop/pois" replace />} />
            <Route path="pois" element={<ShopPoiList />} />
            <Route path="pois/new" element={<ShopPoiForm />} />
            <Route path="pois/:id/edit" element={<ShopPoiForm />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
