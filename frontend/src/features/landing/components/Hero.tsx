import { useEffect, useState } from 'react'
import { ArrowRight, Pause, Play } from 'lucide-react'
import { heroSlides as slides } from '../data/content'

interface HeroProps {
  onStartCodingClick: () => void
  onExploreSystemClick: () => void
}

export function Hero({ onStartCodingClick, onExploreSystemClick }: HeroProps) {
  const [activeSlide, setActiveSlide] = useState(0)
  const [isPaused, setIsPaused] = useState(false)
  const [hasFocus, setHasFocus] = useState(false)
  const [reducedMotion, setReducedMotion] = useState(() => window.matchMedia('(prefers-reduced-motion: reduce)').matches)
  const isPlaying = !isPaused && !hasFocus && !reducedMotion

  useEffect(() => {
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)')
    const handleChange = () => setReducedMotion(preference.matches)
    preference.addEventListener('change', handleChange)
    return () => preference.removeEventListener('change', handleChange)
  }, [])

  useEffect(() => {
    if (!isPlaying) return
    const timer = window.setInterval(() => {
      if (!document.hidden) setActiveSlide((current) => (current + 1) % slides.length)
    }, 4500)
    return () => window.clearInterval(timer)
  }, [activeSlide, isPlaying])

  const slide = slides[activeSlide]
  const handleAction = () => {
    if (activeSlide === 1 || activeSlide === 2) onExploreSystemClick()
    else if (activeSlide === 3) document.getElementById('courses')?.scrollIntoView({ behavior: 'smooth' })
    else onStartCodingClick()
  }
  return (
    <section id="home" className="education-hero" data-playing={isPlaying} aria-label="Học lập trình cùng CodePart"
      onFocusCapture={(event) => {
        if (event.target.matches(':focus-visible')) setHasFocus(true)
      }}
      onBlurCapture={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) setHasFocus(false)
      }}
    >
      <div className="education-hero__inner">
        <div className="education-hero__copy" aria-live={isPlaying ? 'off' : 'polite'} aria-atomic="true">
          <div key={activeSlide} className="education-hero__text">
            <p className="education-hero__eyebrow">{slide.label}</p>
            <h1><span>{slide.title}</span>{' '}<strong>{slide.emphasis}</strong></h1>
          </div>
          <button className="education-hero__action" onClick={handleAction}>
            <span key={activeSlide} className="education-hero__action-label">{slide.action}</span>
            <span className="education-hero__arrow"><ArrowRight size={23} strokeWidth={2.5} /></span>
          </button>
        </div>
        <img className="education-hero__art" src="/education-hero.svg" alt="Laptop với bóng đèn ý tưởng trên chồng sách, bên cạnh cốc và chậu cây xanh" width="900" height="650" fetchPriority="high" />
        <div className="education-hero__pagination" role="group" aria-label="Chọn nội dung banner">
          {slides.map((item, index) => (
            <button key={item.label} type="button" aria-label={`Banner ${index + 1}: ${item.label}`} aria-pressed={activeSlide === index} onClick={() => setActiveSlide(index)}><span /></button>
          ))}
          {!reducedMotion && (
            <button className="education-hero__pause" type="button" aria-label={isPaused ? 'Tiếp tục chuyển banner' : 'Tạm dừng chuyển banner'} aria-pressed={isPaused} onClick={() => setIsPaused((paused) => !paused)}>
              {isPaused ? <Play size={13} /> : <Pause size={13} />}
            </button>
          )}
        </div>
      </div>
      <ul className="education-hero__languages" aria-label="Ngôn ngữ lập trình">
        {['Java', 'C#', 'Python', 'C++'].map((language) => (
          <li key={language}>{language}</li>
        ))}
      </ul>
    </section>
  )
}
