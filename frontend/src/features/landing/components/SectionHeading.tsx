interface SectionHeadingProps {
  eyebrow: string
  title: string
  accent: string
  description?: string
  align?: 'left' | 'center'
}

export function SectionHeading({ eyebrow, title, accent, description, align = 'center' }: SectionHeadingProps) {
  return (
    <div className={`landing-heading landing-heading--${align}`} data-reveal>
      <p className="landing-eyebrow">{eyebrow}</p>
      <h2>{title} <span>{accent}</span></h2>
      {description && <p className="landing-description">{description}</p>}
    </div>
  )
}
