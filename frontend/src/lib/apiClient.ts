export interface AuthTokenResponse {
  accessToken: string
  expiresIn: number
  tokenType: string
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

let accessToken: string | null = null
let refreshPromise: Promise<string | null> | null = null

function apiUrl(path: string): string {
  return `${apiBaseUrl}${path.startsWith('/') ? path : `/${path}`}`
}

async function readTokenResponse(response: Response): Promise<AuthTokenResponse> {
  if (!response.ok) {
    throw new Error(`Authentication request failed with status ${response.status}.`)
  }

  return response.json() as Promise<AuthTokenResponse>
}

export function getAccessToken(): string | null {
  return accessToken
}

export function clearAccessToken(): void {
  accessToken = null
}

export async function login(email: string, password: string): Promise<AuthTokenResponse> {
  const response = await fetch(apiUrl('/api/auth/login'), {
    method: 'POST',
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ email, password }),
  })

  const tokens = await readTokenResponse(response)
  accessToken = tokens.accessToken
  return tokens
}

export function refreshAccessToken(): Promise<string | null> {
  if (refreshPromise) {
    return refreshPromise
  }

  refreshPromise = fetch(apiUrl('/api/auth/refresh'), {
    method: 'POST',
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
    .then(async (response) => {
      if (!response.ok) {
        accessToken = null
        return null
      }

      const tokens = await readTokenResponse(response)
      accessToken = tokens.accessToken
      return accessToken
    })
    .catch(() => {
      accessToken = null
      return null
    })
    .finally(() => {
      refreshPromise = null
    })

  return refreshPromise
}

export async function authenticatedFetch(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  const send = (token: string | null) => {
    const headers = new Headers(init.headers)
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    } else {
      headers.delete('Authorization')
    }

    return fetch(apiUrl(path), {
      ...init,
      credentials: 'include',
      headers,
    })
  }

  const response = await send(accessToken)
  if (response.status !== 401) {
    return response
  }

  const refreshedToken = await refreshAccessToken()
  return refreshedToken ? send(refreshedToken) : response
}

export async function logout(): Promise<void> {
  try {
    await authenticatedFetch('/api/auth/logout', { method: 'POST' })
  } finally {
    accessToken = null
  }
}

export async function logoutAll(): Promise<void> {
  try {
    await authenticatedFetch('/api/auth/logout-all', { method: 'POST' })
  } finally {
    accessToken = null
  }
}
