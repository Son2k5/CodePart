import { useEffect } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ArrowRight, BookOpen, Check, Code2, Layers3, ListChecks, Megaphone, Sparkles, Users } from 'lucide-react';
import { WelcomeBanner } from './WelcomeBanner';
import '@fontsource/be-vietnam-pro/400.css';
import '@fontsource/be-vietnam-pro/500.css';
import '@fontsource/be-vietnam-pro/600.css';
import '@fontsource/be-vietnam-pro/700.css';
import { MemberHeader } from './MemberHeader';
import { useClassesQuery, useClassMutation, type Page, type PublicClass } from '../classes/api';

const practiceOptions = [
  { title: 'Luyện tập lập trình', description: 'Thử một bài toán nhỏ, chinh phục một kỹ năng mới.', icon: Code2, tone: 'blue', to: '/problems' },
  { title: 'Luyện trắc nghiệm', description: 'Ôn lại kiến thức qua những câu hỏi ngắn, dễ nhớ.', icon: ListChecks, tone: 'peach', to: null },
  { title: 'Luyện flashcard', description: 'Lật từng thẻ nhỏ, ghi nhớ thêm một điều hay.', icon: Layers3, tone: 'purple', to: null },
];

export function HomePage() {
  const { hash } = useLocation();
  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView({ behavior: 'instant' });
  }, [hash]);
  const classes = useClassesQuery<Page<PublicClass>>('/classes/public', { page: 1, pageSize: 3 });
  const join = useClassMutation();
  return <div className="member-home">
    <a className="fixed -top-24 left-4 z-[100] rounded-lg bg-white p-3 focus:top-3" href="#member-main">Đến nội dung chính</a>
    <MemberHeader />
    <main id="member-main">
      <WelcomeBanner />
      <div className="member-home-content">
        <section id="practice" className="home-section" aria-labelledby="practice-title">
          <div className="home-section-heading"><div><span className="home-eyebrow">MỖI NGÀY MỘT CHÚT TIẾN BỘ</span><h2 id="practice-title">Hôm nay mình cùng luyện gì? <span aria-hidden="true">✦</span></h2><p>Chọn một góc nhỏ để bắt đầu hành trình của bạn.</p></div><span className="home-heading-note"><Sparkles size={16} /> Học vui, nhớ lâu</span></div>
          <div className="home-practice-grid">{practiceOptions.map(({ title, description, icon: Icon, tone, to }) => {
            const content = <><span className={`home-feature-icon ${tone}`}><Icon size={26} aria-hidden="true" /></span><div><h3>{title}</h3><p>{description}</p></div><span className="home-practice-action">{to ? <>Bắt đầu ngay <ArrowRight size={17} /></> : 'Sắp ra mắt'}</span></>;
            return to ? <Link className={`home-practice-card ${tone}`} to={to} key={title}>{content}</Link> : <article className={`home-practice-card ${tone}`} key={title}>{content}</article>;
          })}</div>
        </section>

        <section id="open-classes" className="home-section" aria-labelledby="classes-title">
          <div className="home-section-heading"><div><span className="home-eyebrow">HỌC CÙNG NHAU, ĐI XA HƠN</span><h2 id="classes-title">Các lớp học đang mở</h2><p>Tìm một lớp phù hợp và bắt đầu cùng những người bạn mới.</p></div><Link className="home-text-link" to="/classes">Xem tất cả lớp học <ArrowRight size={16} /></Link></div>
          {classes.isLoading ? <div className="home-class-grid" role="status"><span className="sr-only">Đang tải lớp học…</span>{[0, 1, 2].map(i => <div key={i} className="home-class-skeleton" aria-hidden="true" />)}</div> : classes.isError ? <div className="home-empty" role="alert"><BookOpen size={30} /><h3>Chưa tải được các lớp học</h3><p>Bạn thử tải lại nhé.</p><button type="button" className="home-button" disabled={classes.isFetching} onClick={() => void classes.refetch()}>{classes.isFetching ? 'Đang tải…' : 'Thử lại'}</button></div> : !classes.data?.items.length ? <div className="home-empty"><BookOpen size={34} /><h3>Lớp học mới đang chờ được mở</h3><p>Quay lại sau nhé, hoặc tham gia lớp bằng mã giảng viên cung cấp.</p><Link className="home-text-link" to="/classes">Tham gia bằng mã lớp <ArrowRight size={16} /></Link></div> : <div className="home-class-grid">{classes.data.items.map((c, index) => {
            const full = c.maxStudents !== null && c.studentCount >= c.maxStudents;
            return <article className="home-class-card" key={c.id}>
              <div className={`home-class-cover tone-${index}`}>
                {c.thumbnail ? <img src={c.thumbnail} alt="" loading="lazy" onError={event => { event.currentTarget.style.display = 'none'; }} /> : null}
                <BookOpen className="home-class-cover-icon" size={58} strokeWidth={1.4} aria-hidden="true" />
                <span className="home-class-badge">{c.isEnrolled ? <><Check size={13} /> Đã tham gia</> : full ? 'Đã đủ học viên' : 'Đang mở · Miễn phí'}</span>
                <span className="home-class-decoration" aria-hidden="true">✦</span>
              </div>
              <div className="home-class-body"><h3>{c.name}</h3><p>{c.description || 'Cùng khám phá kiến thức và rèn luyện kỹ năng lập trình mỗi ngày.'}</p><span className="home-class-count"><Users size={15} /> {c.studentCount}{c.maxStudents !== null ? ` / ${c.maxStudents}` : ''} học viên</span><div className="home-class-bottom">{c.isEnrolled ? <Link className="home-button" to={`/classes/${c.id}/learning`}>Vào lớp học <ArrowRight size={16} /></Link> : <button type="button" className="home-button" disabled={full || join.isPending} onClick={() => join.mutate({ path: `/classes/${c.id}/join` })}>{full ? 'Lớp đã đầy' : join.isPending && join.variables?.path === `/classes/${c.id}/join` ? 'Đang tham gia…' : 'Tham gia lớp'}{!full && <ArrowRight size={16} />}</button>}</div></div>
            </article>;
          })}</div>}
        </section>

        <section id="news" className="home-section home-news" aria-labelledby="news-title">
          <div className="home-section-heading"><div><span className="home-eyebrow">GÓC NHỎ CODEPART</span><h2 id="news-title">Tin tức & thông báo</h2><p>Những điều mới trên hành trình học của chúng mình.</p></div><span className="home-news-icon"><Megaphone size={25} aria-hidden="true" /></span></div>
          <div className="home-news-grid"><article className="home-news-feature"><span className="home-news-tag">Khám phá</span><h3>Một lớp học mới, một khởi đầu mới</h3><p>Khám phá các lớp học đang mở, tham gia miễn phí và học cùng cộng đồng CodePart. Đã có mã lớp? Bạn có thể nhập mã tại trang lớp học.</p><Link className="home-text-link" to="/classes">Tìm lớp của bạn <ArrowRight size={16} /></Link></article><div className="home-news-updates"><article><span className="home-feature-icon peach"><ListChecks size={22} /></span><div><span className="home-news-tag">Sắp ra mắt</span><h3>Ôn kiến thức với trắc nghiệm</h3><p>Một cách luyện tập mới đang được chuẩn bị cho bạn.</p></div></article><article><span className="home-feature-icon purple"><Layers3 size={22} /></span><div><span className="home-news-tag">Sắp ra mắt</span><h3>Ghi nhớ nhẹ nhàng cùng flashcard</h3><p>Những thẻ kiến thức nhỏ sẽ sớm có mặt tại góc luyện tập.</p></div></article></div></div>
        </section>
      </div>
    </main>
  </div>;
}
