import { useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Navbar } from '../features/landing/components/Navbar'
import { Hero } from '../features/landing/components/Hero'
import { SystemFeatures } from '../features/landing/components/SystemFeatures'
import { AboutSection } from '../features/landing/components/AboutSection'
import { WorkflowSection } from '../features/landing/components/WorkflowSection'
import { LearningPathsSection } from '../features/landing/components/LearningPathsSection'
import { AudienceSection } from '../features/landing/components/AudienceSection'
import { FaqSection } from '../features/landing/components/FaqSection'
import { StartSection } from '../features/landing/components/StartSection'
import { Footer } from '../features/landing/components/Footer'
import { navigation } from '../features/landing/data/content'
import { useActiveSection } from '../features/landing/hooks/useActiveSection'
import { useScrollReveal } from '../features/landing/hooks/useScrollReveal'
import '../features/landing/styles/landing.css'

export function LandingPage() {
  const root = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  const activeSection = useActiveSection()
  useScrollReveal(root)

  const handleStart = () => navigate('/register')
  const handleLogin = () => navigate('/login')
  const handleExplore = () => {
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    document.getElementById('system-features')?.scrollIntoView({ behavior: reducedMotion ? 'instant' : 'smooth' })
  }

  return (
    <div ref={root} className="landing-page">
      <a className="landing-skip-link" href="#main-content">Đến nội dung chính</a>
      <Navbar navItems={navigation} activeSection={activeSection} onLoginClick={handleLogin} onRegisterClick={handleStart} />
      <main id="main-content">
        <Hero onStartCodingClick={handleStart} onExploreSystemClick={handleExplore} />
        <SystemFeatures />
        <AboutSection />
        <WorkflowSection />
        <LearningPathsSection onStart={handleStart} />
        <AudienceSection onStart={handleStart} />
        <FaqSection />
        <StartSection onStart={handleStart} onLogin={handleLogin} />
      </main>
      <Footer />
    </div>
  )
}
