import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Camera, Loader2, Upload, X } from 'lucide-react';
import { toast } from 'sonner';
import { validateAvatarFile } from '../schemas/profileSchemas';

interface AvatarUploaderProps {
  avatarUrl: string | null;
  fullName: string;
  busy: boolean;
  progress: number | null;
  onUpload: (blob: Blob) => void;
}

const OUT_SIZE = 256;

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  return (parts[parts.length - 1][0] ?? '?').toLocaleUpperCase('vi');
}

/** Xem trước + crop vuông bằng canvas, rồi giao blob cho parent upload. */
export function AvatarUploader({ avatarUrl, fullName, busy, progress, onUpload }: AvatarUploaderProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [draftUrl, setDraftUrl] = useState<string | null>(null);
  const [cropSrc, setCropSrc] = useState<string | null>(null);

  const [scale, setScale] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const dragging = useRef(false);
  const last = useRef({ x: 0, y: 0 });

  const openPicker = () => {
    if (busy) return;
    inputRef.current?.click();
  };

  const onFile = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    const error = validateAvatarFile(file);
    if (error) {
      toast.error(error);
      return;
    }
    const url = URL.createObjectURL(file);
    setCropSrc(url);
    setScale(1);
    setOffset({ x: 0, y: 0 });
  };

  // Ảnh xem tạm khi đang chờ xác nhận crop.
  useEffect(() => {
    if (!draftUrl) return;
    return () => URL.revokeObjectURL(draftUrl);
  }, [draftUrl]);

  useEffect(() => {
    return () => {
      if (cropSrc) URL.revokeObjectURL(cropSrc);
    };
  }, [cropSrc]);

  const onCropImgLoad = useCallback((e: React.SyntheticEvent<HTMLImageElement>) => {
    const box = e.currentTarget.parentElement?.getBoundingClientRect();
    if (!box) return;
    const min = box.width / e.currentTarget.naturalWidth;
    setScale(Math.max(1, min));
  }, []);

  const onPointerDown = (e: React.PointerEvent) => {
    dragging.current = true;
    last.current = { x: e.clientX, y: e.clientY };
    (e.target as Element).setPointerCapture?.(e.pointerId);
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (!dragging.current) return;
    setOffset((prev) => ({ x: prev.x + (e.clientX - last.current.x), y: prev.y + (e.clientY - last.current.y) }));
    last.current = { x: e.clientX, y: e.clientY };
  };
  const onPointerUp = () => {
    dragging.current = false;
  };

  const applyCrop = () => {
    if (!cropSrc) return;
    const image = new Image();
    image.onload = () => {
      const frame = OUT_SIZE;
      // Ảnh nguồn hiển thị trong khung vuông; scale/offset tính theo khung 256.
      const displayW = image.naturalWidth * scale;
      const displayH = image.naturalHeight * scale;
      const ratio = frame / displayW; // khung vuông 256 ~ frame preview
      const canvas = document.createElement('canvas');
      canvas.width = frame;
      canvas.height = frame;
      const ctx = canvas.getContext('2d');
      if (!ctx) {
        toast.error('Không thể xử lý ảnh.');
        return;
      }
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, frame, frame);
      const drawW = displayW * ratio;
      const drawH = displayH * ratio;
      const dx = offset.x * ratio + (frame - drawW) / 2;
      const dy = offset.y * ratio + (frame - drawH) / 2;
      ctx.drawImage(image, dx, dy, drawW, drawH);
      canvas.toBlob((blob) => {
        if (!blob) {
          toast.error('Không thể tạo ảnh.');
          return;
        }
        const preview = URL.createObjectURL(blob);
        setDraftUrl(preview);
        setCropSrc(null);
      }, 'image/jpeg', 0.9);
    };
    image.src = cropSrc;
  };

  const confirm = () => {
    if (!draftUrl) return;
    fetch(draftUrl)
      .then((r) => r.blob())
      .then((blob) => {
        onUpload(blob);
        setDraftUrl(null);
      })
      .catch(() => toast.error('Không đọc được ảnh xem trước.'));
  };

  const uploading = busy && progress !== null;

  return (
    <div className="relative mx-auto h-32 w-32">
      <div className="h-full w-full overflow-hidden rounded-full border-4 border-white bg-[#edf9fa] shadow-md ring-1 ring-[#dfecee]">
        {draftUrl || avatarUrl ? (
          <img src={draftUrl ?? avatarUrl!} alt={fullName} className="h-full w-full object-cover" />
        ) : (
          <div className="grid h-full w-full place-items-center bg-[linear-gradient(135deg,#bfe9f2,#d7f0d8)] text-4xl font-bold text-[#3e656b]">
            {initials(fullName)}
          </div>
        )}
      </div>

      {uploading && (
        <div className="absolute inset-0 grid place-items-center rounded-full bg-white/70">
          <div className="h-20 w-20 rounded-full" style={{ background: `conic-gradient(#397c8c ${progress! * 360}deg, #dfecee 0)` }}>
            <div className="grid h-full w-full place-items-center">
              <span className="grid h-14 w-14 place-items-center rounded-full bg-white text-xs font-semibold text-[#397c8c]">
                {Math.round(progress! * 100)}%
              </span>
            </div>
          </div>
        </div>
      )}

      <button
        type="button"
        onClick={openPicker}
        disabled={busy}
        aria-label="Chọn ảnh đại diện"
        className="absolute bottom-1 right-1 grid h-9 w-9 place-items-center rounded-full border-2 border-white bg-[#397c8c] text-white shadow-md transition-transform hover:scale-105 disabled:opacity-60"
      >
        <Camera className="h-4 w-4" />
      </button>

      <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={onFile} />

      {cropSrc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-3xl bg-white p-5 shadow-2xl">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="text-base font-semibold text-[#3e656b]">Cắt ảnh vuông</h3>
              <button type="button" onClick={() => setCropSrc(null)} className="grid h-8 w-8 place-items-center rounded-full hover:bg-slate-100">
                <X className="h-4 w-4" />
              </button>
            </div>
            <div
              className="relative mx-auto h-56 w-56 touch-none select-none overflow-hidden rounded-2xl bg-slate-100"
              onPointerDown={onPointerDown}
              onPointerMove={onPointerMove}
              onPointerUp={onPointerUp}
              onPointerLeave={onPointerUp}
            >
              <img
                src={cropSrc}
                alt="preview"
                draggable={false}
                onLoad={onCropImgLoad}
                className="pointer-events-none absolute left-1/2 top-1/2 max-h-none max-w-none"
                style={{ width: 224 * scale, transform: `translate(calc(-50% + ${offset.x}px), calc(-50% + ${offset.y}px)) scale(1)` }}
              />
              <div className="pointer-events-none absolute inset-0 rounded-2xl ring-2 ring-white/70" />
            </div>
            <label className="mt-4 flex items-center gap-3 text-xs text-slate-500">
              <span className="w-10 shrink-0">Phóng to</span>
              <input type="range" min={0.2} max={4} step={0.01} value={scale} onChange={(e) => setScale(Number(e.target.value))} className="w-full accent-[#397c8c]" />
            </label>
            <div className="mt-4 flex justify-end gap-2">
              <button type="button" onClick={() => setCropSrc(null)} className="rounded-full border border-[#cddfe3] px-4 py-2 text-sm hover:bg-slate-50">
                Hủy
              </button>
              <button type="button" onClick={applyCrop} className="rounded-full bg-[#397955] px-4 py-2 text-sm font-semibold text-white hover:bg-[#2f6547]">
                Cắt ảnh
              </button>
            </div>
          </div>
        </div>
      )}

      {draftUrl && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-3xl bg-white p-5 shadow-2xl">
            <h3 className="mb-3 text-base font-semibold text-[#3e656b]">Xác nhận ảnh đại diện</h3>
            <img src={draftUrl} alt="đã cắt" className="mx-auto h-40 w-40 rounded-full object-cover ring-4 ring-[#edf9fa]" />
            <div className="mt-4 flex justify-end gap-2">
              <button type="button" disabled={busy} onClick={() => setDraftUrl(null)} className="rounded-full border border-[#cddfe3] px-4 py-2 text-sm hover:bg-slate-50 disabled:opacity-60">
                Chọn lại
              </button>
              <button type="button" disabled={busy} onClick={confirm} className="inline-flex items-center gap-1.5 rounded-full bg-[#397c8c] px-4 py-2 text-sm font-semibold text-white hover:bg-[#327080] disabled:opacity-60">
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
                {busy ? 'Đang tải…' : 'Tải lên'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
