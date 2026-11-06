import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';
import { X, Upload, Link as LinkIcon, Trash2 } from 'lucide-react';
import { axiosInstance } from '../../lib/axios';
import { teacherError } from '../admin/api/adminTeacherApi';
import './classes.css';
import './class-management.css';
import { useClassesQuery, useClassMutation, type ClassItem, type Teacher } from './api';
import { QueryState, TeacherPicker } from './ClassUi';

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

function ClassForm({
  initial,
  onSaved,
  onCancel,
  onBusy,
  onPickerActive,
}: {
  initial?: ClassItem;
  onSaved?: () => void;
  onCancel?: () => void;
  onBusy?: (busy: boolean) => void;
  onPickerActive?: (active: boolean) => void;
}) {
  const navigate = useNavigate();
  const mutation = useClassMutation();
  const [teachers, setTeachers] = useState<Teacher[]>(initial?.teachers ?? []);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: {
    name: initial?.name ?? '', description: initial?.description ?? '', thumbnail: initial?.thumbnail ?? '',
    status: initial?.status ?? 'Draft', accessType: initial?.accessType ?? 'Public', maxStudents: initial?.maxStudents?.toString() ?? '',
    startDate: localDate(initial?.startDate), endDate: localDate(initial?.endDate), joinCodeExpiresAt: localDate(initial?.joinCodeExpiresAt),
  } });
  const accessType = useWatch({ control: form.control, name: 'accessType' });
  const thumbnail = useWatch({ control: form.control, name: 'thumbnail' });
  const [imageMode, setImageMode] = useState<'url' | 'file'>('file');
  const [uploading, setUploading] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const input = fileInputRef.current;
    if (!input) return;
    const handleCancel = (e: Event) => {
      e.stopPropagation();
      setTimeout(() => onPickerActive?.(false), 200);
    };
    input.addEventListener('cancel', handleCancel);
    return () => {
      input.removeEventListener('cancel', handleCancel);
    };
  }, [onPickerActive]);

  const openFilePicker = () => {
    if (uploading || mutation.isPending) return;
    onPickerActive?.(true);

    const onWindowFocus = () => {
      window.removeEventListener('focus', onWindowFocus);
      // Wait for native events like cancel, change, and click to finish firing before resetting picker active state
      setTimeout(() => {
        onPickerActive?.(false);
      }, 400);
    };

    window.addEventListener('focus', onWindowFocus);
    fileInputRef.current?.click();
  };

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
  return (
    <form className="class-editor" onSubmit={submit}>
      <fieldset disabled={mutation.isPending || uploading}>
        {/* Section 1: Thông tin chung & Thời gian */}
        <div className="class-form-card">
          <div className="class-card-header">
            <div>
              <h3>1. Thông tin chung & Thời gian đào tạo</h3>
              <p>Thiết lập tên lớp, sĩ số, hình thức tham gia và thời gian hiệu lực.</p>
            </div>
          </div>
          <div className="class-card-body">
            <div className="class-grid">
              <label>
                Tên lớp học *
                <input placeholder="Ví dụ: Lập trình căn bản — K24" autoFocus {...form.register('name')} />
              </label>
              <label>
                Trạng thái lớp
                <select {...form.register('status')}>
                  <option value="Draft">Bản nháp</option>
                  <option value="Active">Đang hoạt động</option>
                  <option value="Archived">Đã lưu trữ</option>
                </select>
              </label>
              <label>
                Hình thức tham gia
                <select {...form.register('accessType')}>
                  <option value="Public">Công khai (Mọi người đều có thể vào)</option>
                  <option value="CodeRequired">Yêu cầu mã lớp (Cần mã mời)</option>
                </select>
              </label>
              <label>
                Sĩ số tối đa
                <input placeholder="Không giới hạn" inputMode="numeric" {...form.register('maxStudents')} />
              </label>
              <label>
                Ngày bắt đầu
                <input type="datetime-local" {...form.register('startDate')} />
              </label>
              <label>
                Ngày kết thúc
                <input type="datetime-local" {...form.register('endDate')} />
              </label>
              {accessType === 'CodeRequired' && (
                <label className="col-span-full">
                  Hạn dùng mã tham gia (để trống: không bao giờ hết hạn)
                  <div className="flex items-center gap-2 mt-1">
                    <input type="datetime-local" {...form.register('joinCodeExpiresAt')} className="flex-1" />
                    {initial?.joinCode && (
                      <button
                        type="button"
                        onClick={() => {
                          void navigator.clipboard.writeText(initial.joinCode!).then(
                            () => toast.success('Đã sao chép mã'),
                            () => toast.error('Không thể sao chép mã')
                          );
                        }}
                        className="class-btn-secondary shrink-0"
                      >
                        Sao chép {initial.joinCode}
                      </button>
                    )}
                  </div>
                </label>
              )}
            </div>
            <label className="class-description">
              Mô tả nội dung lớp học
              <textarea placeholder="Mục tiêu môn học, đề cương và những lưu ý cần thiết dành cho sinh viên..." rows={3} {...form.register('description')} />
            </label>
          </div>
        </div>

        {/* Section 2: Ảnh đại diện lớp học */}
        <div className="class-form-card">
          <div className="class-card-header flex-row items-center justify-between">
            <div>
              <h3>2. Ảnh đại diện lớp học</h3>
              <p>Hình ảnh hiển thị đại diện trên danh mục và trang học tập.</p>
            </div>
            <div className="class-image-modes">
              <button
                type="button"
                aria-pressed={imageMode === 'file'}
                onClick={() => setImageMode('file')}
              >
                <Upload size={13} />
                <span>Tải ảnh từ máy</span>
              </button>
              <button
                type="button"
                aria-pressed={imageMode === 'url'}
                onClick={() => setImageMode('url')}
              >
                <LinkIcon size={13} />
                <span>Đường dẫn URL</span>
              </button>
            </div>
          </div>
          <div className="class-card-body">
            <div className="class-upload-container">
              {/* Thẻ input chọn file ẩn độc lập cho mọi thao tác tải / đổi ảnh */}
              <input
                ref={fileInputRef}
                type="file"
                accept="image/png,image/jpeg,image/webp"
                style={{ display: 'none' }}
                tabIndex={-1}
                aria-hidden="true"
                onClick={e => e.stopPropagation()}
                onChange={e => {
                  e.stopPropagation();
                  const file = e.target.files?.[0];
                  e.target.value = '';
                  if (file) {
                    void uploadImage(file);
                  }
                }}
              />

              {thumbnail ? (
                /* Khi đã có ảnh: Hiển thị duy nhất thẻ Preview thông tin & 2 nút thao tác rõ ràng */
                <div
                  className={`class-image-active-card ${isDragging ? 'is-dragging' : ''}`}
                  onDragOver={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(true);
                  }}
                  onDragEnter={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(true);
                  }}
                  onDragLeave={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(false);
                  }}
                  onDrop={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(false);
                    const file = e.dataTransfer.files?.[0];
                    if (file) {
                      void uploadImage(file);
                    }
                  }}
                >
                  <div className="class-image-thumb-box">
                    <img src={thumbnail} alt="Ảnh đại diện lớp" />
                  </div>
                  <div className="class-image-details">
                    <div className="class-image-meta">
                      <span className="class-badge-active">Đã chọn ảnh đại diện</span>
                      <p className="class-image-desc-text">
                        Ảnh đã tải lên thành công · Tỉ lệ đề xuất 16:9
                      </p>
                    </div>
                    <div className="class-image-actions">
                      {imageMode === 'file' && (
                        <button
                          type="button"
                          onClick={openFilePicker}
                          disabled={uploading}
                          className="class-btn-change-image"
                        >
                          <Upload size={13} />
                          <span>{uploading ? 'Đang tải…' : 'Đổi ảnh khác'}</span>
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => form.setValue('thumbnail', '', { shouldDirty: true, shouldValidate: true })}
                        className="class-btn-remove-image"
                      >
                        <Trash2 size={13} />
                        <span>Gỡ ảnh đại diện</span>
                      </button>
                    </div>
                  </div>
                </div>
              ) : imageMode === 'file' ? (
                /* Khi chưa có ảnh và ở chế độ tải từ máy: Hiển thị duy nhất Dropzone */
                <div
                  className={`class-upload-dropzone ${isDragging ? 'is-dragging' : ''}`}
                  role="button"
                  tabIndex={0}
                  onClick={openFilePicker}
                  onKeyDown={e => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      openFilePicker();
                    }
                  }}
                  onDragOver={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(true);
                  }}
                  onDragEnter={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(true);
                  }}
                  onDragLeave={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(false);
                  }}
                  onDrop={e => {
                    e.preventDefault();
                    e.stopPropagation();
                    setIsDragging(false);
                    const file = e.dataTransfer.files?.[0];
                    if (file) {
                      void uploadImage(file);
                    }
                  }}
                >
                  <div className="class-upload-dropzone-content">
                    <div className="class-upload-icon-wrapper">
                      <Upload size={20} />
                    </div>
                    <div>
                      <strong className="class-upload-dropzone-title">
                        {uploading ? 'Đang tải ảnh lên máy chủ…' : 'Nhấp hoặc kéo thả tệp hình ảnh vào đây'}
                      </strong>
                      <span className="class-upload-dropzone-subtitle">
                        Định dạng hỗ trợ: PNG, JPEG, WebP · Dung lượng tối đa 5 MB · Tỉ lệ đề xuất 16:9
                      </span>
                    </div>
                  </div>
                </div>
              ) : null}

              {/* Khi ở chế độ URL: Hiển thị trường nhập URL trực tiếp */}
              {imageMode === 'url' && (
                <div className="class-url-input-zone">
                  <label>
                    <span>Đường dẫn URL hình ảnh trực tiếp</span>
                    <div className="class-url-input-wrap">
                      <LinkIcon size={14} className="text-slate-400 shrink-0" />
                      <input placeholder="https://example.com/thumbnail.png" {...form.register('thumbnail')} />
                    </div>
                  </label>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Section 3: Phân công giáo viên phụ trách */}
        <div className="class-form-card">
          <div className="class-card-header">
            <div>
              <h3>3. Phân công giáo viên phụ trách</h3>
              <p>Chọn các giáo viên phụ trách giảng dạy, ra đề và chấm bài (có thể điều chỉnh sau).</p>
            </div>
          </div>
          <div className="class-card-body p-0">
            <TeacherPicker value={teachers} onChange={setTeachers} />
          </div>
        </div>

        {Object.entries(form.formState.errors).map(([field, error]) => (
          <p className="class-error" role="alert" key={field}>{error.message}</p>
        ))}

        <div className="class-form-footer">
          <span>* Thông tin bắt buộc</span>
          <button type="button" onClick={() => onCancel ? onCancel() : navigate('/admin/classes')}>Hủy</button>
          <button className="primary" type="submit">
            {mutation.isPending ? 'Đang lưu…' : initial ? 'Lưu thay đổi' : 'Tạo lớp học'}
          </button>
        </div>
      </fieldset>
    </form>
  );
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
  const isPickerActiveRef = useRef(false);
  const [busy, setBusy] = useState(false);
  const mouseDownTargetRef = useRef<EventTarget | null>(null);
  const mouseDownInsideRef = useRef(false);

  useEffect(() => {
    const dialog = dialogRef.current;
    const previous = document.activeElement as HTMLElement | null;
    const overflow = document.body.style.overflow;
    dialog?.showModal();
    document.body.style.overflow = 'hidden';
    return () => {
      dialog?.close();
      document.body.style.overflow = overflow;
      previous?.focus();
    };
  }, []);

  const handleMouseDown = (e: React.MouseEvent<HTMLDialogElement>) => {
    mouseDownTargetRef.current = e.target;
    if (!dialogRef.current) return;
    const rect = dialogRef.current.getBoundingClientRect();
    const isInside = (
      e.clientX >= rect.left &&
      e.clientX <= rect.right &&
      e.clientY >= rect.top &&
      e.clientY <= rect.bottom
    );
    mouseDownInsideRef.current = isInside;
  };

  const handleClick = (e: React.MouseEvent<HTMLDialogElement>) => {
    // If form is busy or file picker is active/recently active, DO NOT close
    if (busyRef.current || isPickerActiveRef.current) return;

    // If mouse down started inside dialog contents or scrollbar, DO NOT close
    if (mouseDownInsideRef.current) return;

    // Only proceed if event target is the dialog backdrop
    if (e.target !== dialogRef.current) return;

    // Verify click coordinates are strictly outside the dialog box
    const rect = dialogRef.current.getBoundingClientRect();
    const isOutside = (
      e.clientX < rect.left ||
      e.clientX > rect.right ||
      e.clientY < rect.top ||
      e.clientY > rect.bottom
    );

    if (isOutside && mouseDownTargetRef.current === dialogRef.current) {
      onClose();
    }
  };

  const handleCancel = (e: React.SyntheticEvent<HTMLDialogElement, Event>) => {
    e.preventDefault();
    // Stop cancel events bubbling from internal elements like file input
    if (e.target !== dialogRef.current) {
      e.stopPropagation();
      return;
    }
    // Block dialog closing if form is busy or file picker is active/closing
    if (busyRef.current || isPickerActiveRef.current) {
      return;
    }
    onClose();
  };

  return (
    <dialog
      ref={dialogRef}
      className="class-form-modal classes-page"
      aria-labelledby="class-form-title"
      onCancel={handleCancel}
      onMouseDown={handleMouseDown}
      onClick={handleClick}
    >
      <header className="class-modal-heading">
        <div>
          <h1 id="class-form-title">{initial ? 'Chỉnh sửa lớp học' : 'Thêm lớp học'}</h1>
          <p>{initial ? 'Cập nhật thông tin và phân công giáo viên.' : 'Tạo lớp mới, thiết lập lịch học và phân công giáo viên.'}</p>
        </div>
        <button type="button" aria-label="Đóng biểu mẫu" disabled={busy} onClick={onClose}><X size={18} /></button>
      </header>
      <ClassForm
        initial={initial}
        onSaved={onClose}
        onCancel={onClose}
        onBusy={value => {
          busyRef.current = value;
          setBusy(value);
        }}
        onPickerActive={active => {
          isPickerActiveRef.current = active;
        }}
      />
    </dialog>
  );
}

