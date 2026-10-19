let csrfToken;
export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}
export async function refreshCsrf() {
  const response = await fetch("/api/auth/csrf", {
    credentials: "same-origin",
  });
  if (!response.ok)
    throw new ApiError(
      "Could not establish a secure session. Please reload.",
      response.status,
    );
  csrfToken = (await response.json()).token;
}
export async function api(path, options = {}) {
  const method = options.method ?? "GET";
  const mutation = !["GET", "HEAD"].includes(method);
  if (mutation && !csrfToken) await refreshCsrf();
  const response = await fetch(`/api${path}`, {
    ...options,
    method,
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      ...(mutation ? { "X-CSRF-TOKEN": csrfToken } : {}),
      ...options.headers,
    },
  });
  if (!response.ok) {
    if (response.status === 401) csrfToken = undefined;
    const problem = await response.json().catch(() => ({}));
    const message =
      response.status === 429
        ? "Too many attempts. Please wait a minute and try again."
        : response.status === 401
          ? problem.title || "Your session has ended. Please sign in again."
          : Object.values(problem.errors ?? {}).flat()[0] ||
            problem.title ||
            "Something went wrong. Please try again.";
    throw new ApiError(message, response.status);
  }
  return response.status === 204 ? null : response.json();
}
