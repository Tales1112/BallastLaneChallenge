import React, { useEffect, useRef, useState } from "react";
import { statusLabels } from "../tasks";
import Icon from "./Icon";
import { Button } from "./shared";

function Modal({ title, children, onClose }) {
  const dialog = useRef(null);
  useEffect(() => {
    const node = dialog.current;
    const previous = document.activeElement;
    node.showModal();
    node.querySelector("input")?.focus();
    return () => {
      node.close();
      previous?.focus();
    };
  }, []);
  return (
    <dialog
      ref={dialog}
      className="modal"
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        if (event.target === dialog.current) onClose();
      }}
      aria-labelledby="modal-title"
    >
      <header>
        <div>
          <span className="small-label">MAKE A LITTLE PROGRESS</span>
          <h2 id="modal-title">{title}</h2>
        </div>
        <button
          className="icon-button"
          onClick={onClose}
          aria-label="Close dialog"
        >
          <Icon name="close" />
        </button>
      </header>
      {children}
    </dialog>
  );
}
export function TaskForm({ item, onClose, onSave }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await onSave(
        Object.fromEntries(new FormData(event.currentTarget)),
        item?.id,
      );
      onClose();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title={item ? "A little fine-tuning." : "What’s next?"}
      onClose={busy ? () => {} : onClose}
    >
      <form onSubmit={submit}>
        <label>
          Task title
          <input
            name="title"
            autoFocus
            required
            maxLength={120}
            defaultValue={item?.title || ""}
            placeholder="Give your next step a name"
          />
        </label>
        <label>
          Description <span className="optional">optional</span>
          <textarea
            name="description"
            rows={4}
            maxLength={2000}
            defaultValue={item?.description || ""}
            placeholder="A few details to help you get started…"
          />
        </label>
        <div className="form-row">
          <label>
            Status
            <select name="status" defaultValue={item?.status || "todo"}>
              {Object.entries(statusLabels).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Due date <span className="optional">optional</span>
            <input
              name="dueDate"
              type="date"
              defaultValue={item?.dueDate || ""}
            />
          </label>
        </div>
        {error && (
          <div className="error" role="alert">
            {error}
          </div>
        )}
        <footer>
          <Button
            type="button"
            className="secondary"
            onClick={onClose}
            disabled={busy}
          >
            Cancel
          </Button>
          <Button className="primary" disabled={busy}>
            {busy ? "Saving…" : item ? "Save changes" : "Create task"}
            <Icon name="arrow" size={17} />
          </Button>
        </footer>
      </form>
    </Modal>
  );
}
export function DeleteDialog({ item, onClose, onDelete }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return (
    <Modal title="Let this one go?" onClose={busy ? () => {} : onClose}>
      <p className="muted">“{item.title}” will be permanently deleted.</p>
      {error && (
        <div className="error" role="alert">
          {error}
        </div>
      )}
      <footer>
        <Button className="secondary" disabled={busy} onClick={onClose}>
          Keep task
        </Button>
        <Button
          className="danger"
          disabled={busy}
          onClick={async () => {
            setBusy(true);
            try {
              await onDelete(item.id);
              onClose();
            } catch (e) {
              setError(e.message);
            } finally {
              setBusy(false);
            }
          }}
        >
          {busy ? "Deleting…" : "Delete task"}
        </Button>
      </footer>
    </Modal>
  );
}
