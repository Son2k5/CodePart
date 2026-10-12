import { ClipboardList, CodeXml, Users } from 'lucide-react'
import { systemFeatures } from '../data/content'

const icons = { assignments: ClipboardList, code: CodeXml, people: Users }

export function SystemFeatures() {
  return (
    <section id="system-features" className="system-features" aria-labelledby="system-features-title">
      <div className="system-features__inner">
        <div className="system-features__intro" data-reveal>
          <h2 id="system-features-title">Một nền tảng. <span>Trọn vẹn hành trình học code.</span></h2>
          <p>CodePart kết nối bài tập, môi trường lập trình và quản lý lớp học,<br className="system-features__line-break" /> giúp việc dạy và học lập trình trở nên đơn giản hơn.</p>
        </div>
        <div className="system-features__grid">
          {systemFeatures.map(({ title, description, icon, tone }, index) => {
            const Icon = icons[icon]
            return (
            <article key={title} className="system-features__card" data-reveal style={{ transitionDelay: `${index * 90}ms` }}>
              <div className={`system-features__icon system-features__icon--${tone}`} aria-hidden="true">
                <Icon size={28} strokeWidth={1.7} />
              </div>
              <h3>{title}</h3>
              <p>{description}</p>
            </article>
            )
          })}
        </div>
      </div>
    </section>
  )
}
