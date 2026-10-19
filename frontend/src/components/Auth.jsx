import React, { useState } from "react";
import { api, refreshCsrf } from "../api";
import Icon from "./Icon";
import { Brand, Button } from "./shared";

export default function Auth({ onLogin, flash }) {
  const [register, setRegister] = useState(false);
  const [error, setError] = useState(flash || "");
  const [busy, setBusy] = useState(false);
  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    const values = Object.fromEntries(new FormData(event.currentTarget));
    try {
      const user = await api(`/auth/${register ? "register" : "login"}`, {
        method: "POST",
        body: JSON.stringify(values),
      });
      await refreshCsrf();
      onLogin(user);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="auth-shell">
      <section className="auth-story">
        <Brand />
        <div className="story-main">
          <span className="eyebrow">A LITTLE CLARITY GOES A LONG WAY</span>
          <h1>
            Make room for
            <br />
            your best work<span>.</span>
          </h1>
          <p>
            Big ideas start with small steps. Bring your tasks together, find
            your focus, and move forward.
          </p>
          <div className="story-art" aria-hidden="true">
            <div className="orbit orbit-one" />
            <div className="orbit orbit-two" />
            <div className="floating-task">
              <span className="art-check">
                <Icon name="check" />
              </span>
              <div>
                A fresh start, every day<small>One task at a time.</small>
              </div>
              <span className="spark">✦</span>
            </div>
            <div className="art-leaf">
              <Icon name="leaf" size={60} />
            </div>
          </div>
        </div>
        <span className="story-footer">Less noise. More progress.</span>
      </section>
      <section className="auth-panel">
        <div className="auth-form">
          <span className="small-label">YOUR PERSONAL WORKSPACE</span>
          <h2>{register ? "A fresh start." : "Welcome back."}</h2>
          <p className="muted">
            {register
              ? "Create an account and make space for what matters."
              : "A clear mind starts with a clear plan."}
          </p>
          <form onSubmit={submit}>
            {register && (
              <label>
                Full name
                <input
                  name="name"
                  autoComplete="name"
                  required
                  minLength={2}
                  maxLength={80}
                  placeholder="Alex Morgan"
                />
              </label>
            )}
            <label>
              Email address
              <input
                name="email"
                type="email"
                autoComplete="email"
                required
                maxLength={254}
                defaultValue={register ? "" : "demo@taskflow.local"}
                placeholder="you@example.com"
                key={String(register)}
              />
            </label>
            <label>
              Password
              <input
                name="password"
                type="password"
                autoComplete={register ? "new-password" : "current-password"}
                required
                minLength={register ? 10 : 1}
                maxLength={128}
                defaultValue={register ? "" : "DemoPass123!"}
                placeholder="Enter your password"
                key={String(register)}
              />
            </label>
            {register && (
              <small className="muted">
                At least 10 characters, with a letter and a number.
              </small>
            )}
            {error && (
              <div className="error" role="alert">
                {error}
              </div>
            )}
            <Button className="primary auth-submit" disabled={busy}>
              {busy
                ? "Please wait…"
                : register
                  ? "Create your account"
                  : "Open your workspace"}
              <Icon name="arrow" size={18} />
            </Button>
          </form>
          <p className="auth-switch">
            {register ? "Already have an account?" : "New to TaskFlow?"}{" "}
            <button
              onClick={() => {
                setRegister(!register);
                setError("");
              }}
            >
              {register ? "Sign in" : "Create an account"}
            </button>
          </p>
          {!register && (
            <div className="demo-note">
              <Icon name="lock" size={16} />
              <span>Demo credentials are filled in. Take a look around.</span>
            </div>
          )}
        </div>
        <span className="auth-footer">Your tasks. Your pace.</span>
      </section>
    </main>
  );
}
