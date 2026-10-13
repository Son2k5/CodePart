import React, { useState } from 'react';
import {
  Eye,
  CheckCircle,
  XCircle,
  Lock,
  Unlock,
  Calendar,
  SearchX,
  ChevronLeft,
  ChevronRight,
  Edit,
} from 'lucide-react';
import type { AdminUserItem } from '../types/admin.types';
import { UserRoleBadge } from './UserRoleBadge';
import { UserStatusBadge } from './UserStatusBadge';

interface UserTableProps {
  users: AdminUserItem[];
  onViewUser: (user: AdminUserItem) => void;
  onEditUser: (user: AdminUserItem) => void;
  onApproveTeacher: (user: AdminUserItem) => void;
  onRejectTeacher: (user: AdminUserItem) => void;
  onToggleUserStatus: (user: AdminUserItem) => void;
}

export const UserTable: React.FC<UserTableProps> = ({
  users,
  onViewUser,
  onEditUser,
  onApproveTeacher,
  onRejectTeacher,
  onToggleUserStatus,
}) => {
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const totalPages = Math.ceil(users.length / pageSize) || 1;
  const startIndex = (currentPage - 1) * pageSize;
  const paginatedUsers = users.slice(startIndex, startIndex + pageSize);

  const handleSelectAll = (checked: boolean) => {
    if (checked) {
      setSelectedIds(paginatedUsers.map((u) => u.id));
    } else {
      setSelectedIds([]);
    }
  };

  const handleSelectOne = (id: string) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
    );
  };

  const getInitials = (name: string) => {
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  };

  const getAvatarGradient = (role: string) => {
    if (role === 'Admin') return 'from-amber-400 to-orange-500 text-white';
    if (role === 'Teacher') return 'from-indigo-500 to-purple-600 text-white';
    return 'from-sky-400 to-blue-600 text-white';
  };

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return 'Chưa có';
    const date = new Date(dateStr);
    return date.toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  };

  return (
    <div className="bg-white rounded-2xl border border-slate-200/90 shadow-xs overflow-hidden">
      {/* Bulk action toolbar if items are selected */}
      {selectedIds.length > 0 && (
        <div className="bg-sky-50/80 border-b border-sky-100 px-6 py-3 flex items-center justify-between transition-all">
          <div className="flex items-center gap-2 text-xs text-sky-900 font-medium">
            <span className="w-2 h-2 rounded-full bg-sky-500" />
            <span>Đã chọn <strong>{selectedIds.length}</strong> người dùng</span>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setSelectedIds([])}
              className="text-xs text-slate-600 hover:text-slate-900 px-3 py-1 rounded-lg hover:bg-white/80 transition-colors"
            >
              Bỏ chọn
            </button>
          </div>
        </div>
      )}

      {/* Table Container */}
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50/70 border-b border-slate-200/80 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              <th className="py-3.5 pl-6 pr-3 w-10">
                <input
                  type="checkbox"
                  checked={
                    paginatedUsers.length > 0 &&
                    paginatedUsers.every((u) => selectedIds.includes(u.id))
                  }
                  onChange={(e) => handleSelectAll(e.target.checked)}
                  className="rounded border-slate-300 text-sky-600 focus:ring-sky-500/20 cursor-pointer"
                />
              </th>
              <th className="py-3.5 px-4 min-w-[240px]">Người dùng</th>
              <th className="py-3.5 px-4 min-w-[120px]">Vai trò</th>
              <th className="py-3.5 px-4 min-w-[130px]">Mã định danh</th>
              <th className="py-3.5 px-4 min-w-[180px]">Đơn vị / Khoa</th>
              <th className="py-3.5 px-4 min-w-[130px]">Trạng thái</th>
              <th className="py-3.5 px-4 min-w-[130px]">Ngày tạo</th>
              <th className="py-3.5 pr-6 pl-4 text-right min-w-[130px]">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 text-xs text-slate-600">
            {paginatedUsers.length === 0 ? (
              <tr>
                <td colSpan={8} className="py-14 text-center">
                  <div className="max-w-xs mx-auto flex flex-col items-center justify-center space-y-3">
                    <div className="w-12 h-12 rounded-2xl bg-slate-100 flex items-center justify-center text-slate-400">
                      <SearchX className="w-6 h-6" />
                    </div>
                    <div className="text-slate-900 font-semibold text-sm">Không tìm thấy người dùng</div>
                    <p className="text-slate-500 text-xs">
                      Vui lòng thử thay đổi điều kiện tìm kiếm hoặc đặt lại các bộ lọc hiện tại.
                    </p>
                  </div>
                </td>
              </tr>
            ) : (
              paginatedUsers.map((user) => {
                const isSelected = selectedIds.includes(user.id);
                const code = user.studentId || user.teacherId || '—';

                return (
                  <tr
                    key={user.id}
                    className={`hover:bg-slate-50/80 transition-colors ${
                      isSelected ? 'bg-sky-50/30' : ''
                    }`}
                  >
                    {/* Checkbox */}
                    <td className="py-3.5 pl-6 pr-3">
                      <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => handleSelectOne(user.id)}
                        className="rounded border-slate-300 text-sky-600 focus:ring-sky-500/20 cursor-pointer"
                      />
                    </td>

                    {/* User Info */}
                    <td className="py-3.5 px-4">
                      <div className="flex items-center gap-3">
                        <div
                          className={`w-9 h-9 rounded-xl bg-gradient-to-br ${getAvatarGradient(
                            user.role
                          )} flex items-center justify-center font-bold text-xs shrink-0 shadow-2xs`}
                        >
                          {getInitials(user.fullName)}
                        </div>
                        <div className="min-w-0">
                          <button
                            type="button"
                            onClick={() => onViewUser(user)}
                            className="text-slate-900 font-semibold hover:text-sky-600 truncate block text-left transition-colors cursor-pointer"
                          >
                            {user.fullName}
                          </button>
                          <div className="text-slate-400 text-[11px] truncate">{user.email}</div>
                        </div>
                      </div>
                    </td>

                    {/* Role */}
                    <td className="py-3.5 px-4">
                      <UserRoleBadge role={user.role} />
                    </td>

                    {/* Student/Teacher Code */}
                    <td className="py-3.5 px-4">
                      {code !== '—' ? (
                        <span className="font-mono text-[11px] font-semibold text-slate-700 bg-slate-100 px-2 py-0.5 rounded">
                          {code}
                        </span>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>

                    {/* Department */}
                    <td className="py-3.5 px-4">
                      <span className="text-slate-700 text-xs truncate max-w-[170px] block" title={user.department}>
                        {user.department || 'Chưa cập nhật'}
                      </span>
                    </td>

                    {/* Status */}
                    <td className="py-3.5 px-4">
                      <UserStatusBadge status={user.status} />
                    </td>

                    {/* Created Date */}
                    <td className="py-3.5 px-4 text-slate-500 text-[11px]">
                      <div className="flex items-center gap-1.5">
                        <Calendar className="w-3.5 h-3.5 text-slate-400" />
                        <span>{formatDate(user.createdAt)}</span>
                      </div>
                    </td>

                    {/* Quick Actions */}
                    <td className="py-3.5 pr-6 pl-4 text-right">
                      <div className="flex items-center justify-end gap-1">
                        {/* If pending teacher: Quick approve/reject buttons */}
                        {user.role === 'Teacher' && user.status === 'Pending' && (
                          <>
                            <button
                              type="button"
                              onClick={() => onApproveTeacher(user)}
                              className="p-1.5 rounded-lg text-emerald-600 hover:bg-emerald-50 transition-colors cursor-pointer"
                              title="Phê duyệt hồ sơ giáo viên"
                            >
                              <CheckCircle className="w-4 h-4" />
                            </button>
                            <button
                              type="button"
                              onClick={() => onRejectTeacher(user)}
                              className="p-1.5 rounded-lg text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
                              title="Từ chối hồ sơ giáo viên"
                            >
                              <XCircle className="w-4 h-4" />
                            </button>
                          </>
                        )}

                        {/* View detail button */}
                        <button
                          type="button"
                          onClick={() => onViewUser(user)}
                          className="p-1.5 rounded-lg text-slate-500 hover:text-sky-600 hover:bg-sky-50 transition-colors cursor-pointer"
                          title="Xem chi tiết hồ sơ"
                        >
                          <Eye className="w-4 h-4" />
                        </button>

                        {/* Edit button */}
                        <button
                          type="button"
                          onClick={() => onEditUser(user)}
                          className="p-1.5 rounded-lg text-slate-500 hover:text-indigo-600 hover:bg-indigo-50 transition-colors cursor-pointer"
                          title="Chỉnh sửa thông tin"
                        >
                          <Edit className="w-4 h-4" />
                        </button>

                        {/* Lock / Unlock Toggle */}
                        {user.role !== 'Admin' && (
                          <button
                            type="button"
                            onClick={() => onToggleUserStatus(user)}
                            className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                              user.status === 'Disabled'
                                ? 'text-amber-600 hover:bg-amber-50'
                                : 'text-slate-400 hover:text-slate-700 hover:bg-slate-100'
                            }`}
                            title={user.status === 'Disabled' ? 'Mở khóa tài khoản' : 'Khóa tài khoản'}
                          >
                            {user.status === 'Disabled' ? (
                              <Unlock className="w-4 h-4" />
                            ) : (
                              <Lock className="w-4 h-4" />
                            )}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <div className="p-4 border-t border-slate-100 bg-slate-50/50 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-slate-500">
        <div className="flex items-center gap-2">
          <span>Hiển thị</span>
          <select
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setCurrentPage(1);
            }}
            className="py-1 px-2 border border-slate-200 rounded-lg bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-500 text-xs"
          >
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
          </select>
          <span>
            trên tổng số <strong>{users.length}</strong> người dùng
          </span>
        </div>

        <div className="flex items-center gap-1.5">
          <button
            type="button"
            disabled={currentPage <= 1}
            onClick={() => setCurrentPage((prev) => Math.max(prev - 1, 1))}
            className="p-1.5 rounded-lg border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
            title="Trang trước"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>

          <span className="px-3 py-1 text-slate-700 font-medium">
            Trang {currentPage} / {totalPages}
          </span>

          <button
            type="button"
            disabled={currentPage >= totalPages}
            onClick={() => setCurrentPage((prev) => Math.min(prev + 1, totalPages))}
            className="p-1.5 rounded-lg border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
            title="Trang tiếp"
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
};
