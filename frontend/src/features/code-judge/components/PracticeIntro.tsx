import { ArrowRight } from 'lucide-react';
const shortcuts = [
  { id: 'basics', title: 'Lập trình cơ bản', detail: 'Bắt đầu từ nền tảng', number: '01' },
  { id: 'data-structures', title: 'Cấu trúc dữ liệu', detail: 'Rèn tư duy giải quyết vấn đề', number: '02' },
  { id: 'dynamic-programming', title: 'Quy hoạch động', detail: 'Thử sức với bài nâng cao', number: '03' },
];

type Props = { classView: boolean; category: string; title: string; onCategoryChange: (value: string) => void };
export function PracticeIntro({ classView, category, title, onCategoryChange }: Props) {
  return <>        {!classView && <nav className="practice-shortcuts" aria-label="Khám phá chủ đề">
          {shortcuts.map(s => <button key={s.id} type="button" aria-pressed={category === s.id} onClick={() => onCategoryChange(s.id)}><span className="practice-shortcut-number">{s.number}</span><span><strong>{s.title}</strong><small>{s.detail}</small></span><ArrowRight size={16} /></button>)}
        </nav>}
        <section className="practice-intro">
          
          <h1>{title}<span>.</span></h1>
        </section></>;
}
