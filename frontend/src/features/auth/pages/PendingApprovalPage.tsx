import React from 'react';
import { Link } from 'react-router-dom';
import { AuthLayout } from '../components/AuthLayout';
import { Clock } from 'lucide-react';

export const PendingApprovalPage: React.FC = () => {
  return (
    <AuthLayout title="Account Pending" subtitle="Your teacher account is under review">
      <div className="flex flex-col items-center justify-center space-y-6 mt-8">
        <div className="w-16 h-16 bg-amber-50 text-amber-500 rounded-full flex items-center justify-center">
          <Clock className="w-8 h-8" />
        </div>
        <p className="text-center text-sm text-gray-600 leading-relaxed">
          Cảm ơn bạn đã đăng ký tài khoản Giảng viên. Hệ thống CodePath yêu cầu Quản trị viên (Admin) phê duyệt tài khoản của bạn trước khi có thể đăng nhập.
          <br /><br />
          Vui lòng kiên nhẫn chờ đợi, quản trị viên sẽ xem xét yêu cầu trong thời gian sớm nhất.
        </p>
        <div className="pt-4 w-full">
          <Link
            to="/login"
            className="w-full flex items-center justify-center py-3.5 px-4 bg-[#1a1a1a] hover:bg-black text-white font-semibold text-sm rounded-full shadow-md transition-all duration-200"
          >
            Return to Login
          </Link>
        </div>
      </div>
    </AuthLayout>
  );
};
