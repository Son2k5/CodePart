import { Link, Outlet } from 'react-router-dom';
import { ArrowUp, Code2, Heart, Sparkles } from 'lucide-react';

export function MemberFooter() {
  return <footer className="member-footer">
    <div className="member-footer-inner">
      <div className="member-footer-top">
        <div><Link to="/home" className="member-footer-brand"><span><Code2 size={25} /></span>CodePart<span className="member-footer-sparkle" aria-hidden="true">✦</span></Link><p>Mỗi ngày một chút tò mò,<br />mỗi dòng code một bước tiến.</p><span className="member-footer-note"><Heart size={14} /> Cùng bạn học, cùng bạn lớn lên</span></div>
        <nav aria-label="Học tập cùng CodePart"><h2>Góc học tập</h2><Link to="/courses">Khóa học của tôi</Link><Link to="/classes">Khám phá lớp học</Link><Link to="/problems">Luyện tập lập trình</Link></nav>
        <nav aria-label="Khám phá trang chủ"><h2>Cùng khám phá</h2><Link to="/home#practice">Góc luyện tập</Link><Link to="/home#news">Tin tức & thông báo</Link><Link to="/home#open-classes">Lớp học đang mở</Link></nav>
        <div className="member-footer-message"><Sparkles size={24} aria-hidden="true" /><strong>Bạn đang làm tốt lắm!</strong><p>Không cần vội vàng.<br />Chỉ cần hôm nay tiến thêm một chút.</p><span aria-hidden="true">&lt; / &gt;</span></div>
      </div>
      <div className="member-footer-bottom"><span>© {new Date().getFullYear()} CodePart · Học từng chút, vững từng ngày.</span><button type="button" onClick={() => window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' })}>Về đầu trang <ArrowUp size={15} /></button></div>
    </div>
  </footer>;
}

export function MemberFooterLayout() {
  return <div className="member-shell"><div className="member-shell-content"><Outlet /></div><MemberFooter /></div>;
}
