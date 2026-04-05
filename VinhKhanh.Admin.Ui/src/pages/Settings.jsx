import React from 'react';

const Settings = () => {
  return (
    <section className="space-y-6">
      <header>
        <h2 className="text-3xl font-bold text-gray-900">Cai dat</h2>
        <p className="text-sm text-gray-500 mt-2">
          Khu vuc cau hinh he thong se duoc mo rong o cac ban tiep theo.
        </p>
      </header>

      <div className="rounded-3xl border border-gray-100 bg-white p-8 shadow-sm">
        <p className="text-gray-700 leading-relaxed">
          Hien tai phan cai dat da co route rieng de tranh bi chuyen huong sai sang trang login.
          Ban co the bo sung cac cau hinh nhu ngon ngu, phan quyen va tuy chinh he thong tai day.
        </p>
      </div>
    </section>
  );
};

export default Settings;
