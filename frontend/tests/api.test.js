import test from "node:test";
import assert from "node:assert/strict";

test("expired authentication discards its CSRF token before the next mutation", async () => {
  const { api } = await import("../src/api.js?session-regression");
  const originalFetch = globalThis.fetch;
  const requests = [];
  const responses = [
    new Response(JSON.stringify({ token: "authenticated-token" })),
    new Response(null, { status: 204 }),
    new Response(null, { status: 401 }),
    new Response(JSON.stringify({ token: "anonymous-token" })),
    new Response(JSON.stringify({ id: "signed-in-user" })),
  ];
  globalThis.fetch = async (url, options) => {
    requests.push({ url, options });
    return responses.shift();
  };
  try {
    await api("/tasks/task-id", { method: "DELETE" });
    await assert.rejects(api("/tasks"), (error) => error.status === 401);
    await api("/auth/login", { method: "POST", body: "{}" });
    assert.deepEqual(
      requests.map((x) => x.url),
      [
        "/api/auth/csrf",
        "/api/tasks/task-id",
        "/api/tasks",
        "/api/auth/csrf",
        "/api/auth/login",
      ],
    );
    assert.equal(
      requests[1].options.headers["X-CSRF-TOKEN"],
      "authenticated-token",
    );
    assert.equal(
      requests[4].options.headers["X-CSRF-TOKEN"],
      "anonymous-token",
    );
    assert.equal(requests[4].options.credentials, "same-origin");
  } finally {
    globalThis.fetch = originalFetch;
  }
});
