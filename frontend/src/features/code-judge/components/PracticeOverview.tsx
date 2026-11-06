import type { PracticeProgress } from '../api';
import { difficultyLabels } from '../problemCategories';

type Props = { progress?: PracticeProgress[]; title: string; hasFilters: boolean };
const arc = 'M 37.574 122.426 A 60 60 0 1 1 122.426 122.426';

export function PracticeOverview({ progress, title, hasFilters }: Props) {
  const ready = progress !== undefined;
  const total = progress?.reduce((sum, item) => sum + item.total, 0) ?? 0;
  const solved = progress?.reduce((sum, item) => sum + item.solved, 0) ?? 0;
  let offset = 0;
  const levels = Object.entries(difficultyLabels).map(([difficulty, label]) => {
    const item = progress?.find(item => item.difficulty === difficulty);
    const level = { difficulty, label, total: item?.total ?? 0, solved: item?.solved ?? 0, offset };
    offset += total ? level.total / total * 100 : 0;
    return level;
  });

  return <aside className="practice-overview" aria-label="Tiến độ giải bài">
    <section className="practice-progress-card">
      <h2>{title}</h2>
      <div className="practice-progress-body">
        <dl className="practice-progress-legend">{levels.map(level => <div key={level.difficulty} className={level.difficulty.toLowerCase()}>
          <dt>{level.label}</dt><dd>{ready ? <><strong>{level.solved}</strong><span>/{level.total}</span></> : '—'}</dd>
        </div>)}</dl>
        <div className="practice-progress-ring" role="img" aria-label={ready ? `Đã giải ${solved} trên ${total} bài tập` : 'Chưa có số liệu tiến độ'}>
          <svg viewBox="0 0 160 160" aria-hidden="true">
            <path className="practice-ring-track" d={arc} pathLength="100" />
            {levels.filter(level => level.solved > 0 && total > 0).map(level => <path key={level.difficulty} className={`practice-ring-value ${level.difficulty.toLowerCase()}`} d={arc} pathLength="100" strokeDasharray={`${level.solved / total * 100} 100`} strokeDashoffset={-level.offset} />)}
            <circle className="practice-ring-start" cx="37.574" cy="122.426" r="4" />
            <circle className="practice-ring-end" cx="122.426" cy="122.426" r="4" />
          </svg>
          <div className="practice-ring-label"><strong>{ready ? solved : '—'}</strong><span>/{ready ? total : '—'}</span><small>Đã giải</small></div>
        </div>
      </div>
      {hasFilters && <p className="practice-progress-caption">Theo bộ lọc hiện tại</p>}
      {!ready && <p className="practice-progress-caption" role="status">Chưa có số liệu tiến độ.</p>}
    </section>
  </aside>;
}
