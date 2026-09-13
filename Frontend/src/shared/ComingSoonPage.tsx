interface ComingSoonPageProps {
  title: string
}

export function ComingSoonPage({ title }: ComingSoonPageProps) {
  return (
    <div className="flex h-full min-h-[60vh] flex-col items-center justify-center rounded-lg border border-dashed border-line text-center">
      <p className="font-serif text-xl font-semibold text-navy">{title}</p>
      <p className="mt-1 text-sm text-muted">This page is under construction.</p>
    </div>
  )
}
