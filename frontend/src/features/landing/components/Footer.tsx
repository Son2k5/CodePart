export function Footer() {
  return (
    <footer className="landing-footer">
      <div className="landing-container">
        <div className="landing-footer__main">
          <div><a href="#home" className="landing-footer__brand">CodePart</a><p>Không gian thực hành lập trình.<br />Học từng chút, vững từng ngày.</p></div>
          <nav aria-label="Khám phá CodePart"><h2>Khám phá</h2><a href="#about">Về CodePart</a><a href="#system-features">Tính năng hệ thống</a><a href="#workflow">Cách học</a></nav>
          <nav aria-label="Học tập"><h2>Học tập</h2><a href="#courses">Các học phần</a><a href="#faq">Câu hỏi thường gặp</a><a href="#contact">Bắt đầu cùng CodePart</a></nav>
        </div>
        <div className="landing-footer__bottom"><span>© {new Date().getFullYear()} CodePart</span><span>Được xây dựng cho hành trình học lập trình.</span><a href="#home">Về đầu trang ↑</a></div>
      </div>
    </footer>
  )
}
