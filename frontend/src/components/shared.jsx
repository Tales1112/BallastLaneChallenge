import React from "react";
import Icon from "./Icon";

export function Brand() {
  return (
    <div className="brand">
      <span className="brand-mark">
        <Icon name="check" size={24} />
      </span>
      TaskFlow<span className="brand-dot">.</span>
    </div>
  );
}
export function Button({ children, className = "", ...props }) {
  return (
    <button className={`button ${className}`} {...props}>
      {children}
    </button>
  );
}
