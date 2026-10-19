export const statusLabels = {
  todo: "To do",
  inProgress: "In progress",
  done: "Completed",
};
export function localDate(date = new Date()) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}
export function isOverdue(task, today = localDate()) {
  return Boolean(
    task.dueDate && task.dueDate < today && task.status !== "done",
  );
}
export function filterTasks(
  tasks,
  {
    view = "all",
    status = "all",
    query = "",
    sort = "newest",
    today = localDate(),
  } = {},
) {
  const normalized = query.trim().toLowerCase();
  const items = tasks.filter(
    (task) =>
      (view !== "today" || task.dueDate === today) &&
      (view !== "upcoming" ||
        (task.dueDate && task.dueDate > today && task.status !== "done")) &&
      (view !== "completed" || task.status === "done") &&
      (status === "all" || task.status === status) &&
      (!normalized ||
        `${task.title} ${task.description}`.toLowerCase().includes(normalized)),
  );
  return items.sort((a, b) =>
    sort === "due"
      ? (a.dueDate || "9999").localeCompare(b.dueDate || "9999") ||
        b.createdAt.localeCompare(a.createdAt)
      : b.createdAt.localeCompare(a.createdAt),
  );
}
