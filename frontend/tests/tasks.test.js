import test from "node:test";
import assert from "node:assert/strict";
import { filterTasks, isOverdue, localDate } from "../src/tasks.js";
const tasks = [
  {
    id: "a",
    title: "API review",
    description: "Inspect auth",
    status: "todo",
    dueDate: "2026-10-18",
    createdAt: "2026-10-17",
  },
  {
    id: "b",
    title: "Ship",
    description: "",
    status: "done",
    dueDate: "2026-10-17",
    createdAt: "2026-10-16",
  },
  {
    id: "c",
    title: "Plan",
    description: "Next release",
    status: "inProgress",
    dueDate: "2026-10-20",
    createdAt: "2026-10-18",
  },
  {
    id: "d",
    title: "Idea",
    description: "",
    status: "todo",
    dueDate: null,
    createdAt: "2026-10-15",
  },
];
test("views use local calendar dates and exclude completed upcoming items", () => {
  assert.deepEqual(
    filterTasks(tasks, { view: "today", today: "2026-10-18" }).map((x) => x.id),
    ["a"],
  );
  assert.deepEqual(
    filterTasks(tasks, { view: "upcoming", today: "2026-10-18" }).map(
      (x) => x.id,
    ),
    ["c"],
  );
  assert.deepEqual(
    filterTasks(tasks, { view: "completed" }).map((x) => x.id),
    ["b"],
  );
});
test("search and status filters compose", () => {
  assert.equal(
    filterTasks(tasks, { query: " AUTH ", status: "todo" })[0].id,
    "a",
  );
  assert.equal(filterTasks(tasks, { query: "AUTH", status: "done" }).length, 0);
});
test("due-date sort places undated items last without mutating input", () => {
  assert.deepEqual(
    filterTasks(tasks, { sort: "due" }).map((x) => x.id),
    ["b", "a", "c", "d"],
  );
  assert.equal(tasks[0].id, "a");
});
test("completed tasks and tasks without dates are never overdue", () => {
  assert.equal(isOverdue(tasks[1], "2026-10-18"), false);
  assert.equal(isOverdue(tasks[3], "2026-10-18"), false);
  assert.equal(isOverdue(tasks[0], "2026-10-19"), true);
  assert.equal(isOverdue(tasks[0], "2026-10-18"), false);
});
test("localDate uses local components rather than UTC conversion", () => {
  assert.equal(localDate(new Date(2026, 9, 18, 23, 59)), "2026-10-18");
});
