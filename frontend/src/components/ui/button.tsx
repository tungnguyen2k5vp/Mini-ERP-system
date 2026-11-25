import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { clsx } from 'clsx'
import type { ComponentProps } from 'react'
import { twMerge } from 'tailwind-merge'

// Adapted from the shadcn/ui Button recipe, MIT.
const buttonVariants = cva('button', {
  variants: { variant: { primary: 'button-primary', outline: 'button-outline', ghost: 'button-ghost' } },
  defaultVariants: { variant: 'primary' },
})

export function Button({ asChild = false, className, variant, ...props }:
  ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }) {
  const Component = asChild ? Slot : 'button'
  return <Component className={twMerge(clsx(buttonVariants({ variant }), className))} {...props} />
}
