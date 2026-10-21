import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, Pause, Play } from 'lucide-react';
import { useAuthStore } from '../auth/store/authStore';

const slides = [
  { title: 'Một ngày mới,', emphasis: 'một điều hay để học', action: 'Tiếp tục hành trình của bạn', to: '/courses' },
  { title: 'Viết từng dòng code,', emphasis: 'vững từng kỹ năng', action: 'Bắt đầu luyện tập', to: '/problems' },
  { title: 'Từ những điều nhỏ,', emphasis: 'đến ý tưởng lớn', action: 'Khám phá các lớp học', to: '/classes' },
  { title: 'Thử sức mỗi ngày,', emphasis: 'tiến bộ từng bước', action: 'Chinh phục bài tập mới', to: '/problems' },
];

export function WelcomeBanner() {
  const name = useAuthStore(state => state.user?.fullName.trim() || 'bạn');
  const [active, setActive] = useState(0);
  const [paused, setPaused] = useState(false);
  const [focused, setFocused] = useState(false);
  const [hovered, setHovered] = useState(false);
  const [reducedMotion, setReducedMotion] = useState(() => window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  const copy = useRef<HTMLDivElement>(null);
  const art = useRef<HTMLImageElement>(null);
  const playing = !paused && !focused && !hovered && !reducedMotion;
  const slide = slides[active];

  useEffect(() => {
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    const update = () => setReducedMotion(media.matches);
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, []);

  useEffect(() => {
    if (!playing) return;
    const timer = window.setInterval(() => {
      if (!document.hidden) setActive(index => (index + 1) % slides.length);
    }, 4500);
    return () => window.clearInterval(timer);
  }, [playing, active]);

  useEffect(() => {
    if (reducedMotion) return;
    const animations = Array.from(copy.current?.querySelectorAll('[data-reveal]') || []).map((element, index) => element.animate(
      [{ opacity: 0, transform: 'translateY(16px)' }, { opacity: 1, transform: 'translateY(0)' }],
      { duration: 650, delay: index * 100, easing: 'ease-out', fill: 'both' },
    ));
    return () => animations.forEach(animation => animation.cancel());
  }, [active, reducedMotion]);

  useEffect(() => {
    if (!playing) return;
    const animation = art.current?.animate(
      [{ transform: 'translateY(0)' }, { transform: 'translateY(-12px)' }, { transform: 'translateY(0)' }],
      { duration: 6000, iterations: Infinity, easing: 'ease-in-out' },
    );
    return () => animation?.cancel();
  }, [playing]);

  return <section aria-label="Chào mừng đến với CodePart" aria-roledescription="trình chiếu" className="relative overflow-hidden bg-white font-sans"
    onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}
    onFocusCapture={() => setFocused(true)} onBlurCapture={event => { if (!event.currentTarget.contains(event.relatedTarget)) setFocused(false); }}>
    <div aria-hidden="true" className="pointer-events-none absolute -top-[8%] left-[42%] h-[95%] w-[110%] -rotate-8 rounded-[48%_0_0_58%] bg-linear-[100deg,#e0f3ff,#b5eaf5] min-[761px]:-top-[40%] min-[761px]:left-[45%] min-[761px]:h-[130%] min-[761px]:w-3/4" />
    <div aria-hidden="true" className="pointer-events-none absolute top-[77%] left-[5%] h-1/2 w-[125%] rotate-20 rounded-[50%_65%_0_0] bg-white min-[761px]:top-[60%] min-[761px]:left-[36%] min-[761px]:h-[160%] min-[761px]:w-[110%]" />
    <div className="relative z-10 mx-auto flex max-w-[1360px] flex-col px-6 pt-7 pb-5 min-[761px]:min-h-[490px] min-[761px]:flex-row min-[761px]:items-start min-[761px]:px-8 min-[761px]:pt-12 min-[761px]:pb-[85px] min-[1101px]:min-h-[550px] min-[1101px]:px-14 min-[1101px]:pt-[65px] min-[1101px]:pb-[90px]">
      <div ref={copy} className="relative z-20 w-full min-[761px]:w-[51%] min-[761px]:pt-[30px] min-[1101px]:w-[49%]" aria-live={playing ? 'off' : 'polite'} aria-atomic="true">
        <p data-reveal className="mb-2 min-h-[2.6em] text-lg leading-relaxed break-words text-[#8b8d8d] min-[761px]:text-[19px] min-[1101px]:text-[23px]">Chào mừng trở lại, {name}!</p>
        <h1 className="min-h-[2.5em] text-[clamp(28px,6.5vw,44px)] leading-[1.25] font-light tracking-[-1.2px] text-[#686a68] min-[761px]:text-[clamp(36px,3.8vw,54px)] min-[761px]:tracking-[-1.8px]">
          <span data-reveal className="block">{slide.title}</span><strong data-reveal className="block font-[750]">{slide.emphasis}</strong>
        </h1>
        <Link to={slide.to} className="group mt-7 flex min-h-[50px] w-full max-w-[425px] items-center justify-between gap-3 rounded-full bg-white py-[5px] pr-[6px] pl-[22px] text-sm text-[#737773] shadow-[0_3px_19px_#0000001c] transition-shadow hover:shadow-[0_4px_22px_#0000002d] motion-reduce:transition-none min-[761px]:mt-10 min-[761px]:text-[15px]">
          <span data-reveal>{slide.action}</span><span className="grid size-[39px] shrink-0 place-items-center rounded-full bg-[#ffd642] text-white"><ArrowRight size={23} strokeWidth={2.5} className="transition-transform group-hover:translate-x-[3px] motion-reduce:transition-none" /></span>
        </Link>
      </div>
      <img ref={art} src="/education-hero.svg" alt="Laptop với bóng đèn ý tưởng trên chồng sách, bên cạnh cốc và chậu cây xanh" width="900" height="650" fetchPriority="high" className="pointer-events-none relative z-10 mt-5 h-auto w-full max-w-[340px] self-center object-contain min-[761px]:absolute min-[761px]:right-[-24px] min-[761px]:bottom-[30px] min-[761px]:mt-0 min-[761px]:w-[57%] min-[761px]:max-w-none min-[1101px]:right-0 min-[1101px]:bottom-[35px]" />
      <div role="group" aria-label="Điều khiển banner" className="relative z-30 mt-4 flex items-center min-[761px]:absolute min-[761px]:bottom-8 min-[761px]:left-8 min-[1101px]:bottom-[38px] min-[1101px]:left-14">
        {slides.map((item, index) => <button key={item.title} type="button" aria-label={`Banner ${index + 1}: ${item.title} ${item.emphasis}`} aria-pressed={active === index} onClick={() => { setActive(index); setPaused(true); }} className="grid size-9 place-items-center rounded-full hover:bg-slate-100/70"><span className="grid size-[14px] place-items-center rounded-full border border-[#aaa] bg-white">{active === index && <span className="size-2 rounded-full bg-[#ffd642]" />}</span></button>)}
        {!reducedMotion && <button type="button" aria-label={paused ? 'Tiếp tục chuyển banner' : 'Tạm dừng chuyển banner'} aria-pressed={paused} onClick={() => setPaused(value => !value)} className="ml-2 grid size-9 place-items-center rounded-full text-[#818b8b] hover:bg-slate-100/70">{paused ? <Play size={15} /> : <Pause size={15} />}</button>}
      </div>
    </div>
    <ul aria-label="Ngôn ngữ lập trình" className="relative z-10 flex flex-wrap items-center justify-center gap-x-[clamp(32px,7vw,100px)] gap-y-4 border-t border-[#e2eaec] px-6 py-5 text-[clamp(17px,1.6vw,22px)] font-semibold tracking-[-0.5px] text-[#7b898d]">
      {['Java', 'C#', 'Python', 'C++'].map(language => <li key={language}>{language}</li>)}
    </ul>
  </section>;
}
