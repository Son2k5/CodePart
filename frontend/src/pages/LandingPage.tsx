import { useState, useEffect } from 'react'
import { Navbar } from '../components/Navbar/Navbar'
import { Hero } from '../components/Hero/Hero'
import { InteractiveCodeDemo } from '../components/InteractiveDemo/InteractiveCodeDemo'
import { WorkflowSection } from '../components/Workflow/WorkflowSection'
import { CoursesSection } from '../components/Courses/CoursesSection'
import { TestimonialsSection } from '../components/Testimonials/TestimonialsSection'
import { CtaBanner } from '../components/CtaBanner/CtaBanner'
import { Footer } from '../components/Footer/Footer'
import { Toast, type ToastMessage } from '../components/Toast/Toast'
import { useNavigate } from 'react-router-dom'

import {
  NAVIGATION_ITEMS,
  WORKFLOW_STEPS,
  COURSES_DATA,
  TESTIMONIALS_DATA,
} from '../data/landingData'

export function LandingPage() {
  const [activeSection, setActiveSection] = useState('home')
  const [toasts, setToasts] = useState<ToastMessage[]>([])
  const navigate = useNavigate()

  // Helper to add toast messages
  const addToast = (title: string, message: string, type: 'success' | 'error' | 'info' = 'success') => {
    const id = Date.now().toString()
    setToasts((prev) => [...prev, { id, title, message, type }])
  }

  const dismissToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id))
  }

  // Handle navigate to auth
  const handleNavigateAuth = () => {
    navigate('/auth')
  }

  // Handle navigate to auth for specific course
  const handleRegisterCourse = () => {
    // We can pass state to auth page if we want, but for now just navigate
    navigate('/auth')
  }

  // Handle submission from CTA banner
  const handleCtaRegister = (contact: string) => {
    addToast(
      'Đăng ký thành công!',
      `CodePath đã ghi nhận thông tin (${contact}). Trợ giảng sẽ liên hệ qua email trong vòng 15 phút để kích hoạt tài khoản!`
    )
  }

  // Handle consultation button
  const handleConsult = () => {
    navigate('/auth')
  }

  // Scroll to interactive demo
  const handleExploreExamples = () => {
    const el = document.getElementById('interactive-demo')
    if (el) {
      el.scrollIntoView({ behavior: 'smooth' })
    }
  }

  // Simple active nav tracker on scroll
  useEffect(() => {
    const handleScroll = () => {
      const sections = ['home', 'interactive-demo', 'workflow', 'courses', 'testimonials', 'contact']
      const scrollPosition = window.scrollY + 120

      for (const sectionId of sections) {
        const el = document.getElementById(sectionId)
        if (el) {
          const top = el.offsetTop
          const height = el.offsetHeight
          if (scrollPosition >= top && scrollPosition < top + height) {
            setActiveSection(sectionId === 'interactive-demo' ? 'demo' : sectionId)
            break
          }
        }
      }
    }

    window.addEventListener('scroll', handleScroll, { passive: true })
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  return (
    <div className="min-h-screen bg-white text-slate-900 flex flex-col font-sans selection:bg-blue-100 selection:text-blue-900">
      {/* 1. Header / Navigation */}
      <Navbar
        navItems={NAVIGATION_ITEMS}
        activeSection={activeSection}
        onLoginClick={handleNavigateAuth}
        onRegisterClick={handleNavigateAuth}
      />

      <main className="flex-1">
        {/* 2. Hero Section */}
        <Hero
          onStartCodingClick={handleNavigateAuth}
          onExploreExamplesClick={handleExploreExamples}
        />

        {/* 3. Interactive Student Code Runner Playground */}
        <InteractiveCodeDemo />

        {/* 4. Quy trình đào tạo (Workflow) */}
        <WorkflowSection steps={WORKFLOW_STEPS} />

        {/* 5. Lịch học & Phân bổ (Courses) */}
        <CoursesSection
          courses={COURSES_DATA}
          onRegisterCourse={handleRegisterCourse}
          onConsultClick={handleConsult}
        />

        {/* 6. Cảm nhận học viên (Testimonials) */}
        <TestimonialsSection testimonials={TESTIMONIALS_DATA} />

        {/* 7. Call To Action Form Banner */}
        <CtaBanner onRegisterSubmit={handleCtaRegister} />
      </main>

      {/* 8. Footer */}
      <Footer />

      {/* 10. Toasts */}
      <Toast toasts={toasts} onDismiss={dismissToast} />
    </div>
  )
}
