import { LockKeyhole } from 'lucide-react';
export function HintStepper({ unlockedLevel, currentLevel, busy, remaining, onSelect }: { unlockedLevel: number; currentLevel?: number; busy: boolean; remaining?: number; onSelect: (level: number) => void }) {
  return <div><nav aria-label="Cấp gợi ý">{[1, 2, 3].map(level => {
    const locked = level > unlockedLevel + 1;
    return <button key={level} disabled={busy || locked || remaining === 0} aria-pressed={currentLevel === level} title={locked ? 'Thử sửa và nộp thêm bài, hoặc chờ để mở gợi ý tiếp theo.' : undefined} onClick={() => onSelect(level)}>{locked && <LockKeyhole size={13} />}Gợi ý {level}</button>;
  })}</nav>{unlockedLevel < 2 && <p className="ws-muted" style={{ marginTop: 8 }}>🔒 Cấp tiếp theo mở theo tiến độ làm bài. Thử sửa, nộp thêm hoặc quay lại sau nhé.</p>}</div>;
}
