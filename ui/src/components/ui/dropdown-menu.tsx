import * as DropdownMenuPrimitive from '@radix-ui/react-dropdown-menu'
import { Check, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'

export const DropdownMenu = DropdownMenuPrimitive.Root
export const DropdownMenuTrigger = DropdownMenuPrimitive.Trigger
export const DropdownMenuSub = DropdownMenuPrimitive.Sub

const itemBase =
  'flex cursor-pointer select-none items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm outline-none transition-colors focus:bg-muted data-[highlighted]:bg-muted'

export function DropdownMenuContent({
  className,
  sideOffset = 8,
  ...props
}: React.ComponentProps<typeof DropdownMenuPrimitive.Content>) {
  return (
    <DropdownMenuPrimitive.Portal>
      <DropdownMenuPrimitive.Content
        sideOffset={sideOffset}
        className={cn(
          'z-50 min-w-56 rounded-xl border border-border bg-background p-1.5 text-foreground shadow-[0_12px_36px_rgb(24_24_27/0.15)]',
          'data-[state=open]:animate-in data-[state=open]:fade-in-0',
          className,
        )}
        {...props}
      />
    </DropdownMenuPrimitive.Portal>
  )
}

export function DropdownMenuItem({ className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Item>) {
  return <DropdownMenuPrimitive.Item className={cn(itemBase, className)} {...props} />
}

export function DropdownMenuLabel({ className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Label>) {
  return <DropdownMenuPrimitive.Label className={cn('px-2.5 py-2 text-xs text-muted-foreground', className)} {...props} />
}

export function DropdownMenuSeparator({ className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Separator>) {
  return <DropdownMenuPrimitive.Separator className={cn('my-1.5 h-px bg-border', className)} {...props} />
}

export function DropdownMenuSubTrigger({ className, children, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.SubTrigger>) {
  return (
    <DropdownMenuPrimitive.SubTrigger className={cn(itemBase, className)} {...props}>
      {children}
      <ChevronRight className="ms-auto size-4 text-faint rtl:rotate-180" />
    </DropdownMenuPrimitive.SubTrigger>
  )
}

export function DropdownMenuSubContent({ className, sideOffset = 6, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.SubContent>) {
  return (
    <DropdownMenuPrimitive.Portal>
      <DropdownMenuPrimitive.SubContent
        sideOffset={sideOffset}
        className={cn('z-50 min-w-48 rounded-xl border border-border bg-background p-1.5 text-foreground shadow-[0_12px_36px_rgb(24_24_27/0.15)]', className)}
        {...props}
      />
    </DropdownMenuPrimitive.Portal>
  )
}

/** A menu row that shows a trailing check when selected (language options). */
export function DropdownMenuCheckItem({
  selected,
  children,
  className,
  ...props
}: React.ComponentProps<typeof DropdownMenuPrimitive.Item> & { selected?: boolean }) {
  return (
    <DropdownMenuPrimitive.Item className={cn(itemBase, selected && 'font-semibold', className)} {...props}>
      {children}
      {selected && <Check className="ms-auto size-4" />}
    </DropdownMenuPrimitive.Item>
  )
}
