import React from "react";
import { statusLabels, isOverdue, localDate } from "../tasks";
import Icon from "./Icon";

function DueDate({ item }) {
  if (!item.dueDate)
    return (
      <span className="due-date no-date">
        <Icon name="calendar" size={14} />
        No due date
      </span>
    );
  const overdue = isOverdue(item);
  const today = item.dueDate === localDate();
  return (
    <span
      className={`due-date ${overdue ? "overdue" : today && item.status !== "done" ? "today" : ""}`}
    >
      <Icon name="calendar" size={14} />
      {today
        ? "Today"
        : new Date(`${item.dueDate}T12:00:00`).toLocaleDateString("en-US", {
            month: "short",
            day: "numeric",
            year:
              item.dueDate.slice(0, 4) !== String(new Date().getFullYear())
                ? "numeric"
                : undefined,
          })}
      {overdue && <span className="overdue-dot" />}
    </span>
  );
}
export default function TaskCard({
  item,
  layout,
  onEdit,
  onDelete,
  onToggle,
  busy,
}) {
  return (
    <article
      className={`task-card ${layout} ${item.status === "done" ? "is-done" : ""}`}
    >
      <div className="task-top">
        <span className={`status ${item.status}`}>
          <span />
          {statusLabels[item.status]}
        </span>
        <div className="card-actions">
          <button
            className="icon-button"
            onClick={() => onEdit(item)}
            aria-label={`Edit ${item.title}`}
          >
            <Icon name="edit" size={15} />
          </button>
          <button
            className="icon-button delete-action"
            onClick={() => onDelete(item)}
            aria-label={`Delete ${item.title}`}
          >
            <Icon name="trash" size={15} />
          </button>
        </div>
      </div>
      <div className="task-content">
        <h3>{item.title}</h3>
        <p>{item.description || "A small step worth taking."}</p>
      </div>
      <div className="task-bottom">
        <DueDate item={item} />
        <button
          className={`complete-button ${item.status === "done" ? "checked" : ""}`}
          onClick={() => onToggle(item)}
          disabled={busy}
          aria-label={`${item.status === "done" ? "Reopen" : "Complete"} ${item.title}`}
          aria-pressed={item.status === "done"}
        >
          <Icon name="check" size={15} />
        </button>
      </div>
    </article>
  );
}
