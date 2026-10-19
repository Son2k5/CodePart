import { Component, type ReactNode } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { X } from 'lucide-react';

export function Mascot({ mood = 'happy', size = 72 }: { mood?: 'happy' | 'thinking' | 'sad' | 'celebrate'; size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 100 100" fill="none" role="img" aria-label={({ happy: 'Mầm nhỏ vui vẻ', thinking: 'Mầm nhỏ đang suy nghĩ', sad: 'Mầm nhỏ động viên bạn', celebrate: 'Mầm nhỏ chúc mừng' })[mood]}>
    <ellipse cx="50" cy="90" rx="28" ry="5" fill="#DCEAE0" />
    <path d="M49 24C29 25 24 11 28 7c16-2 25 6 21 17Z" fill="#9FD976" /><path d="M50 25C49 10 65 5 73 8c0 13-10 20-23 17Z" fill="#75BD59" />
    <path d="M21 51c0-19 13-29 29-29s30 10 30 29v15c0 16-13 23-30 23S21 82 21 66V51Z" fill="#E2F3CB" stroke="#92BD79" strokeWidth="2" />
    <path d={mood === 'celebrate' ? 'M21 61 9 43M80 61l12-18' : 'M21 61 11 67M80 61l10 6'} stroke="#92BD79" strokeWidth="5" strokeLinecap="round" />
    {mood === 'celebrate' ? <path d="m33 51 5-4 5 4m15 0 5-4 5 4" stroke="#385C46" strokeWidth="3" strokeLinecap="round" /> : <><ellipse cx="38" cy="51" rx="3" ry="4" fill="#385C46" /><ellipse cx="63" cy="51" rx="3" ry="4" fill="#385C46" /></>}
    <ellipse cx="30" cy="60" rx="6" ry="3" fill="#F6B7A9" /><ellipse cx="72" cy="60" rx="6" ry="3" fill="#F6B7A9" />
    <path d={mood === 'sad' ? 'M45 68q5-6 11 0' : mood === 'thinking' ? 'M48 66h7' : 'M44 63q7 10 14 0'} stroke="#385C46" strokeWidth="2.5" strokeLinecap="round" />
    {mood === 'celebrate' && <><path d="m9 20 2 5 5 2-5 2-2 5-2-5-5-2 5-2Z" fill="#F4C855" /><circle cx="88" cy="25" r="4" fill="#A9DCEC" /></>}
  </svg>;
}
export function Loading({ text = 'Đang chuẩn bị góc học tập…' }: { text?: string }) {
  return <div className="ws-empty" role="status"><Mascot mood="thinking" /><p>{text}</p><span className="ws-dots" aria-hidden="true"><i /><i /><i /></span></div>;
}
export function Empty({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="ws-empty"><Mascot /><h3>{title}</h3><p>{children}</p></div>;
}
export function Modal({ open, onOpenChange, title, description, children }: { open: boolean; onOpenChange: (open: boolean) => void; title: string; description: string; children: ReactNode }) {
  return <Dialog.Root open={open} onOpenChange={onOpenChange}><Dialog.Portal><Dialog.Overlay className="ws-overlay" /><Dialog.Content className="workspace ws-modal"><Dialog.Title>{title}</Dialog.Title><Dialog.Description>{description}</Dialog.Description>{children}<Dialog.Close className="ws-icon ws-modal-close" aria-label="Đóng"><X size={18} /></Dialog.Close></Dialog.Content></Dialog.Portal></Dialog.Root>;
}
export class PanelBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() { return this.state.failed ? <div className="ws-empty" role="alert"><Mascot mood="sad" /><p>Góc học tập này gặp trục trặc.</p><button onClick={() => this.setState({ failed: false })}>Thử mở lại</button></div> : this.props.children; }
}
