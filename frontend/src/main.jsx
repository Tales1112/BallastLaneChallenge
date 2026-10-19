import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { api, refreshCsrf } from "./api";
import { Brand, Button } from "./components/shared";
import Auth from "./components/Auth";
import Workspace from "./components/Workspace";
import "./styles.css";

function App() {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [fatal, setFatal] = useState("");
  const [flash, setFlash] = useState("");
  async function initialize() {
    setLoading(true);
    setFatal("");
    try {
      await refreshCsrf();
      try {
        setUser(await api("/auth/me"));
      } catch (e) {
        if (e.status !== 401) throw e;
      }
    } catch (e) {
      setFatal(e.message);
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    initialize();
  }, []);
  if (loading)
    return (
      <main className="startup">
        <Brand />
        <span className="loading-ring" />
        <p>Making a little room for your day…</p>
      </main>
    );
  if (fatal)
    return (
      <main className="startup">
        <Brand />
        <p role="alert">{fatal}</p>
        <Button className="primary" onClick={initialize}>
          Try again
        </Button>
      </main>
    );
  return user ? (
    <Workspace
      user={user}
      onLogout={(message) => {
        setUser(null);
        setFlash(message || "");
      }}
    />
  ) : (
    <Auth
      onLogin={(value) => {
        setUser(value);
        setFlash("");
      }}
      flash={flash}
    />
  );
}
createRoot(document.getElementById("root")).render(<App />);
