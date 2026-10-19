import { useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import { Mascot, Modal } from './ui';
import { VerdictBanner, TestGrid } from './Results';
import { demoSubmission, makeResults } from './mocks/fixtures';
import './workspace.css';
export function DesignSystemPage() {
  const [open, setOpen] = useState(false);
  return <main className="workspace ws-design"><Link to="/">← CodePath</Link><h1>Một góc học tập thật dễ thương 🌱</h1><p>Sky · Leaf · Sunshine — cùng tinh thần nhẹ nhàng của landing page.</p><section className="ws-card"><h2>Bảng màu</h2><div className="ws-token-grid">{['bg', 'surface', 'primary', 'mint', 'coral', 'butter', 'sky', 'text', 'muted'].map(token => <div key={token}><i style={{ background: `var(--ws-${token})` }} /><p>--ws-{token}</p></div>)}</div></section><section className="ws-card"><h2>Những người bạn nhỏ</h2><div className="ws-design-row">{(['happy', 'thinking', 'sad', 'celebrate'] as const).map(mood => <Mascot mood={mood} size={100} key={mood} />)}</div></section><section className="ws-card"><h2>Thao tác & thông báo</h2><div className="ws-design-row"><button className="ws-submit" onClick={() => toast.success('Đã lưu nháp. Tiếp tục ý tưởng của bạn nhé!')}>Thông báo thành công</button><button onClick={() => setOpen(true)}>Mở hộp thoại</button><button disabled>Đang xử lý</button><span className="ws-badge is-pass">Dễ</span><span className="ws-badge is-wait">Đang chờ</span><span className="ws-badge is-fail">Thử lại nhé</span></div></section><section className="ws-card"><h2>Kết quả bài làm</h2>{['Accepted', 'WrongAnswer', 'CompileError', 'RuntimeError', 'TimeLimitExceeded', 'MemoryLimitExceeded', 'Pending'].map(status => <VerdictBanner key={status} submission={{ ...demoSubmission, id: `design-${status}`, status }} />)}<TestGrid total={15} results={makeResults('PartialPass').slice(0, 11)} /></section><Modal open={open} onOpenChange={setOpen} title="Chào bạn, mình là Mầm!" description="Mỗi ngày một chút, bạn sẽ tiến bộ hơn."><Mascot /><button className="ws-submit" onClick={() => setOpen(false)}>Bắt đầu thôi</button></Modal></main>;
}
