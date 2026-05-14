import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'

function cx(...classes: (string | false | undefined)[]) {
  return classes.filter(Boolean).join(' ')
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'ghost' }

export function Button({ variant = 'primary', className, ...props }: ButtonProps) {
  return (
    <button
      className={cx(
        'inline-flex items-center justify-center rounded-md px-4 py-2 text-sm font-medium transition-colors',
        'disabled:cursor-not-allowed disabled:opacity-50',
        variant === 'primary' && 'bg-brand-500 text-white hover:bg-brand-600',
        variant === 'secondary' && 'border border-slate-300 bg-white text-slate-800 hover:bg-slate-100',
        variant === 'ghost' && 'text-slate-600 hover:bg-slate-100',
        className,
      )}
      {...props}
    />
  )
}

export function Input({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cx(
        'w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm',
        'focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20',
        className,
      )}
      {...props}
    />
  )
}

export function Label({ children, htmlFor }: { children: ReactNode; htmlFor?: string }) {
  return (
    <label htmlFor={htmlFor} className="mb-1 block text-sm font-medium text-slate-700">
      {children}
    </label>
  )
}

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx('rounded-lg border border-slate-200 bg-white p-6 shadow-sm', className)}>{children}</div>
}

const statusColours: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  Scheduled: 'bg-amber-100 text-amber-800',
  Live: 'bg-emerald-100 text-emerald-800',
  Ended: 'bg-slate-200 text-slate-600',
}

export function StatusBadge({ status }: { status: string }) {
  return (
    <span className={cx('rounded-full px-2 py-0.5 text-xs font-medium', statusColours[status] ?? 'bg-slate-100')}>
      {status}
    </span>
  )
}
