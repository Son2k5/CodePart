import { useState } from 'react'
import { Menu, X } from 'lucide-react'
import type { NavItem } from '../types'

interface NavbarProps {
  navItems?: NavItem[]
  activeSection?: string
  onLoginClick: () => void
  onRegisterClick: () => void
}

export function Navbar({ navItems = [], activeSection = 'home', onLoginClick, onRegisterClick }: NavbarProps) {
  const [menuOpen, setMenuOpen] = useState(false)
  const primaryLinks = navItems.filter((item) => ['home', 'about', 'courses', 'workflow'].includes(item.id))
  return (
    <header className="education-nav" onKeyDown={(event) => {
      if (event.key === 'Escape' && menuOpen) {
        setMenuOpen(false)
        event.currentTarget.querySelector<HTMLButtonElement>('.education-nav__toggle')?.focus()
      }
    }}>
      <div className="education-nav__inner">
        <div className="education-nav__brand">
          <button className="education-nav__toggle" aria-label={menuOpen ? 'Đóng menu' : 'Mở menu'} aria-expanded={menuOpen} aria-controls={menuOpen ? 'education-menu' : undefined} onClick={() => setMenuOpen(!menuOpen)}>
            {menuOpen ? <X size={27} /> : <Menu size={27} />}
          </button>
          <a href="#home" aria-label="CodePart — Trang chủ">CodePart</a>
        </div>
        <nav className="education-nav__links" aria-label="Điều hướng chính">
          {primaryLinks.map((item) => <a key={item.id} href={item.href} aria-current={activeSection === item.id ? 'location' : undefined}>{item.label}</a>)}
        </nav>
        <button className="education-nav__signup" onClick={onRegisterClick}>Đăng ký</button>
        {menuOpen && (
          <nav id="education-menu" className="education-nav__menu" aria-label="Menu mở rộng">
            {navItems.map((item) => <a key={item.id} href={item.href} onClick={() => setMenuOpen(false)}>{item.label}</a>)}
            <button onClick={() => { setMenuOpen(false); onLoginClick() }}>Đăng nhập</button>
          </nav>
        )}
      </div>
    </header>
  )
}
