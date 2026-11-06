import { useEffect, useRef, useState } from 'react';
import { X, Upload, Link as LinkIcon, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { axiosInstance } from '../../lib/axios';
import { teacherError } from '../admin/api/adminTeacherApi';
import { useClassMutation, type ClassItem } from './api';

interface ClassEditModalProps {
  initial: ClassItem;
  onClose: () => void;
  onSuccess: () => void;
}

const localDate = (value?: string | null) => {
  if (!value) return '';
  const d = new Date(value.endsWith('Z') ? value : value + 'Z');
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
};

export function ClassEditModal({ initial, onClose, onSuccess }: ClassEditModalProps) {
  const mutation = useClassMutation();

  const [name, setName] = useState(initial.name ?? '');
  const [description, setDescription] = useState(initial.description ?? '');
  const [thumbnail, setThumbnail] = useState(initial.thumbnail ?? '');
  const [status, setStatus] = useState<ClassItem['status']>(initial.status ?? 'Draft');
  const [accessType, setAccessType] = useState<ClassItem['accessType']>(initial.accessType ?? 'Public');
  const [maxStudents, setMaxStudents] = useState<string>(initial.maxStudents ? String(initial.maxStudents) : '');
  const [startDate, setStartDate] = useState(localDate(initial.startDate));
  const [endDate, setEndDate] = useState(localDate(initial.endDate));
  const [joinCodeExpiresAt, setJoinCodeExpiresAt] = useState(localDate(initial.joinCodeExpiresAt));
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const [imageMode, setImageMode] = useState<'file' | 'url'>('file');
  const [uploading, setUploading] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const isPickerActiveRef = useRef(false);
  const overlayRef = useRef<HTMLDivElement>(null);
  const mouseDownTargetRef = useRef<EventTarget | null>(null);

  useEffect(() => {
    const input = fileInputRef.current;
    if (!input) return;
    const handleCancel = (e: Event) => {
      e.stopPropagation();
      setTimeout(() => {
        isPickerActiveRef.current = false;
      }, 200);
    };
    input.addEventListener('cancel', handleCancel);
    return () => {
      input.removeEventListener('cancel', handleCancel);
    };
  }, []);

  const openFilePicker = () => {
    if (uploading || mutation.isPending) return;
    isPickerActiveRef.current = true;

    const onWindowFocus = () => {
      window.removeEventListener('focus', onWindowFocus);
      setTimeout(() => {
        isPickerActiveRef.current = false;
      }, 400);
    };

    window.addEventListener('focus', onWindowFocus);
    fileInputRef.current?.click();
  };

  const uploadImage = async (file?: File) => {
    if (!file) return;
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      toast.error('Chọn ảnh PNG, JPEG hoặc WebP không quá 5 MB.');
      return;
    }
    setUploading(true);
    try {
      const { data } = await axiosInstance.post<{ url: string }>(
        '/admin/classes/thumbnail',
        file,
        { headers: { 'Content-Type': file.type } }
      );
      setThumbnail(data.url);
      toast.success('Đã tải ảnh lên thành công');
    } catch (error) {
      toast.error(teacherError(error));
    } finally {
      setUploading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || name.trim().length < 3) {
      setErrorMsg('Tên lớp học phải có ít nhất 3 ký tự.');
      return;
    }
    if (startDate && endDate && new Date(endDate) <= new Date(startDate)) {
      setErrorMsg('Ngày kết thúc phải diễn ra sau ngày bắt đầu.');
      return;
    }
    setErrorMsg(null);

    mutation.mutate(
      {
        path: `/admin/classes/${initial.id}`,
        method: 'put',
        data: {
          name: name.trim(),
          description: description.trim(),
          thumbnail: thumbnail.trim() || null,
          status,
          accessType,
          maxStudents: maxStudents.trim() ? Number(maxStudents) : null,
          startDate: startDate ? new Date(startDate).toISOString() : null,
          endDate: endDate ? new Date(endDate).toISOString() : null,
          joinCodeExpiresAt: accessType === 'CodeRequired' && joinCodeExpiresAt ? new Date(joinCodeExpiresAt).toISOString() : null,
          teacherIds: (initial.teachers ?? []).map(t => t.id),
          rowVersion: initial.rowVersion,
        },
      },
      {
        onSuccess: () => {
          toast.success('Đã lưu thông tin lớp học thành công');
          onSuccess();
          onClose();
        },
      }
    );
  };

  const handleOverlayMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    mouseDownTargetRef.current = e.target;
  };

  const handleOverlayClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (mutation.isPending || uploading || isPickerActiveRef.current) return;
    if (e.target === overlayRef.current && mouseDownTargetRef.current === overlayRef.current) {
      onClose();
    }
  };

  return (
    <div
      ref={overlayRef}
      onMouseDown={handleOverlayMouseDown}
      onClick={handleOverlayClick}
      className="fixed inset-0 z-50 bg-black/45 backdrop-blur-[2px] flex items-center justify-center p-3 sm:p-4"
    >
      {/* Hình chữ nhật phẳng, không bo góc, không đổ bóng, phân tầng chiều sâu */}
      <div className="bg-white border border-slate-300 rounded-none w-[min(780px,calc(100vw-24px))] max-h-[92vh] flex flex-col shadow-none overflow-hidden font-sans text-slate-800">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <div>
            <h2 className="text-base font-bold text-slate-900">
              Chỉnh sửa thông tin lớp học
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Cập nhật tên lớp, ảnh đại diện, quyền truy cập, sĩ số và thời gian đào tạo
            </p>
          </div>
          <button
            type="button"
            aria-label="Đóng popup"
            disabled={mutation.isPending || uploading}
            onClick={onClose}
            className="w-8 h-8 rounded-none flex items-center justify-center text-slate-500 hover:text-slate-800 hover:bg-slate-200/60 border border-slate-300 bg-white transition-colors cursor-pointer"
          >
            <X size={16} />
          </button>
        </div>

        {/* Scrollable Form Body */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-5">
          {errorMsg && (
            <div className="p-3 border border-rose-200 bg-rose-50 text-rose-700 text-xs rounded-none">
              {errorMsg}
            </div>
          )}

          {/* Section: Thông tin chung & Thời gian */}
          <div className="border border-slate-200 bg-white rounded-none">
            <div className="px-4 py-3 border-b border-slate-200 bg-slate-50/80 border-l-3 border-l-blue-600">
              <h3 className="text-xs font-bold text-slate-800">
                1. Thông tin chung & Thời gian đào tạo
              </h3>
            </div>
            <div className="p-4 space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label htmlFor="edit-name" className="text-xs font-semibold text-slate-700 block mb-1">
                    Tên lớp học *
                  </label>
                  <input
                    id="edit-name"
                    type="text"
                    value={name}
                    onChange={e => setName(e.target.value)}
                    placeholder="Ví dụ: Lập trình Java cơ bản"
                    className="w-full border border-slate-300 rounded-none px-3 py-2 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                    required
                  />
                </div>

                <div>
                  <label htmlFor="edit-status" className="text-xs font-semibold text-slate-700 block mb-1">
                    Trạng thái hoạt động
                  </label>
                  <select
                    id="edit-status"
                    value={status}
                    onChange={e => setStatus(e.target.value as typeof status)}
                    className="w-full border border-slate-300 rounded-none px-3 py-2 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                  >
                    <option value="Draft">Draft (Bản nháp)</option>
                    <option value="Active">Active (Đang hoạt động)</option>
                    <option value="Archived">Archived (Đã lưu trữ)</option>
                  </select>
                </div>

                <div>
                  <label htmlFor="edit-access" className="text-xs font-semibold text-slate-700 block mb-1">
                    Quyền tham gia
                  </label>
                  <select
                    id="edit-access"
                    value={accessType}
                    onChange={e => setAccessType(e.target.value as typeof accessType)}
                    className="w-full border border-slate-300 rounded-none px-3 py-2 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                  >
                    <option value="Public">Công khai (Sinh viên tự do vào lớp)</option>
                    <option value="CodeRequired">Cần mã lớp (Yêu cầu nhập mã)</option>
                  </select>
                </div>

                <div>
                  <label htmlFor="edit-max" className="text-xs font-semibold text-slate-700 block mb-1">
                    Sĩ số tối đa (để trống nếu không giới hạn)
                  </label>
                  <input
                    id="edit-max"
                    type="number"
                    min="1"
                    value={maxStudents}
                    onChange={e => setMaxStudents(e.target.value)}
                    placeholder="Không giới hạn"
                    className="w-full border border-slate-300 rounded-none px-3 py-2 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                  />
                </div>

                <div>
                  <label htmlFor="edit-start" className="text-xs font-semibold text-slate-700 block mb-1">
                    Thời gian bắt đầu
                  </label>
                  <input
                    id="edit-start"
                    type="datetime-local"
                    value={startDate}
                    onChange={e => setStartDate(e.target.value)}
                    className="w-full border border-slate-300 rounded-none px-3 py-2 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                  />
                </div>

                <div>
                  <label htmlFor="edit-end" className="text-xs font-semibold text-slate-700 block mb-1">
                    Thời gian kết thúc
                  </label>
                  <input
                    id="edit-end"
                    type="datetime-local"
                    value={endDate}
                    onChange={e => setEndDate(e.target.value)}
                    className="w-full border border-slate-300 rounded-none px-3 py-2 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                  />
                </div>

                {accessType === 'CodeRequired' && (
                  <div className="md:col-span-2">
                    <label htmlFor="edit-expiry" className="text-xs font-semibold text-slate-700 block mb-1">
                      Hạn sử dụng mã tham gia (để trống nếu không hết hạn)
                    </label>
                    <input
                      id="edit-expiry"
                      type="datetime-local"
                      value={joinCodeExpiresAt}
                      onChange={e => setJoinCodeExpiresAt(e.target.value)}
                      className="w-full border border-slate-300 rounded-none px-3 py-2 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                    />
                  </div>
                )}
              </div>

              <div>
                <label htmlFor="edit-desc" className="text-xs font-semibold text-slate-700 block mb-1">
                  Mô tả chi tiết lớp học
                </label>
                <textarea
                  id="edit-desc"
                  rows={3}
                  value={description}
                  onChange={e => setDescription(e.target.value)}
                  placeholder="Nội dung, mục tiêu hoặc ghi chú của lớp..."
                  className="w-full border border-slate-300 rounded-none p-3 text-xs bg-white text-slate-900 focus:outline-none focus:border-blue-500"
                />
              </div>
            </div>
          </div>

          {/* Section: Ảnh đại diện lớp học */}
          <div className="border border-slate-200 bg-white rounded-none">
            <div className="px-4 py-3 border-b border-slate-200 bg-slate-50/80 border-l-3 border-l-blue-600 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
              <div>
                <h3 className="text-xs font-bold text-slate-800">
                  2. Ảnh đại diện lớp học
                </h3>
                <p className="text-[11px] text-slate-500 mt-0.5">
                  Hình ảnh hiển thị đại diện trên danh mục và các trang học tập
                </p>
              </div>

              {/* Segmented Switcher */}
              <div className="flex items-center border border-slate-300 bg-white rounded-none shrink-0">
                <button
                  type="button"
                  onClick={() => setImageMode('file')}
                  className={`flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold rounded-none cursor-pointer border-r border-slate-300 transition-colors ${imageMode === 'file'
                      ? 'bg-slate-900 text-white'
                      : 'bg-white text-slate-600 hover:bg-slate-50'
                    }`}
                >
                  <Upload size={12} />
                  <span>Tải ảnh từ máy</span>
                </button>
                <button
                  type="button"
                  onClick={() => setImageMode('url')}
                  className={`flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold rounded-none cursor-pointer transition-colors ${imageMode === 'url'
                      ? 'bg-slate-900 text-white'
                      : 'bg-white text-slate-600 hover:bg-slate-50'
                    }`}
                >
                  <LinkIcon size={12} />
                  <span>Đường dẫn URL</span>
                </button>
              </div>
            </div>

            <div className="p-4 space-y-3">
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

              {/* Active Image Card: Khi đã có ảnh thì hiển thị duy nhất thẻ này, các nút đặt hàng ngang rõ ràng */}
              {thumbnail ? (
                <div
                  className={`flex flex-col sm:flex-row sm:items-center gap-4 p-4 border border-slate-300 bg-white rounded-none transition-colors ${isDragging ? 'border-blue-500 bg-blue-50/40' : ''
                    }`}
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
                  <div className="w-36 h-20 border border-slate-300 bg-slate-50 rounded-none overflow-hidden shrink-0 flex items-center justify-center">
                    <img src={thumbnail} alt="Ảnh đại diện lớp" className="w-full h-full object-contain bg-white rounded-none" />
                  </div>
                  <div className="min-w-0 flex-1 space-y-2.5">
                    <div>
                      <span className="inline-block px-2 py-0.5 text-[10px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200 rounded-none">
                        Đã chọn ảnh đại diện
                      </span>
                      <p className="text-xs text-slate-500 mt-1">
                        Ảnh đã tải lên thành công
                      </p>
                    </div>
                    <div className="flex items-center gap-2.5 flex-wrap">
                      {imageMode === 'file' && (
                        <button
                          type="button"
                          onClick={openFilePicker}
                          disabled={uploading}
                          className="inline-flex items-center gap-1.5 h-7 px-3 text-xs font-semibold rounded-none border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 hover:border-slate-400 transition-colors cursor-pointer shadow-none"
                        >
                          <Upload size={12} />
                          <span>{uploading ? 'Đang tải…' : 'Đổi ảnh khác'}</span>
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => setThumbnail('')}
                        className="inline-flex items-center gap-1.5 h-7 px-3 text-xs font-semibold rounded-none border border-rose-300 bg-rose-50 text-rose-700 hover:bg-rose-100/70 transition-colors cursor-pointer shadow-none"
                      >
                        <Trash2 size={12} />
                        <span>Gỡ ảnh đại diện</span>
                      </button>
                    </div>
                  </div>
                </div>
              ) : imageMode === 'file' ? (
                /* Khi chưa có ảnh và ở chế độ file: Hiển thị duy nhất Dropzone */
                <div
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
                  className={`border-1.5 border-dashed rounded-none text-center cursor-pointer transition-colors select-none p-5 ${isDragging
                      ? 'border-blue-500 bg-blue-50/60'
                      : 'border-slate-300 hover:border-blue-500 bg-slate-50/40 hover:bg-slate-50'
                    }`}
                >
                  <div className="flex flex-col items-center gap-2">
                    <div className="w-8 h-8 grid place-items-center border border-slate-300 bg-white text-slate-500 rounded-none">
                      <Upload size={16} />
                    </div>
                    <div>
                      <span className="block text-xs font-semibold text-slate-800">
                        {uploading
                          ? 'Đang tải ảnh lên máy chủ…'
                          : 'Nhấp hoặc kéo thả tệp hình ảnh vào đây'}
                      </span>
                      <span className="block text-[11px] text-slate-400 mt-0.5">
                        Định dạng hỗ trợ: PNG, JPEG, WebP · Dung lượng tối đa 5 MB · Tỉ lệ đề xuất 16:9
                      </span>
                    </div>
                  </div>
                </div>
              ) : null}

              {/* Khi ở chế độ URL: Hiển thị ô nhập URL */}
              {imageMode === 'url' && (
                <div className="space-y-1">
                  <label htmlFor="edit-thumbnail-url" className="text-xs font-semibold text-slate-700 block">
                    Đường dẫn URL ảnh trực tiếp
                  </label>
                  <div className="flex items-center border border-slate-300 bg-white rounded-none px-3 py-1.5 focus-within:border-blue-500">
                    <LinkIcon size={14} className="text-slate-400 shrink-0 mr-2" />
                    <input
                      id="edit-thumbnail-url"
                      type="url"
                      value={thumbnail}
                      onChange={e => setThumbnail(e.target.value)}
                      placeholder="https://example.com/thumbnail.png"
                      className="w-full text-xs bg-transparent text-slate-900 focus:outline-none"
                    />
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Footer inside form to trigger submit */}
          <div className="pt-4 border-t border-slate-200 flex items-center justify-between">
            <span className="text-xs text-slate-400">* Thông tin bắt buộc</span>
            <div className="flex items-center gap-2.5">
              <button
                type="button"
                disabled={mutation.isPending || uploading}
                onClick={onClose}
                className="px-4 py-2 text-xs font-semibold rounded-none border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 transition-colors cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={mutation.isPending || uploading}
                className="px-5 py-2 text-xs font-semibold rounded-none bg-blue-600 hover:bg-blue-700 text-white transition-colors cursor-pointer shadow-none disabled:opacity-50"
              >
                {mutation.isPending ? 'Đang lưu…' : uploading ? 'Đang tải ảnh…' : 'Lưu thay đổi'}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
