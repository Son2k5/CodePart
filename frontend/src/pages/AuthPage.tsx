import React, { useState } from 'react'
import { Code2, Mail, Lock, User, ArrowRight, Sparkles } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { login } from '../lib/apiClient' // Adjust if register is available

export const AuthPage: React.FC = () => {
  const [isLogin, setIsLogin] = useState(true)
  const navigate = useNavigate()

  // Form states
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsLoading(true)

    try {
      if (isLogin) {
        await login(email, password)
        navigate('/') // Go to dashboard or home after login
      } else {
        // Mock register for now, usually await register(name, email, password)
        await new Promise(r => setTimeout(r, 1000))
        setIsLogin(true) // Switch to login after successful register
      }
    } catch (err: any) {
      setError(err.message || 'Xác thực thất bại. Vui lòng kiểm tra lại thông tin.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-white flex">
      {/* Left Column - Visual/Brand (Hidden on very small screens) */}
      <div className="hidden lg:flex lg:w-1/2 relative bg-slate-900 overflow-hidden">
        {/* Abstract Background Elements */}
        <div className="absolute inset-0 bg-gradient-to-br from-blue-900 via-slate-900 to-indigo-900" />
        <div className="absolute top-0 left-0 w-full h-full opacity-30 bg-[radial-gradient(ellipse_at_top_left,_var(--tw-gradient-stops))] from-blue-400 via-transparent to-transparent" />
        <div className="absolute bottom-0 right-0 w-full h-full opacity-20 bg-[radial-gradient(ellipse_at_bottom_right,_var(--tw-gradient-stops))] from-indigo-500 via-transparent to-transparent" />

        {/* Content */}
        <div className="relative z-10 flex flex-col justify-between p-16 h-full w-full">
          <div>
            <div className="flex items-center gap-2.5 mb-12">
              <div className="w-10 h-10 bg-blue-600 rounded-xl flex items-center justify-center text-white shadow-lg">
                <Code2 className="w-5 h-5 stroke-[2.5]" />
              </div>
              <div className="flex items-baseline gap-1">
                <span className="text-2xl font-extrabold tracking-tight text-white">CodePath</span>
              </div>
            </div>

            <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg bg-white/10 backdrop-blur-md border border-white/20 text-blue-100 text-xs font-bold tracking-wider uppercase mb-6">
              <Sparkles className="w-4 h-4 text-blue-300" />
              <span>Dành cho sinh viên CNTT</span>
            </div>

            <h1 className="text-4xl lg:text-5xl font-extrabold text-white leading-tight mb-6">
              Bắt đầu hành trình <br /> trở thành Kỹ sư phần mềm.
            </h1>
            <p className="text-lg text-slate-300 max-w-md leading-relaxed">
              Nền tảng thực hành tự động chấm điểm, hỗ trợ bạn nắm vững nền tảng thuật toán, Java, Python và JavaScript.
            </p>
          </div>

          {/* Mini Testimonial */}
          <div className="bg-white/10 backdrop-blur-lg border border-white/10 p-6 rounded-2xl max-w-md">
            <p className="text-slate-200 text-sm italic mb-4 leading-relaxed">
              "Việc hệ thống chỉ ra test case sai ở ngay từng dòng code giúp mình hiểu sâu hơn về tư duy thuật toán, thay vì chỉ học vẹt."
            </p>
            <div className="flex items-center gap-3">
              <div className="w-9 h-9 rounded-full bg-blue-500 flex items-center justify-center text-white font-bold text-sm">
                VN
              </div>
              <div>
                <h4 className="text-sm font-bold text-white">Nguyễn Văn Nam</h4>
                <p className="text-xs text-slate-400">Sinh viên K65 - Khoa CNTT</p>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Right Column - Form */}
      <div className="w-full lg:w-1/2 flex items-center justify-center p-6 sm:p-12 lg:p-16">
        <div className="w-full max-w-md">
          {/* Mobile Header (Only visible on small screens) */}
          <div className="flex lg:hidden items-center gap-2.5 mb-10">
            <div className="w-9 h-9 bg-blue-600 rounded-lg flex items-center justify-center text-white shadow-sm">
              <Code2 className="w-5 h-5 stroke-[2.5]" />
            </div>
            <span className="text-2xl font-extrabold tracking-tight text-slate-900">CodePath</span>
          </div>

          <div className="mb-8">
            <h2 className="text-3xl font-extrabold text-slate-900 tracking-tight mb-2">
              {isLogin ? 'Đăng nhập hệ thống' : 'Tạo tài khoản sinh viên'}
            </h2>
            <p className="text-sm text-slate-600">
              {isLogin
                ? 'Chào mừng bạn quay trở lại. Hãy đăng nhập để tiếp tục thực hành.'
                : 'Đăng ký miễn phí để truy cập kho bài tập tự động chấm điểm.'}
            </p>
          </div>

          {error && (
            <div className="mb-6 p-4 bg-rose-50 border border-rose-200 text-rose-700 rounded-xl text-sm font-medium animate-fadeIn">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Name Field (Only for Register) */}
            {!isLogin && (
              <div className="space-y-1.5 animate-fadeIn">
                <label className="block text-xs font-bold uppercase tracking-wider text-slate-700">
                  Họ tên sinh viên
                </label>
                <div className="relative">
                  <User className="w-5 h-5 text-slate-400 absolute left-3.5 top-3" />
                  <input
                    type="text"
                    required
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="VD: Nguyễn Văn A"
                    className="w-full pl-11 pr-4 py-3 bg-white border border-slate-300 rounded-xl text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition-all"
                  />
                </div>
              </div>
            )}

            {/* Email Field */}
            <div className="space-y-1.5">
              <label className="block text-xs font-bold uppercase tracking-wider text-slate-700">
                Email / Mã sinh viên
              </label>
              <div className="relative">
                <Mail className="w-5 h-5 text-slate-400 absolute left-3.5 top-3" />
                <input
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="sinhvien@codepath.edu.vn"
                  className="w-full pl-11 pr-4 py-3 bg-white border border-slate-300 rounded-xl text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition-all"
                />
              </div>
            </div>

            {/* Password Field */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-700">
                  Mật khẩu
                </label>
                {isLogin && (
                  <a href="#forgot" className="text-xs font-semibold text-blue-600 hover:text-blue-700 hover:underline transition">
                    Quên mật khẩu?
                  </a>
                )}
              </div>
              <div className="relative">
                <Lock className="w-5 h-5 text-slate-400 absolute left-3.5 top-3" />
                <input
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-11 pr-4 py-3 bg-white border border-slate-300 rounded-xl text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-600 focus:border-transparent transition-all"
                />
              </div>
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full inline-flex items-center justify-center gap-2 py-3.5 px-4 mt-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white font-semibold text-sm rounded-xl shadow-md hover:shadow-lg transition-all duration-200"
            >
              <span>{isLoading ? 'Đang xử lý...' : (isLogin ? 'Đăng nhập vào hệ thống' : 'Tạo tài khoản')}</span>
              {!isLoading && <ArrowRight className="w-4 h-4" />}
            </button>
          </form>

          {/* Separator */}
          <div className="my-8 flex items-center">
            <div className="flex-1 border-t border-slate-200"></div>
            <span className="px-4 text-xs font-semibold text-slate-400 uppercase tracking-wider">Hoặc</span>
            <div className="flex-1 border-t border-slate-200"></div>
          </div>

          {/* OAuth Buttons */}
          <button className="w-full inline-flex items-center justify-center gap-3 py-3 px-4 bg-white hover:bg-slate-50 border border-slate-300 text-slate-700 font-semibold text-sm rounded-xl transition duration-150 shadow-sm">
            <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path fillRule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.531 1.032 1.531 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" clipRule="evenodd" />
            </svg>
            <span>Tiếp tục với Github</span>
          </button>

          {/* Toggle Login/Register */}
          <div className="mt-8 text-center text-sm text-slate-600">
            {isLogin ? (
              <p>
                Sinh viên mới?{' '}
                <button onClick={() => { setIsLogin(false); setError(null); }} className="font-bold text-blue-600 hover:text-blue-700 hover:underline transition">
                  Đăng ký tài khoản
                </button>
              </p>
            ) : (
              <p>
                Đã có tài khoản?{' '}
                <button onClick={() => { setIsLogin(true); setError(null); }} className="font-bold text-blue-600 hover:text-blue-700 hover:underline transition">
                  Đăng nhập ngay
                </button>
              </p>
            )}
          </div>

        </div>
      </div>
    </div>
  )
}
