import { PointerEvent, ReactNode, useRef, useState, type CSSProperties } from "react";

type Props = {
  disabled?: boolean;
  className?: string;
  style?: CSSProperties;
  onSwipeRight: () => void;
  children: ReactNode;
};

export function SwipeAway({ disabled, className, style, onSwipeRight, children }: Props) {
  const startX = useRef(0);
  const startY = useRef(0);
  const dx = useRef(0);
  const tracking = useRef(false);
  const [shift, setShift] = useState(0);

  function reset() {
    tracking.current = false;
    dx.current = 0;
    setShift(0);
  }

  function onPointerDown(event: PointerEvent<HTMLDivElement>) {
    if (disabled || (event.target as HTMLElement).closest("input, button")) {
      return;
    }

    tracking.current = true;
    startX.current = event.clientX;
    startY.current = event.clientY;
    dx.current = 0;
    event.currentTarget.setPointerCapture(event.pointerId);
  }

  function onPointerMove(event: PointerEvent<HTMLDivElement>) {
    if (!tracking.current) {
      return;
    }

    const x = event.clientX - startX.current;
    const y = event.clientY - startY.current;
    if (Math.abs(y) > 24 && Math.abs(y) > Math.abs(x)) {
      reset();
      return;
    }

    dx.current = Math.max(0, x);
    setShift(dx.current);
  }

  function onPointerUp() {
    if (!tracking.current) {
      return;
    }

    const shouldRemove = dx.current > 72;
    reset();
    if (shouldRemove) {
      onSwipeRight();
    }
  }

  return (
    <div
      className={className}
      style={{ ...style, transform: shift ? `translateX(${Math.min(shift, 140)}px)` : undefined }}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={reset}
    >
      {children}
    </div>
  );
}
