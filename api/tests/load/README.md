# Auth capacity load tests

The scripts expect pre-created, verified users whose addresses follow
`LOGIN_EMAIL_PREFIX + zero-padded index + @ + LOGIN_EMAIL_DOMAIN`, all sharing
`LOGIN_PASSWORD`. Do not run them against production user data.

Example:

```powershell
$env:BASE_URL = 'https://localhost:5001'
$env:LOGIN_EMAIL_PREFIX = 'load-user-'
$env:LOGIN_EMAIL_DOMAIN = 'example.edu'
$env:LOGIN_PASSWORD = '<load-test-password>'
k6 run --summary-export tests/load/results/scenario-a.json tests/load/scenario-a-nat.js
```

Run the remaining files in the same way. `X-Forwarded-For` affects the test only
when the load generator reaches the API through a proxy listed in
`ReverseProxy:KnownNetworks` or `KnownProxies`. Never trust a public client range.

Scenario D requires HTTPS because refresh tokens are Secure HttpOnly cookies.
