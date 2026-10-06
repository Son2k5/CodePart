# Login capacity and tuning

## Mục tiêu và luồng xử lý

Login đi qua các lớp theo thứ tự: trusted forwarded headers, token bucket theo IP,
Redis backoff, một truy vấn user theo unique email, concurrency gate, BCrypt, rồi
phát access/refresh token. DB và Redis I/O không giữ permit BCrypt. Refresh token
rotation không chạy BCrypt và không đi qua login gate.

Không thêm replica API trước khi đo vì gate bảo vệ CPU theo từng node. Counter chống
brute-force nằm trong Redis để dùng chung giữa các replica; chỉ khi Redis lỗi mới dùng
fallback in-memory của từng node.

## Cấu hình

### `Auth:RateLimiting`

| Thuộc tính | Mặc định | Ý nghĩa |
|---|---:|---|
| `BurstCapacity` | 400 | Số login một IP có thể gửi ngay. Đủ cho ít nhất 300 sinh viên chung NAT. |
| `RefillPerMinute` | 80 | Số token bổ sung mỗi phút cho cùng IP. |
| `QueueLimit` | 0 | IP limiter không xếp hàng; khi hết token trả 429 và `Retry-After`. |

### `Auth:LoginConcurrency`

| Thuộc tính | Mặc định | Ý nghĩa |
|---|---:|---|
| `MaxConcurrentLogins` | `Environment.ProcessorCount` | Số BCrypt đồng thời trên một API node. Có thể override sau benchmark. |
| `QueueLimit` | 300 | Tổng request được chờ async ngoài các permit đang chạy. |
| `QueueTimeoutSeconds` | 10 | Thời gian chờ tối đa trước 429. |
| `MaxConcurrentPerIp` | 4 | Không cho một IP chiếm toàn bộ CPU gate. |

`MaxConcurrentPerIp` giới hạn công việc đang chạy, không phải số sinh viên hợp lệ
đằng sau NAT. Các request còn lại chờ async và không giữ thread.

### `Auth:LoginProtection`

| Thuộc tính | Mặc định | Ý nghĩa |
|---|---:|---|
| `BackoffThreshold` | 5 | Bắt đầu backoff cho cặp IP+email. |
| `FailureCounterTtlMinutes` | 15 | TTL của failure counter IP+email. |
| `InitialBackoffSeconds` | 2 | Delay lần đầu: hai giây. |
| `MaxBackoffSeconds` | 60 | Trần exponential backoff. |

Backoff có dạng `min(MaxBackoffSeconds, InitialBackoffSeconds * 2^n)`.

### Password và token

| Thuộc tính | Mặc định |
|---|---:|
| `Auth:PasswordHashing:WorkFactor` | 12 |
| `Jwt:AccessTokenExpiryMinutes` | 15 |
| `Auth:Session:RefreshTokenLifetimeDays` | 7 |
| `Auth:Session:RefreshTokenAbsoluteLifetimeDays` | 30 |

Khi login thành công với BCrypt cost cũ, hash được nâng lên cost hiện tại. Không hạ
cost để đạt throughput. Refresh token chỉ lưu SHA-256 hash, được rotate mỗi lần dùng;
reuse hoặc race sẽ revoke token family.

### Reverse proxy

Chỉ khai báo chính xác proxy/subnet do hệ thống quản lý:

```json
"ReverseProxy": {
  "KnownNetworks": ["172.20.0.0/16"],
  "KnownProxies": ["127.0.0.1"],
  "ForwardLimit": 1
}
```

Không thêm subnet của client, trường hoặc Internet. `X-Forwarded-For` từ địa chỉ ngoài
danh sách bị bỏ qua. Nginx thường là proxy duy nhất nên `ForwardLimit = 1` là phù hợp.

### MySQL và Redis

`Infrastructure:MySql` cho phép đặt `MinPoolSize`, `MaxPoolSize` và
`ConnectionIdleLifetimeSeconds`. Các giá trị mặc định là `null`, tức giữ mặc định của
MySqlConnector cho đến khi có số liệu server thật. Có thể đặt pool trực tiếp trong connection
string; cấu hình strongly typed sẽ override nếu được cung cấp.

StackExchange.Redis dùng một singleton multiplexer, không tạo connection pool theo
mỗi request. `Infrastructure:Redis` expose connect retry, connect/sync timeout và
keepalive. Luôn dùng Redis authentication ngoài Development/Testing.

Máy đã chạy MySQL/Redis service local thì API không cần Docker. Integration tests
vẫn dùng Testcontainers để có database cô lập và cần Docker.

## Chọn giá trị theo core và RAM

1. Đo thời gian BCrypt cost 12 trên đúng CPU production, gọi là `H` giây/hash.
2. Bắt đầu `MaxConcurrentLogins = số logical core` như mặc định.
3. Throughput BCrypt gần đúng của một node là
   `MaxConcurrentLogins / H` login/giây, sau đó nhân 0.7–0.8 để chừa CPU cho API.
4. Tăng hoặc giảm permit bằng k6; dừng tăng khi p95 xấu đi, CPU giữ 100%, hoặc xuất
   hiện thread-pool starvation. Không suy ra permit chỉ từ RAM.
5. Queue gần đúng theo Little's Law:
   `QueueLimit >= burst_rate * acceptable_wait_seconds`, nhưng phải giới hạn để tránh
   giữ quá nhiều request. Mặc định 300 là điểm bắt đầu, không phải cam kết cho mọi máy.
6. `BurstCapacity` phải lớn hơn số người có thể login gần đồng thời sau một NAT.
   `RefillPerMinute` phải lớn hơn mức login hợp lệ duy trì, nhưng thấp hơn tốc độ attack
   mà hệ thống muốn chấp nhận.
7. MySQL `MaxPoolSize` phải dựa trên tổng replica và giới hạn connection của DB:
   `pool_per_node * replica_count + admin_reserve <= database_max_connections`.

RAM chủ yếu ảnh hưởng queue, EF objects và connection buffers; BCrypt chủ yếu bị giới
hạn bởi CPU. Theo dõi working set trong load test trước khi tăng queue hoặc DB pool.

## Health

`/health` kiểm tra MySQL và Redis; `/health/live` chỉ kiểm tra process. Mọi response
có `X-Correlation-ID`. Log scope chứa correlation ID nhưng không chứa password, token
hoặc hash.

## Chạy và tune bằng k6

1. Tạo ít nhất 1.000 user đã verify theo quy ước email trong `tests/load/README.md`.
2. Chạy API bằng cấu hình gần production, HTTPS và Nginx tương đương production.
3. Cấu hình load generator/proxy là trusted proxy chỉ trong môi trường test.
4. Đặt `BASE_URL`, `LOGIN_EMAIL_PREFIX`, `LOGIN_EMAIL_DOMAIN` và `LOGIN_PASSWORD`.
5. Chạy lần lượt:

```powershell
k6 run --summary-export tests/load/results/a.json tests/load/scenario-a-nat.js
k6 run --summary-export tests/load/results/b.json tests/load/scenario-b-distributed.js
k6 run --summary-export tests/load/results/c.json tests/load/scenario-c-attack.js
k6 run --summary-export tests/load/results/d.json tests/load/scenario-d-refresh.js
```

6. Ghi vào `tests/load/report.md`: core/RAM, replica, toàn bộ override, DB pool, Redis,
   p50/p95/p99, status counts, CPU, RAM, DB active connections và queue depth.
7. Chỉ thay một nhóm cấu hình mỗi lần. Điều kiện đạt: không HTTP 500, không cạn DB
   pool, không thread-pool starvation, user thật ở scenario C vẫn login được, và p95
   scenario A dưới năm giây.
