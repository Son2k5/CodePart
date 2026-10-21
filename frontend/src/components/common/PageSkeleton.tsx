export function PageSkeleton() {
  return (
    <div className="min-h-screen bg-gray-50/50 p-6 flex flex-col gap-6 animate-pulse" aria-label="Đang tải trang…">
      {/* Top navigation skeleton */}
      <div className="h-14 bg-white rounded-xl shadow-xs border border-gray-100 flex items-center justify-between px-6">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gray-200" />
          <div className="w-24 h-4 rounded bg-gray-200" />
        </div>
        <div className="flex items-center gap-4">
          <div className="w-16 h-3 rounded bg-gray-200" />
          <div className="w-8 h-8 rounded-full bg-gray-200" />
        </div>
      </div>

      {/* Main body skeleton */}
      <div className="flex-1 flex flex-col gap-4 max-w-6xl w-full mx-auto">
        <div className="h-8 w-64 rounded bg-gray-200" />
        <div className="h-4 w-96 rounded bg-gray-100" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mt-4">
          <div className="h-44 bg-white rounded-xl border border-gray-100 shadow-xs p-5 space-y-3">
            <div className="h-5 w-3/4 rounded bg-gray-200" />
            <div className="h-3 w-full rounded bg-gray-100" />
            <div className="h-3 w-5/6 rounded bg-gray-100" />
          </div>
          <div className="h-44 bg-white rounded-xl border border-gray-100 shadow-xs p-5 space-y-3">
            <div className="h-5 w-3/4 rounded bg-gray-200" />
            <div className="h-3 w-full rounded bg-gray-100" />
            <div className="h-3 w-5/6 rounded bg-gray-100" />
          </div>
          <div className="h-44 bg-white rounded-xl border border-gray-100 shadow-xs p-5 space-y-3">
            <div className="h-5 w-3/4 rounded bg-gray-200" />
            <div className="h-3 w-full rounded bg-gray-100" />
            <div className="h-3 w-5/6 rounded bg-gray-100" />
          </div>
        </div>
      </div>
    </div>
  );
}
