import { useEffect, useState } from 'react'
import { navigation } from '../data/content'

export function useActiveSection() {
  const [activeSection, setActiveSection] = useState('home')
  useEffect(() => {
    let frame = 0
    const update = () => {
      frame = 0
      let current = 'home'
      for (const item of navigation) {
        const element = document.getElementById(item.id)
        if (element && element.getBoundingClientRect().top <= 160) current = item.id
      }
      setActiveSection(current)
    }
    const schedule = () => { if (!frame) frame = requestAnimationFrame(update) }
    update()
    window.addEventListener('scroll', schedule, { passive: true })
    window.addEventListener('resize', schedule)
    return () => {
      cancelAnimationFrame(frame)
      window.removeEventListener('scroll', schedule)
      window.removeEventListener('resize', schedule)
    }
  }, [])
  return activeSection
}
