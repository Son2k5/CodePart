import { useCallback, useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { AdminHeader } from '../components/AdminHeader';
import { AdminSidebar } from '../components/AdminSidebar';
import '../styles/admin-animations.css';
export function AdminLayout() {
  const [theme, setTheme] = useState<'light' | 'dark'>(() => {
    try {
      const saved = localStorage.getItem('codepart-admin-theme');
      if (saved === 'light' || saved === 'dark') return saved;
    } catch { /* Storage may be unavailable. */ }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  });
  useEffect(() => {
    try { localStorage.setItem('codepart-admin-theme', theme); } catch { /* Keep the in-memory preference. */ }
  }, [theme]);
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);
  const closeMobile = useCallback(() => setIsMobileSidebarOpen(false), []);
  useEffect(() => {
    const desktop = window.matchMedia('(min-width: 1024px)');
    const resize = () => { if (desktop.matches) closeMobile(); };
    desktop.addEventListener('change', resize);
    return () => desktop.removeEventListener('change', resize);
  }, [closeMobile]);
  const toggleSidebar = () => {
    setIsMobileSidebarOpen((value) => !value);
  };
  return <div data-theme={theme} className="admin-shell flex flex-col min-h-dvh bg-[#ededf0] text-sm text-neutral-800 antialiased [&_button:disabled]:cursor-wait [&_button:disabled]:opacity-50">
    <a href="#admin-main-content" className="fixed -top-25 left-5 z-100 bg-white p-3 text-neutral-900 focus:top-3">Chuyển đến nội dung</a>
    <AdminHeader theme={theme} onToggleTheme={() => setTheme((current) => current === 'light' ? 'dark' : 'light')} onToggleSidebar={toggleSidebar} isMobileSidebarOpen={isMobileSidebarOpen} />
    <div className="flex min-w-0 flex-1">
      <AdminSidebar isCollapsed={false} isMobileOpen={isMobileSidebarOpen} onCloseMobile={closeMobile} />
      <div className="min-w-0 flex-1 flex flex-col bg-[#ededf0]">
        <main id="admin-main-content" className="flex-1 w-full p-4 sm:p-6" tabIndex={-1}><Outlet /></main>
      </div>
    </div>
  </div>;
}
