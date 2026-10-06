# Login capacity test report

Status: **not executed**.

The implementation and k6 scenarios are present, but k6 is not installed in the
current development environment and no production-like API target was supplied.
No latency or throughput numbers have been fabricated.

## Configuration represented by the scripts

- Scenario A: 1,000 verified users, three NAT IP addresses, maximum 60 seconds.
- Scenario B: 1,000 verified users distributed across many IP addresses.
- Scenario C: 20 rotating-email attacks/second from one IP while 100 real users log in.
- Scenario D: constant arrival rate of 5,000 refresh requests per 60 seconds.
- Required assertions: zero HTTP 500 responses; scenario A login p95 below five seconds;
  real users in scenario C and refreshes in scenario D must succeed.

## Server defaults to record when executing

- Token bucket: burst 400, refill 80/minute, queue 0.
- Login gate: `Environment.ProcessorCount` global permits, queue 300, timeout 10 seconds,
  four permits per IP.
- BCrypt work factor: 12.
- Refresh token rolling lifetime: seven days; absolute family lifetime: 30 days.

After each run, attach the k6 JSON summary and record server core/RAM, MySQL
pool settings, Redis settings, API replica count, and the final appsettings overrides.
