import { queryClient } from './query'

export type Profile = {
  id: string
  email: string
  displayName: string
  roles: string[]
  permissions: string[]
}

type Session = { accessToken: string; expiresAt: string; user: Profile }

type Problem = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
  code?: string
  correlationId?: string
}

export class ApiError extends Error {
  constructor(public status: number, public problem: Problem) {
    super(problem.detail ?? problem.title ?? 'Không thể xử lý yêu cầu.')
  }
}

export type Page<T> = { items: T[]; pageNumber: number; pageSize: number; total: number }

let session: Session | null | undefined
let pendingRefresh: Promise<Session | null> | undefined
let sessionVersion = 0
let closingSession = false
const listeners = new Set<() => void>()

export const currentSession = () => session

export function onSessionChange(listener: () => void) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}

function publish(next: Session | null) {
  if (session?.user.id !== next?.user.id) {
    sessionVersion += 1
    queryClient.clear()
  }
  session = next
  listeners.forEach(listener => listener())
}

async function parse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as Problem
    throw new ApiError(response.status, {
      ...problem,
      correlationId: problem.correlationId ?? response.headers.get('X-Correlation-ID') ?? undefined,
    })
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

async function send(path: string, options: RequestInit = {}, accessToken?: string) {
  const headers = new Headers(options.headers)
  if (accessToken) headers.set('Authorization', 'Bearer ' + accessToken)
  if (options.body && typeof options.body === 'string') headers.set('Content-Type', 'application/json')
  try {
    return await fetch('/api/v1' + path, { ...options, headers, credentials: 'same-origin' })
  } catch (error) {
    if (options.signal?.aborted) throw error
    throw new ApiError(0, { title: 'Mất kết nối với máy chủ. Hãy thử lại.' })
  }
}

const cookieRequest = { method: 'POST', headers: { 'X-MiniErp-Session': '1' } }

export function refreshSession() {
  const version = sessionVersion
  return pendingRefresh ??= (async () => {
    const response = await send('/auth/refresh', cookieRequest)
    const next = response.status === 401 ? null : await parse<Session>(response)
    if (version !== sessionVersion) return null
    publish(next)
    return next
  })().finally(() => { pendingRefresh = undefined })
}

export async function login(email: string, password: string) {
  if (pendingRefresh) await pendingRefresh.catch(() => null)
  const response = await send('/auth/login', { ...cookieRequest, body: JSON.stringify({ email, password }) })
  publish(await parse<Session>(response))
}

export async function logout() {
  closingSession = true
  try {
    if (pendingRefresh) await pendingRefresh.catch(() => null)
    await parse<void>(await send('/auth/logout', cookieRequest, session?.accessToken))
    publish(null)
  } finally {
    closingSession = false
  }
}

export async function changePassword(input: { currentPassword: string; newPassword: string }) {
  await api<void>('/auth/change-password', { ...cookieRequest, body: JSON.stringify(input) })
  publish(null)
}

async function authorized(path: string, options?: RequestInit) {
  const original = session
  let response = await send(path, options, original?.accessToken)
  if (response.status === 401 && original && !closingSession && !options?.signal?.aborted) {
    const refreshed = original !== session ? session : await refreshSession()
    if (refreshed?.user.id === original.user.id) response = await send(path, options, refreshed.accessToken)
  }
  return response
}

export async function api<T>(path: string, options?: RequestInit) {
  return parse<T>(await authorized(path, options))
}

export async function download(path: string, filename: string) {
  const response = await authorized(path)
  if (!response.ok) await parse(response)
  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 3000)
}

export function json(method: 'POST' | 'PUT', value: unknown): RequestInit {
  return { method, body: JSON.stringify(value) }
}
