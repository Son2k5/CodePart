import { useState } from 'react'
import { ArrowUpRight } from 'lucide-react'
import { learningPaths } from '../data/content'
import { SectionHeading } from './SectionHeading'

const filters = [{ id: 'all', label: 'Tất cả' }, { id: 'foundation', label: 'Nền tảng' }, { id: 'advanced', label: 'Nâng cao' }] as const

const languageArtwork: Record<string, { file: string; name: string }> = {
  python: { file: 'python', name: 'Python' },
  java: { file: 'java', name: 'Java' },
  oop: { file: 'csharp', name: 'C#' },
  algorithms: { file: 'cplusplus', name: 'C++' },
}

export function LearningPathsSection({ onStart }: { onStart: () => void }) {
  const [filter, setFilter] = useState<typeof filters[number]['id']>('all')
  const paths = learningPaths.filter((path) => filter === 'all' || path.category === filter)
  return (
    <section id="courses" className="landing-section">
      <div className="landing-container">
        <SectionHeading eyebrow="NỘI DUNG HỌC TẬP" title="Bắt đầu từ nền tảng." accent="Mở rộng khả năng." description="Khám phá các hướng thực hành, từ cú pháp đầu tiên đến những bài toán cần tư duy sâu hơn." />
        <div className="learning-filters" role="group" aria-label="Lọc học phần">
          {filters.map((item) => <button key={item.id} type="button" aria-pressed={filter === item.id} onClick={() => setFilter(item.id)}>{item.label}</button>)}
        </div>
        <div className="learning-paths" aria-live="polite">
          {paths.map((path) => (
            <article key={path.id} className={`learning-path learning-path--${path.tone}`}>
              <div className="learning-path__visual" aria-hidden="true">
                <span className="learning-path__code">{path.mark}</span>
                <img src={`/images/languages/${languageArtwork[path.id].file}.svg`} alt="" width="80" height="80" loading="lazy" decoding="async" />
                <span className="learning-path__visual-name">{languageArtwork[path.id].name}</span>
              </div>
              <div className="learning-path__top"><span className="learning-path__subject">{path.category === 'foundation' ? 'LÀM QUEN & THỰC HÀNH' : 'TƯ DUY & ỨNG DỤNG'}</span><span>{path.level}</span></div>
              <h3>{path.name}</h3>
              <p>{path.description}</p>
              <button className="landing-text-link" onClick={onStart} aria-label={`Bắt đầu học ${path.name}`}>Bắt đầu học <ArrowUpRight size={18} aria-hidden="true" /></button>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
