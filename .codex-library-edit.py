from pathlib import Path
root=Path('.')
def edit(path, old, new):
 p=root/path; s=p.read_text(encoding='utf-8-sig'); assert old in s, (path,old); p.write_text(s.replace(old,new),encoding='utf-8')
base='api/src/'
edit(base+'Domain/CodePath.Domain/Judge/Problem.cs','public string Difficulty', 'public string Category { get; set; } = "general";\n    public string Difficulty')
p=base+'Application/CodePath.Application/Judge/JudgeContracts.cs'
edit(p,'Guid? ClassId = null);','Guid? ClassId = null, string Category = "general");')
edit(p,'x.CourseId.HasValue != x.ClassId.HasValue','!(x.CourseId.HasValue && x.ClassId.HasValue)')
edit(p,'Choose exactly one class or course for this problem.','Choose at most one class or course; leave both empty for the practice library.')
edit(p,'RuleFor(x => x.Title)', 'RuleFor(x => x.Category).Must(x => new[] { "general", "basics", "arrays", "strings", "math", "search-sort", "data-structures", "graphs", "dynamic-programming" }.Contains(x));\n        RuleFor(x => x.Title)')
edit(p,'Guid? classId = null);','Guid? classId = null, bool library = false, string? search = null, string? difficulty = null, string? category = null, string? language = null);')
p=base+'Infrastructure/CodePath.Infrastructure/Judge/JudgeService.cs'
edit(p,'Guid? classId = null)\n    {\n        if (courseId', 'Guid? classId = null, bool library = false, string? search = null, string? difficulty = null, string? category = null, string? language = null)\n    {\n        if (library && (courseId.HasValue || classId.HasValue)) return Fail("Library cannot be combined with a class or course.");\n        if (courseId')
edit(p,'var scopes = await access.Scopes(a, manage, ct);','var scopes = library ? Array.Empty<JudgeScope>() : await access.Scopes(a, manage, ct);')
edit(p,'if (courseId.HasValue) q = q.Where', '''if (library) q = q.Where(p => p.CourseId == null && p.ClassId == null);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); q = q.Where(p => p.Title.Contains(term)); }
        if (!string.IsNullOrWhiteSpace(difficulty)) q = q.Where(p => p.Difficulty == difficulty);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(language)) q = q.Where(p => p.StarterCodes.Any(s => s.Language == language));
        if (courseId.HasValue) q = q.Where''')
edit(p,'p.Id, p.Title, p.Difficulty, p.Points, Status', 'p.Id, p.Title, p.Category, p.Difficulty, p.Points, Status')
edit(p,'p.Id, p.Title, p.Description, p.Difficulty','p.Id, p.Title, p.Description, p.Category, p.Difficulty')
edit(p,'p.Difficulty = r.Difficulty;', 'p.Category = r.Category; p.Difficulty = r.Difficulty;')
p=base+'Infrastructure/CodePath.Infrastructure/Judge/JudgeDbContext.cs'
edit(p,'p.Property(x => x.Title).HasMaxLength(250);','p.Property(x => x.Title).HasMaxLength(250);\n        p.Property(x => x.Category).HasMaxLength(40).HasDefaultValue("general");')
p=base+'Api/CodePath.Api/Endpoints/JudgeEndpoints.cs'
edit(p,'Guid? classId = null) => (await s.ListProblems(Actor(c), false, page, ct, courseId, classId))','Guid? classId = null, bool library = false, string? search = null, string? difficulty = null, string? category = null, string? language = null) => (await s.ListProblems(Actor(c), false, page, ct, courseId, classId, library, search, difficulty, category, language))')
p='frontend/src/features/code-judge/api.ts'
edit(p,'difficulty: string; timeLimitMs', 'category: string; difficulty: string; timeLimitMs')
edit(p,'  run: async', '''  library: async (filters: { page: number; search: string; difficulty: string; category: string; language: string }): Promise<Page<Problem>> => (await api.get('/problems/', { params: { ...filters, library: true } })).data,
  run: async''')
p='frontend/src/features/code-judge/ScopePicker.tsx'
edit(p,"{all ? 'Tất cả lớp' : 'Chọn lớp đăng bài'}", "{all ? 'Tất cả bài tập' : 'Thư viện luyện tập chung'}")
p='frontend/src/features/code-judge/JudgeAdminPage.tsx'
edit(p,"import { ScopePicker }", "import { problemCategories } from './problemCategories';\nimport { ScopePicker }")
edit(p,"title: '', description: '', difficulty", "title: '', description: '', category: 'general', difficulty")
edit(p,'Bài tập lập trình theo lớp','Quản lý bài tập lập trình')
edit(p,'Chọn lớp, tạo đề và bộ test, sau đó xuất bản để học sinh làm bài trên khung coding chung.','Tạo bài cho thư viện luyện tập chung hoặc giao riêng cho lớp. Thêm đề, phân loại và bộ test trước khi xuất bản.')
edit(p,'disabled={!scopes.data?.length}', 'disabled={busy}')
edit(p,'+ Tạo bài tập cho lớp','+ Tạo bài tập')
edit(p,'<label>Lớp nhận bài tập','<label>Nơi đăng bài tập')
edit(p,'<label>Độ khó<select', '<label>Dạng bài<select value={form.category || \'general\'} onChange={e => setForm({ ...form, category: e.target.value })}>{problemCategories.map(c => <option key={c.id} value={c.id}>{c.label}</option>)}</select></label>\n          <label>Độ khó<select')
edit(p,'disabled={!problemScope(form)} onClick={save}', 'disabled={busy} onClick={save}')
edit(p,"?.name} · {p.status", "?.name || 'Thư viện luyện tập chung'} · {p.status")
p='api/tests/CodePath.Unit.Tests/Judge/JudgeRulesTests.cs'
edit(p,'New_problem_must_belong_to_exactly_one_class_or_course','New_problem_can_be_public_or_belong_to_one_class_or_course')
edit(p,'Assert.False(validator.Validate(request).IsValid);','Assert.True(validator.Validate(request).IsValid);\n        Assert.False(validator.Validate(request with { Category = "invalid" }).IsValid);\n        Assert.True(validator.Validate(request with { Category = "arrays" }).IsValid);')
