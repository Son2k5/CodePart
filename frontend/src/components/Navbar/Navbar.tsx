import React, { useState } from 'react'
import { Code2, Menu, X, ArrowRight } from 'lucide-react'
import type { NavItem } from '../../types/landing'

interface NavbarProps {
  navItems: NavItem[]
  activeSection: string
  onLoginClick: () => void
  onRegisterClick: () => void
}

export const Navbar: React.FC<NavbarProps> = ({
  navItems,
  activeSection,
  onLoginClick,
  onRegisterClick,
}) => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)

  return (
    <header className="sticky top-0 z-40 bg-white/95 backdrop-blur-md border-b border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16 sm:h-20">
          {/* Logo */}
          <a href="#home" className="flex items-center gap-2.5 group">
            <div className="w-9 h-9 bg-blue-600 rounded-lg flex items-center justify-center text-white shadow-sm transition group-hover:bg-blue-700">
              <Code2 className="w-5 h-5 stroke-[2.5]" />
            </div>
            <div className="flex items-baseline gap-1">
              <span className="text-xl font-extrabold tracking-tight text-slate-900">CodePath</span>
              <span className="text-xs font-bold text-blue-600 tracking-wider uppercase">Academy</span>
            </div>
          </a>

          {/* Desktop Nav Links */}
          <nav className="hidden md:flex items-center gap-7">
            {navItems.map((item) => {
              const isActive = activeSection === item.id
              return (
                <a
                  key={item.id}
                  href={item.href}
                  className={`text-sm font-medium transition-colors duration-150 py-1 border-b-2 ${
                    isActive
                      ? 'text-blue-600 border-blue-600 font-semibold'
                      : 'text-slate-600 border-transparent hover:text-blue-600'
                  }`}
                >
                  {item.label}
                </a>
              )
            })}
          </nav>

          {/* Desktop Actions */}
          <div className="hidden md:flex items-center gap-3">
            <button
              onClick={onLoginClick}
              className="px-4 py-2 text-sm font-semibold text-slate-700 hover:text-blue-600 hover:bg-slate-50 rounded-lg transition"
            >
              Đăng nhập
            </button>
            <button
              onClick={onRegisterClick}
              className="inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-sm transition hover:shadow duration-150"
            >
              <span>Đăng ký tài khoản</span>
              <ArrowRight className="w-4 h-4" />
            </button>
          </div>

          {/* Mobile Menu Button */}
          <div className="flex md:hidden">
            <button
              type="button"
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="p-2 text-slate-600 hover:text-slate-900 rounded-lg hover:bg-slate-100 transition"
              aria-label="Toggle Navigation Menu"
            >
              {mobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Menu Dropdown */}
      {mobileMenuOpen && (
        <div className="md:hidden bg-white border-b border-slate-200 px-4 pt-2 pb-6 space-y-3 shadow-lg">
          <div className="flex flex-col space-y-2">
            {navItems.map((item) => (
              <a
                key={item.id}
                href={item.href}
                onClick={() => setMobileMenuOpen(false)}
                className={`px-3 py-2 rounded-md text-sm font-medium transition ${
                  activeSection === item.id
                    ? 'bg-blue-50 text-blue-600 font-semibold'
                    : 'text-slate-700 hover:bg-slate-50'
                }`}
              >
                {item.label}
              </a>
            ))}
          </div>
          <div className="pt-3 border-t border-slate-100 flex flex-col gap-2">
            <button
              onClick={() => {
                setMobileMenuOpen(false)
                onLoginClick()
              }}
              className="w-full py-2.5 text-center text-sm font-semibold text-slate-700 hover:bg-slate-50 rounded-lg border border-slate-200 transition"
            >
              Đăng nhập
            </button>
            <button
              onClick={() => {
                setMobileMenuOpen(false)
                onRegisterClick()
              }}
              className="w-full py-2.5 text-center text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-sm transition"
            >
              Kích hoạt tài khoản thực hành
            </button>
          </div>
        </div>
      )}
    </header>
  )
}
