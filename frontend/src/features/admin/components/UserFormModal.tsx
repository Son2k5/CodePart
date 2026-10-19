import React, { useState } from 'react';
import { X, UserPlus, Save, GraduationCap, UserCheck } from 'lucide-react';
import type { AdminUserItem } from '../types/admin.types';
import type { Role, UserStatus } from '../../auth/types/auth.types';
import { teacherError, type Faculty, type TeacherFormData } from '../api/adminTeacherApi';
import { DEPARTMENTS } from '../data/mockAdminData';

interface UserFormModalProps {
  faculties?: Faculty[];
  fixedRole?: 'Student' | 'Teacher';
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (userData: TeacherFormData) => void | Promise<void>;
  initialData?: AdminUserItem | null;
}

const UserFormDialog: React.FC<Omit<UserFormModalProps, 'isOpen'>> = ({
  fixedRole,
  faculties,
  onClose,
  onSubmit,
  initialData,
}) => {
  const isEditing = Boolean(initialData);

  const [fullName, setFullName] = useState(initialData?.fullName || '');
  const [email, setEmail] = useState(initialData?.email || '');
  const [role, setRole] = useState<Role>(fixedRole || initialData?.role || 'Student');
  const [status, setStatus] = useState<UserStatus>(initialData?.status || 'Active');
  const [identifier, setIdentifier] = useState(
    initialData?.studentId || initialData?.teacherId || ''
  );
  const [department, setDepartment] = useState(faculties ? initialData?.facultyId ?? '' : initialData?.department || DEPARTMENTS[0]);
  const [phone, setPhone] = useState(initialData?.phone || '');
  const [bio, setBio] = useState(initialData?.bio || '');
  const [error, setError] = useState<string | null>(null);

  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (saving) return;
    setError(null);

    if (!fullName.trim()) {
      setError('Vui lòng nhập họ và tên.');
      return;
    }

    if (!email.trim() || !email.trim().toLowerCase().endsWith('@hanu.edu.vn')) {
      setError('Email trường bắt buộc phải có đuôi @hanu.edu.vn');
      return;
    }

    const payload: TeacherFormData = {
      fullName: fullName.trim(),
      email: email.trim().toLowerCase(),
      role,
      status,
      department: faculties ? faculties.find(f => f.id === department)?.name : department,
      facultyId: faculties ? department || null : undefined,
      temporaryPassword,
      phone: phone.trim(),
      bio: bio.trim(),
      ...(role === 'Student'
        ? { studentId: identifier.trim() || undefined, teacherId: undefined }
        : { teacherId: identifier.trim() || undefined, studentId: undefined }),
    };

    setSaving(true);
    try { await onSubmit(payload); onClose(); }
    catch (err) { setError(teacherError(err)); }
    finally { setSaving(false); }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-xs animate-in fade-in duration-150">
      <div className="relative w-full max-w-lg bg-white rounded-3xl shadow-2xl border border-slate-100 overflow-hidden flex flex-col max-h-[90vh]">
        {/* Modal Header */}
        <div className="px-6 py-5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-sky-100 text-sky-600 flex items-center justify-center font-bold">
              {isEditing ? <Save className="w-5 h-5" /> : <UserPlus className="w-5 h-5" />}
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">
                {isEditing ? 'Chỉnh sửa thông tin' : `Thêm ${role === 'Student' ? 'sinh viên' : 'giáo viên'} mới`}
              </h3>
              <p className="text-xs text-slate-500">
                Quản lí hồ sơ {role === 'Student' ? 'sinh viên' : 'giáo viên'} trong hệ thống
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Body */}
        <form onSubmit={handleSubmit} className="p-6 overflow-y-auto space-y-4 text-xs">
          {error && (
            <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 font-medium">
              {error}
            </div>
          )}

          {/* Full Name */}
          <div className="space-y-1">
            <label className="font-semibold text-slate-700">Họ và tên *</label>
            <input
              maxLength={200}
              type="text"
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="VD: TS. Nguyễn Văn Hùng hoặc Đỗ Minh Sơn"
              className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 text-xs"
            />
          </div>

          {/* Email */}
          <div className="space-y-1">
            <label className="font-semibold text-slate-700">Email trường (@hanu.edu.vn) *</label>
            <input
              type="email"
              required
              disabled={isEditing}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="VD: 2101040001@hanu.edu.vn hoặc tengv@hanu.edu.vn"
              className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-slate-50 disabled:bg-slate-100 disabled:cursor-not-allowed focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 text-xs font-mono"
            />
          </div>

          {faculties && !isEditing && <>
            <label className="block space-y-1">Mật khẩu tạm thời *
              <input type="password" autoComplete="new-password" required minLength={8} maxLength={128} value={temporaryPassword} onChange={e => setTemporaryPassword(e.target.value)} className="w-full p-3 border rounded-xl" />
              <small>8–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.</small>
            </label>
            <label className="block space-y-1">Trạng thái ban đầu
              <select value={status} onChange={e => setStatus(e.target.value as UserStatus)} className="w-full p-3 border rounded-xl">
                <option value="Active">Đang hoạt động</option><option value="Pending">Chờ phê duyệt</option>
              </select>
            </label>
          </>}
          {/* Role selection tabs */}
          {!fixedRole && <div className="space-y-1.5">
            <label className="font-semibold text-slate-700">Vai trò người dùng *</label>
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => setRole('Student')}
                className={`p-3 rounded-xl border flex items-center justify-center gap-2 cursor-pointer transition-all ${
                  role === 'Student'
                    ? 'border-sky-500 bg-sky-50/70 text-sky-800 font-semibold'
                    : 'border-slate-200 bg-slate-50/50 text-slate-600 hover:bg-slate-100'
                }`}
              >
                <GraduationCap className="w-4 h-4 text-sky-600" />
                <span>Sinh viên</span>
              </button>

              <button
                type="button"
                onClick={() => setRole('Teacher')}
                className={`p-3 rounded-xl border flex items-center justify-center gap-2 cursor-pointer transition-all ${
                  role === 'Teacher'
                    ? 'border-indigo-500 bg-indigo-50/70 text-indigo-800 font-semibold'
                    : 'border-slate-200 bg-slate-50/50 text-slate-600 hover:bg-slate-100'
                }`}
              >
                <UserCheck className="w-4 h-4 text-indigo-600" />
                <span>Giáo viên</span>
              </button>
            </div>
          </div>

          }
          {/* Identifier (StudentId or TeacherId) */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <label className="font-semibold text-slate-700">
                {role === 'Student' ? 'Mã sinh viên (MSSV)' : 'Mã định danh GV'}
              </label>
              <input
                type="text"
                maxLength={50}
              value={identifier}
                onChange={(e) => setIdentifier(e.target.value)}
                placeholder={role === 'Student' ? 'VD: 2101040001' : 'VD: GV-CNTT-082'}
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 text-xs font-mono"
              />
            </div>

            {/* Status */}
            {!faculties && <div className="space-y-1">
              <label className="font-semibold text-slate-700">Trạng thái *</label>
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value as UserStatus)}
                className="w-full px-3 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 text-xs"
              >
                <option value="Active">Hoạt động (Active)</option>
                <option value="Pending">Chờ duyệt (Pending)</option>
                <option value="Disabled">Đã khóa (Disabled)</option>
                <option value="Rejected">Đã từ chối (Rejected)</option>
              </select>
            </div>}
          </div>

          {/* Department */}
          <div className="space-y-1">
            <label className="font-semibold text-slate-700">Khoa / Đơn vị phụ trách</label>
            <select
              value={department}
              onChange={(e) => setDepartment(e.target.value)}
              className="w-full px-3 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 text-xs"
            >
              {faculties && <option value="">Chưa phân khoa</option>}
              {faculties ? faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>) : DEPARTMENTS.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </div>

          {/* Phone */}
          <div className="space-y-1">
            <label className="font-semibold text-slate-700">Số điện thoại liên hệ</label>
            <input
              type="text"
              maxLength={20}
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="VD: 0981234567"
              className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 text-xs"
            />
          </div>

          {/* Bio */}
          <div className="space-y-1">
            <label className="font-semibold text-slate-700">Ghi chú / Giới thiệu</label>
            <textarea
              maxLength={1000}
              rows={2}
              value={bio}
              onChange={(e) => setBio(e.target.value)}
              placeholder="Ghi chú về lớp học hoặc chuyên ngành phụ trách..."
              className="w-full px-3.5 py-2 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500/20 focus:border-sky-500 text-slate-800 text-xs resize-none"
            />
          </div>

          {/* Actions */}
          <div className="pt-3 flex items-center justify-end gap-2 text-xs">
            <button
              type="button"
              onClick={onClose}
              className="px-3.5 py-2 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 font-medium cursor-pointer transition-colors"
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              disabled={saving}
              className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-medium shadow-xs cursor-pointer transition-all flex items-center gap-1.5"
            >
              <Save className="w-3.5 h-3.5" />
              <span>{isEditing ? 'Lưu thay đổi' : (fixedRole === 'Teacher' ? 'Thêm giáo viên' : 'Tạo người dùng')}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export const UserFormModal: React.FC<UserFormModalProps> = ({
  fixedRole,
  faculties,
  isOpen,
  onClose,
  onSubmit,
  initialData,
}) => {
  if (!isOpen) return null;

  return (
    <UserFormDialog
      faculties={faculties}
      fixedRole={fixedRole}
      key={initialData?.id || 'new'}
      onClose={onClose}
      onSubmit={onSubmit}
      initialData={initialData}
    />
  );
};
