import { isRouteErrorResponse, useRouteError, Link } from 'react-router-dom';

export function RouteErrorBoundary() {
  const error = useRouteError();

  let title = 'Đã xảy ra sự cố';
  let message = 'Không thể tải được nội dung trang. Vui lòng kiểm tra kết nối mạng và thử lại.';

  if (isRouteErrorResponse(error)) {
    if (error.status === 404) {
      title = '404 - Không tìm thấy trang';
      message = 'Trang bạn đang tìm kiếm không tồn tại hoặc đã được di chuyển.';
    } else if (error.status === 403) {
      title = '403 - Giới hạn quyền truy cập';
      message = 'Bạn không có quyền truy cập vào nội dung này.';
    } else if (error.status === 500) {
      title = '500 - Lỗi máy chủ';
      message = 'Máy chủ đang gặp sự cố. Đội ngũ kỹ thuật đang xử lý.';
    }
  } else if (error instanceof Error) {
    if (error.message.includes('Failed to fetch dynamically imported module') || error.message.includes('dynamically imported module')) {
      title = 'Phiên bản trang web đã cập nhật';
      message = 'Trình duyệt của bạn đang giữ bản cache cũ. Vui lòng làm mới trang để tải bản cập nhật mới nhất.';
    } else {
      message = error.message;
    }
  }

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center p-6 font-sans">
      <div className="max-w-md w-full bg-white rounded-2xl p-8 border border-gray-200 shadow-sm text-center">
        <div className="w-14 h-14 bg-red-50 text-red-600 rounded-2xl mx-auto flex items-center justify-center mb-5 text-2xl font-bold">
          !
        </div>
        <h1 className="text-xl font-bold text-gray-900 mb-2">{title}</h1>
        <p className="text-sm text-gray-600 mb-6 leading-relaxed">{message}</p>
        <div className="flex flex-col sm:flex-row gap-3 justify-center">
          <button
            onClick={() => window.location.reload()}
            className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-xl transition-colors shadow-xs"
          >
            Tải lại trang
          </button>
          <Link
            to="/"
            className="px-5 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-medium rounded-xl transition-colors"
          >
            Về trang chủ
          </Link>
        </div>
      </div>
    </div>
  );
}
