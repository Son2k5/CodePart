import React from 'react';
import { Bell, Clock, CheckCircle2, AlertTriangle, X } from 'lucide-react';

interface NotificationItem {
  id: string;
  title: string;
  message: string;
  time: string;
  type: 'approval' | 'warning' | 'info';
  read: boolean;
}

interface AdminNotificationsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onNavigatePending?: () => void;
}

export const AdminNotificationsModal: React.FC<AdminNotificationsModalProps> = ({
  isOpen,
  onClose,
  onNavigatePending,
}) => {
  if (!isOpen) return null;

  const notifications: NotificationItem[] = [
    {
      id: '1',
      title: 'Hồ sơ giáo viên mới chờ duyệt',
      message: 'TS. Nguyễn Văn Hùng vừa đăng ký tài khoản giảng viên với email nguyenvanhung@hanu.edu.vn.',
      time: '15 phút trước',
      type: 'approval',
      read: false,
    },
    {
      id: '2',
      title: 'Hồ sơ giáo viên mới chờ duyệt',
      message: 'ThS. Trần Thị Mai Lan vừa đăng ký tài khoản giảng viên với email tranmailan@hanu.edu.vn.',
      time: '2 giờ trước',
      type: 'approval',
      read: false,
    },
    {
      id: '3',
      title: 'Cảnh báo tài khoản sinh viên',
      message: 'Tài khoản 2301040089@hanu.edu.vn vừa bị tạm khóa do vi phạm làm bài trực tuyến.',
      time: '1 ngày trước',
      type: 'warning',
      read: true,
    },
    {
      id: '4',
      title: 'Hệ thống đánh giá Online Judge',
      message: 'Bộ chấm code C++ / Python / Java đã sẵn sàng phục vụ kỳ thi học kỳ 1.',
      time: '2 ngày trước',
      type: 'info',
      read: true,
    },
  ];

  return (
    <div className="absolute right-0 top-12 w-80 sm:w-96 bg-white rounded-2xl shadow-xl border border-slate-200/80 z-50 overflow-hidden animate-in fade-in slide-in-from-top-2 duration-150">
      <div className="p-3.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
        <div className="flex items-center gap-2">
          <Bell className="w-4 h-4 text-sky-600" />
          <span className="text-xs font-bold text-slate-800">Thông báo hệ thống</span>
          <span className="px-1.5 py-0.5 rounded-full text-[10px] font-bold bg-sky-100 text-sky-700">
            2 mới
          </span>
        </div>
        <button
          type="button"
          onClick={onClose}
          className="p-1 rounded-md text-slate-400 hover:text-slate-600"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      <div className="divide-y divide-slate-100 max-h-80 overflow-y-auto">
        {notifications.map((item) => (
          <div
            key={item.id}
            className={`p-3 text-xs transition-colors hover:bg-slate-50 cursor-pointer ${
              !item.read ? 'bg-sky-50/30' : ''
            }`}
            onClick={() => {
              if (item.type === 'approval' && onNavigatePending) {
                onNavigatePending();
                onClose();
              }
            }}
          >
            <div className="flex items-start gap-2.5">
              <div className="shrink-0 mt-0.5">
                {item.type === 'approval' && (
                  <span className="w-6 h-6 rounded-lg bg-amber-100 text-amber-600 flex items-center justify-center">
                    <Clock className="w-3.5 h-3.5" />
                  </span>
                )}
                {item.type === 'warning' && (
                  <span className="w-6 h-6 rounded-lg bg-rose-100 text-rose-600 flex items-center justify-center">
                    <AlertTriangle className="w-3.5 h-3.5" />
                  </span>
                )}
                {item.type === 'info' && (
                  <span className="w-6 h-6 rounded-lg bg-blue-100 text-blue-600 flex items-center justify-center">
                    <CheckCircle2 className="w-3.5 h-3.5" />
                  </span>
                )}
              </div>
              <div className="space-y-1">
                <div className="font-semibold text-slate-800 text-[11px]">{item.title}</div>
                <p className="text-slate-600 text-[11px] leading-relaxed">{item.message}</p>
                <div className="text-[10px] text-slate-400">{item.time}</div>
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="p-2.5 border-t border-slate-100 bg-slate-50 text-center">
        <button
          type="button"
          onClick={() => {
            if (onNavigatePending) onNavigatePending();
            onClose();
          }}
          className="text-[11px] font-semibold text-sky-600 hover:text-sky-700 transition-colors"
        >
          Xem danh sách chờ duyệt →
        </button>
      </div>
    </div>
  );
};
