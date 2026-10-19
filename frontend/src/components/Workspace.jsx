import React, { useEffect, useState } from "react";
import { api, refreshCsrf } from "../api";
import { filterTasks, localDate, statusLabels } from "../tasks";
import Icon from "./Icon";
import { Brand, Button } from "./shared";
import { TaskForm, DeleteDialog } from "./TaskDialogs";
import TaskCard from "./TaskCard";

export default function Workspace({ user, onLogout }) {
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [view, setView] = useState("all");
  const [status, setStatus] = useState("all");
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState("newest");
  const [layout, setLayout] = useState("grid");
  const [editor, setEditor] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [notice, setNotice] = useState("");
  const [busyIds, setBusyIds] = useState([]);
  async function load() {
    setLoading(true);
    setError("");
    try {
      setTasks(await api("/tasks"));
    } catch (e) {
      if (e.status === 401) onLogout(e.message);
      else setError(e.message);
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    load();
  }, []);
  useEffect(() => {
    if (notice) {
      const timer = setTimeout(() => setNotice(""), 3500);
      return () => clearTimeout(timer);
    }
  }, [notice]);
  async function save(values, id) {
    try {
      const item = await api(id ? `/tasks/${id}` : "/tasks", {
        method: id ? "PUT" : "POST",
        body: JSON.stringify(values),
      });
      setTasks((previous) =>
        id
          ? previous.map((task) => (task.id === id ? item : task))
          : [item, ...previous],
      );
      setNotice(
        id ? "Changes saved. Keep moving." : "A new step, ready when you are.",
      );
    } catch (e) {
      if (e.status === 401) onLogout(e.message);
      throw e;
    }
  }
  async function remove(id) {
    try {
      await api(`/tasks/${id}`, { method: "DELETE" });
      setTasks((previous) => previous.filter((task) => task.id !== id));
      setNotice("Task deleted. A little more space.");
    } catch (e) {
      if (e.status === 401) onLogout(e.message);
      throw e;
    }
  }
  async function toggle(item) {
    setBusyIds((previous) => [...previous, item.id]);
    try {
      await save(
        { ...item, status: item.status === "done" ? "todo" : "done" },
        item.id,
      );
    } catch (e) {
      setError(e.message);
    } finally {
      setBusyIds((previous) => previous.filter((id) => id !== item.id));
    }
  }
  const visible = filterTasks(tasks, { view, status, query, sort });
  const completed = tasks.filter((task) => task.status === "done").length;
  const inProgress = tasks.filter(
    (task) => task.status === "inProgress",
  ).length;
  const todo = tasks.filter((task) => task.status === "todo").length;
  const percent = tasks.length
    ? Math.round((completed / tasks.length) * 100)
    : 0;
  const names = {
    all: "All tasks",
    today: "Today",
    upcoming: "Upcoming",
    completed: "Completed",
  };
  const navigation = [
    ["all", "grid", "All tasks", tasks.length],
    [
      "today",
      "sun",
      "Today",
      tasks.filter((task) => task.dueDate === localDate()).length,
    ],
    [
      "upcoming",
      "calendar",
      "Upcoming",
      filterTasks(tasks, { view: "upcoming" }).length,
    ],
    ["completed", "check", "Completed", completed],
  ];
  return (
    <div className="workspace">
      <aside className="sidebar">
        <Brand />
        <div className="workspace-name">
          <span className="workspace-avatar">P</span>
          <span>
            Personal workspace<small>A space for your next step</small>
          </span>
        </div>
        <span className="nav-label">WORKSPACE</span>
        <nav aria-label="Task views">
          {navigation.map(([key, icon, label, count]) => (
            <button
              key={key}
              className={`nav-item ${view === key ? "active" : ""}`}
              aria-current={view === key ? "page" : undefined}
              onClick={() => {
                setView(key);
                setStatus("all");
              }}
            >
              <Icon name={icon} size={19} />
              <span>{label}</span>
              <small>{count}</small>
            </button>
          ))}
        </nav>
        <div className="sidebar-note">
          <Icon name="leaf" size={26} />
          <h3>
            Small steps.
            <br />
            Meaningful progress.
          </h3>
          <p>
            You don’t have to do it all.
            <br />
            Just the next right thing.
          </p>
          <span>
            YOU’VE GOT THIS <Icon name="arrow" size={14} />
          </span>
        </div>
        <div className="profile">
          <span className="avatar">
            {user.name
              .split(" ")
              .slice(0, 2)
              .map((name) => name[0])
              .join("")}
          </span>
          <div>
            <strong>{user.name}</strong>
            <small>Personal account</small>
          </div>
          <button
            className="icon-button"
            title="Sign out"
            aria-label="Sign out"
            onClick={async () => {
              try {
                await api("/auth/logout", { method: "POST" });
                await refreshCsrf();
                onLogout();
              } catch (e) {
                setError(e.message);
              }
            }}
          >
            <Icon name="logout" size={18} />
          </button>
        </div>
      </aside>
      <div className="main-shell">
        <header className="topbar">
          <div className="breadcrumb">
            My workspace<span>/</span>
            <strong>{names[view]}</strong>
          </div>
          <div className="topbar-right">
            <span className="private-label">
              <Icon name="lock" size={13} />
              Private workspace
            </span>
            <span className="topbar-date">
              {new Date().toLocaleDateString("en-US", {
                month: "short",
                day: "numeric",
                year: "numeric",
              })}
            </span>
          </div>
        </header>
        <main className="dashboard">
          <div className="page-heading">
            <div>
              <span className="small-label">A CLEAR SPACE FOR WHAT’S NEXT</span>
              <h1>
                Your work, in focus<span>.</span>
              </h1>
              <p>
                Welcome back, {user.name.split(" ")[0]}. Let’s make a little
                progress.
              </p>
            </div>
            <Button className="primary" onClick={() => setEditor({})}>
              <Icon name="plus" size={18} />
              New task
            </Button>
          </div>
          <section className="overview" aria-label="Task summary">
            <div className="progress-card">
              <div>
                <span className="eyebrow">A LITTLE EVERY DAY</span>
                <h2>Progress looks good on you.</h2>
                <p>
                  {completed === tasks.length && tasks.length
                    ? "Everything is checked off. Enjoy a little breathing room."
                    : "Every task you finish makes room for what’s next."}
                </p>
                <div className="progress-meta">
                  <span>
                    {completed} of {tasks.length} tasks completed
                  </span>
                  <strong>{percent}%</strong>
                </div>
                <progress
                  value={completed}
                  max={tasks.length || 1}
                  aria-label="Task completion"
                />
              </div>
              <div className="progress-art" aria-hidden="true">
                <span className="art-orbit" />
                <span className="big-check">
                  <Icon name="check" size={43} />
                </span>
                <span className="tiny-spark">✦</span>
              </div>
            </div>
            <div className="stat-card">
              <span className="stat-icon todo">
                <Icon name="list" />
              </span>
              <strong>{String(todo).padStart(2, "0")}</strong>
              <span>To do</span>
              <small>A fresh opportunity</small>
            </div>
            <div className="stat-card">
              <span className="stat-icon inProgress">
                <Icon name="clock" />
              </span>
              <strong>{String(inProgress).padStart(2, "0")}</strong>
              <span>In progress</span>
              <small>You’re making moves</small>
            </div>
          </section>
          <section className="tasks-section" aria-labelledby="tasks-title">
            <div className="section-heading">
              <div>
                <h2 id="tasks-title">
                  {names[view]}
                  <span>{visible.length}</span>
                </h2>
                <p>
                  {view === "completed"
                    ? "A little proof of how far you’ve come."
                    : view === "today"
                      ? "A little focus for the day ahead."
                      : view === "upcoming"
                        ? "A little planning for what’s on the horizon."
                        : "A home for everything on your mind."}
                </p>
              </div>
              <div className="layout-switch" aria-label="Task layout">
                <button
                  className={layout === "grid" ? "selected" : ""}
                  onClick={() => setLayout("grid")}
                  aria-label="Grid view"
                  aria-pressed={layout === "grid"}
                >
                  <Icon name="grid" size={17} />
                </button>
                <button
                  className={layout === "list" ? "selected" : ""}
                  onClick={() => setLayout("list")}
                  aria-label="List view"
                  aria-pressed={layout === "list"}
                >
                  <Icon name="list" size={18} />
                </button>
              </div>
            </div>
            <div className="toolbar">
              <div
                className="status-tabs"
                role="group"
                aria-label="Filter by status"
              >
                {[["all", "All"], ...Object.entries(statusLabels)].map(
                  ([key, label]) => (
                    <button
                      key={key}
                      className={status === key ? "selected" : ""}
                      aria-pressed={status === key}
                      onClick={() => setStatus(key)}
                    >
                      {label}
                    </button>
                  ),
                )}
              </div>
              <div className="search-sort">
                <label className="search">
                  <Icon name="search" size={17} />
                  <input
                    type="search"
                    aria-label="Search tasks"
                    placeholder="Search tasks…"
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                  />
                </label>
                <select
                  className="sort-select"
                  aria-label="Sort tasks"
                  value={sort}
                  onChange={(event) => setSort(event.target.value)}
                >
                  <option value="newest">Newest first</option>
                  <option value="due">Due date</option>
                </select>
              </div>
            </div>
            {error && (
              <div className="error" role="alert">
                {error}
                <button onClick={load}>Try again</button>
              </div>
            )}
            {loading ? (
              <div className="empty-state" role="status">
                <span className="loading-ring" />
                <h3>Gathering your next steps…</h3>
              </div>
            ) : visible.length ? (
              <div className={`tasks ${layout}`}>
                {visible.map((item) => (
                  <TaskCard
                    key={item.id}
                    item={item}
                    layout={layout}
                    onEdit={setEditor}
                    onDelete={setDeleting}
                    onToggle={toggle}
                    busy={busyIds.includes(item.id)}
                  />
                ))}
              </div>
            ) : (
              <div className="empty-state">
                <span className="empty-icon">
                  <Icon name={query ? "search" : "leaf"} size={30} />
                </span>
                <h3>
                  {query || status !== "all"
                    ? "A little too quiet here."
                    : view === "completed"
                      ? "Your progress will live here."
                      : "A little space for your next idea."}
                </h3>
                <p>
                  {query || status !== "all"
                    ? "Try a different search or status filter."
                    : view === "completed"
                      ? "Complete a task to see it here."
                      : "Create a task and take the first small step."}
                </p>
                {view !== "completed" && !query && status === "all" && (
                  <Button className="secondary" onClick={() => setEditor({})}>
                    <Icon name="plus" size={16} />
                    Create a task
                  </Button>
                )}
              </div>
            )}
            <div className="list-footer">
              <span>
                {loading
                  ? "Loading your workspace"
                  : `Showing ${visible.length} of ${tasks.length} tasks`}
              </span>
              <span>
                <span className="live-dot" />
                All changes saved automatically
              </span>
            </div>
          </section>
          <footer className="page-footer">
            <Brand />
            <span>A little more focus. A little less noise.</span>
          </footer>
        </main>
      </div>
      {editor && (
        <TaskForm
          item={editor.id ? editor : null}
          onClose={() => setEditor(null)}
          onSave={save}
        />
      )}
      {deleting && (
        <DeleteDialog
          item={deleting}
          onClose={() => setDeleting(null)}
          onDelete={remove}
        />
      )}
      {notice && (
        <div className="toast" role="status">
          <span>
            <Icon name="check" size={16} />
          </span>
          {notice}
        </div>
      )}
    </div>
  );
}
