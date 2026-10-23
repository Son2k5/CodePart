import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';
import { X } from 'lucide-react';
import { axiosInstance } from '../../lib/axios';
import { teacherError } from '../admin/api/adminTeacherApi';
import './class-management.css';
import { useClassesQuery, useClassMutation, type ClassItem, type Teacher } from './api';
import { QueryState, TeacherPicker } from './ClassUi';
import './classes.css';

const schema = z.object({ name: z.string().trim().min(3, 'Tên cần ít nhất 3 ký tự').max(100), description: z.string().max(5000),
  thumbnail: z.string().refine(x => !x || /^\/api\/class-images\/[a-f0-9]{32}\.(png|jpg|webp)$/.test(x) || /^https?:\/\//.test(x) && URL.canParse(x), 'URL ảnh không hợp lệ'),
  status: z.enum(['Draft', 'Active', 'Archived']), accessType: z.enum(['Public', 'CodeRequired']),
  maxStudents: z.string().refine(x => !x || /^\d+$/.test(x) && Number(x) > 0 && Number(x) <= 2147483647, 'Giới hạn phải là số nguyên dương'),
  startDate: z.string(), endDate: z.string(), joinCodeExpiresAt: z.string(),
}).refine(x => !x.startDate || !x.endDate || new Date(x.endDate) > new Date(x.startDate), { path: ['endDate'], message: 'Ngày kết thúc phải sau ngày bắt đầu' })
  .refine(x => x.accessType !== 'CodeRequired' || !x.joinCodeExpiresAt || new Date(x.joinCodeExpiresAt) > new Date(), { path: ['joinCodeExpiresAt'], message: 'Hạn dùng mã phải ở tương lai' });
type Values = z.infer<typeof schema>;
const localDate = (value?: string | null) => { if (!value) return ''; const d = new Date(value.endsWith('Z') ? value : value + 'Z'); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };
const utcDate = (value: string) => value ? new Date(value).toISOString() : null;

function ClassForm({ initial, onSaved, onCancel, onBusy }: { initial?: ClassItem; onSaved?: () => void; onCancel?: () => void; onBusy?: (busy: boolean) => void }) {
  const navigate = useNavigate(); const mutation = useClassMutation(); const [teachers, setTeachers] = useState<Teacher[]>(initial?.teachers ?? []);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: {
    name: initial?.name ?? '', description: initial?.description ?? '', thumbnail: initial?.thumbnail ?? '',
    status: initial?.status ?? 'Draft', accessType: initial?.accessType ?? 'Public', maxStudents: initial?.maxStudents?.toString() ?? '',
    startDate: localDate(initial?.startDate), endDate: localDate(initial?.endDate), joinCodeExpiresAt: localDate(initial?.joinCodeExpiresAt),
  } });
  const accessType = useWatch({ control: form.control, name: 'accessType' });
  const thumbnail = useWatch({ control: form.control, name: 'thumbnail' });
  const [imageMode, setImageMode] = useState<'url' | 'file'>('url');
  const [uploading, setUploading] = useState(false);
  const uploadImage = async (file?: File) => {
    if (!file) return;
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      toast.error('Chọn ảnh PNG, JPEG hoặc WebP không quá 5 MB.'); return;
    }
    setUploading(true); onBusy?.(true);
    try {
      const { data } = await axiosInstance.post<{ url: string }>('/admin/classes/thumbnail', file, { headers: { 'Content-Type': file.type } });
      form.setValue('thumbnail', data.url, { shouldValidate: true, shouldDirty: true });
      toast.success('Đã tải ảnh lên');
    } catch (error) { toast.error(teacherError(error)); }
    finally { setUploading(false); onBusy?.(false); }
  };
  const submit = form.handleSubmit(async values => {
    onBusy?.(true);
    try {
      const result = await mutation.mutateAsync({ path: '/admin/classes' + (initial ? `/${initial.id}` : ''), method: initial ? 'put' : 'post', data: {
        ...values, thumbnail: values.thumbnail || null, maxStudents: values.maxStudents ? Number(values.maxStudents) : null,
        startDate: utcDate(values.startDate), endDate: utcDate(values.endDate),
        joinCodeExpiresAt: values.accessType === 'CodeRequired' ? utcDate(values.joinCodeExpiresAt) : null,
        teacherIds: teachers.map(t => t.id), rowVersion: initial?.rowVersion ?? null,
      } });
      if (onSaved) onSaved();
      else navigate(`/admin/classes/${initial?.id ?? result.id}`);
    } catch { /* Keep edits on server error; mutation displays the message. */ }
    finally { onBusy?.(false); }
  });
  return <form className="class-editor" onSubmit={submit}><fieldset disabled={mutation.isPending || uploading}><div className="class-form-section"><h2>Thông tin lớp học</h2><p>Đặt tên và thiết lập cách sinh viên tham gia lớp.</p></div><div className="class-grid">
    <label>Tên lớp học *<input placeholder="Ví dụ: Lập trình căn bản — K24" autoFocus {...form.register('name')} /></label>
    <label>Trạng thái<select {...form.register('status')}><option value="Draft">Bản nháp</option><option value="Active">Đang hoạt động</option><option value="Archived">Đã lưu trữ</option></select></label>
    <label>Quyền tham gia<select {...form.register('accessType')}><option value="Public">Công khai</option><option value="CodeRequired">Cần mã lớp</option></select></label>
    <label>Sĩ số tối đa<input placeholder="Không giới hạn" inputMode="numeric" {...form.register('maxStudents')} /></label>
    <label>Ngày bắt đầu<input type="datetime-local" {...form.register('startDate')} /></label>
    <label>Ngày kết thúc<input type="datetime-local" {...form.register('endDate')} /></label>
    <section className="class-image-field" aria-label="Ảnh đại diện lớp"><strong>Ảnh đại diện</strong><div className="class-image-modes"><button type="button" aria-pressed={imageMode === 'url'} onClick={() => setImageMode('url')}>Đường dẫn ảnh</button><button type="button" aria-pressed={imageMode === 'file'} onClick={() => setImageMode('file')}>Tải ảnh từ máy</button></div>
      {imageMode === 'url' ? <label>Đường dẫn ảnh<input placeholder="https://..." {...form.register('thumbnail')} /></label> : <label className="class-upload-zone">{uploading ? 'Đang tải ảnh lên…' : 'Chọn ảnh từ thiết bị'}<input type="file" accept="image/png,image/jpeg,image/webp" onChange={e => { void uploadImage(e.target.files?.[0]); e.target.value = ''; }} /><small>PNG, JPEG, WebP · Tối đa 5 MB</small></label>}
      {thumbnail && <div className="class-image-preview"><img src={thumbnail} alt="Ảnh đại diện lớp" /><button type="button" onClick={() => form.setValue('thumbnail', '', { shouldDirty: true, shouldValidate: true })}>Gỡ ảnh</button></div>}
    </section>
    {accessType === 'CodeRequired' && <label>Hạn dùng mã (để trống: không hết hạn)<input type="datetime-local" {...form.register('joinCodeExpiresAt')} />
      {initial?.joinCode && <button type="button" onClick={() => { void navigator.clipboard.writeText(initial.joinCode!).then(() => toast.success('Đã sao chép mã'), () => toast.error('Không thể sao chép mã')); }}>Sao chép {initial.joinCode}</button>}
    </label>}
  </div><label className="class-description">Mô tả lớp học<textarea placeholder="Nội dung, mục tiêu và lưu ý dành cho sinh viên..." rows={3} {...form.register('description')} /></label>
    <TeacherPicker value={teachers} onChange={setTeachers} />
    {Object.entries(form.formState.errors).map(([field, error]) => <p className="class-error" role="alert" key={field}>{error.message}</p>)}
    <div className="class-form-footer"><span>* Thông tin bắt buộc</span><button type="button" onClick={() => onCancel ? onCancel() : navigate('/admin/classes')}>Hủy</button><button className="primary" type="submit">{mutation.isPending ? 'Đang lưu…' : initial ? 'Lưu thay đổi' : 'Tạo lớp học'}</button></div>
  </fieldset></form>;
}
export function ClassFormPage() {
  const { id } = useParams(); const query = useClassesQuery<ClassItem>(`/admin/classes/${id}`, undefined, !!id);
  return <main className="classes-page"><Link to="/admin/classes">← Danh sách lớp</Link><h1>{id ? 'Sửa lớp học' : 'Tạo lớp học'}</h1>
    {id ? <><QueryState loading={query.isLoading} error={query.error} retry={() => query.refetch()} />{query.data && <ClassForm key={id} initial={query.data} />}</> : <ClassForm />}
  </main>;
}

export function ClassFormModal({ initial, onClose }: { initial?: ClassItem; onClose: () => void }) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const busyRef = useRef(false);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const dialog = dialogRef.current;
    const previous = document.activeElement as HTMLElement | null;
    const overflow = document.body.style.overflow;
    dialog?.showModal();
    document.body.style.overflow = 'hidden';
    return () => { dialog?.close(); document.body.style.overflow = overflow; previous?.focus(); };
  }, []);
  return <dialog ref={dialogRef} className="class-form-modal classes-page" aria-labelledby="class-form-title" onCancel={e => { e.preventDefault(); if (!busyRef.current) onClose(); }}>
    <header className="class-modal-heading"><div><h1 id="class-form-title">{initial ? 'Chỉnh sửa lớp học' : 'Thêm lớp học'}</h1><p>{initial ? 'Cập nhật thông tin và phân công giáo viên.' : 'Tạo lớp mới, thiết lập lịch học và phân công giáo viên.'}</p></div><button type="button" aria-label="Đóng biểu mẫu" disabled={busy} onClick={onClose}><X size={20} /></button></header>
    <ClassForm initial={initial} onSaved={onClose} onCancel={onClose} onBusy={value => { busyRef.current = value; setBusy(value); }} />
  </dialog>;
}

