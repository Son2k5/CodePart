import { useEffect, type RefObject } from 'react'

/** Reveal each element once; keep content visible without observer support. */
export function useScrollReveal(root: RefObject<HTMLDivElement | null>) {
  useEffect(() => {
    const container = root.current
    if (!container || !('IntersectionObserver' in window)) return
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)')
    const elements = container.querySelectorAll<HTMLElement>('[data-reveal]')
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(({ target, isIntersecting }) => {
        if (isIntersecting) {
          target.classList.remove('reveal-pending')
          observer.unobserve(target)
        }
      })
    }, { threshold: 0.12 })
    const configure = () => {
      observer.disconnect()
      elements.forEach((element) => {
        element.classList.remove('reveal-pending')
        if (!preference.matches && element.getBoundingClientRect().top > window.innerHeight) {
          element.classList.add('reveal-pending')
          observer.observe(element)
        }
      })
    }
    configure()
    preference.addEventListener('change', configure)
    return () => {
      observer.disconnect()
      preference.removeEventListener('change', configure)
      elements.forEach((element) => element.classList.remove('reveal-pending'))
    }
  }, [root])
}
