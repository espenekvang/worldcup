import type { TvChannel } from '../types'

interface ChannelLogoProps {
  channel: TvChannel
  className?: string
}

// Merkevarefarger for de norske VM-rettighetshaverne.
const CHANNEL_STYLE: Record<TvChannel, { bg: string; fg: string; label: string }> = {
  NRK: { bg: '#0064b4', fg: '#ffffff', label: 'NRK' },
  TV2: { bg: '#e30613', fg: '#ffffff', label: 'TV 2' },
}

/** Liten kanal-logo som viser hvilken norsk TV-kanal som sender kampen. */
export default function ChannelLogo({ channel, className }: ChannelLogoProps) {
  const style = CHANNEL_STYLE[channel]

  return (
    <span
      className={`inline-flex items-center rounded-sm px-1 py-px text-[9px] font-bold leading-none tracking-tight ${className ?? ''}`}
      style={{ backgroundColor: style.bg, color: style.fg }}
      title={`Sendes på ${style.label}`}
      aria-label={`Sendes på ${style.label}`}
    >
      {style.label}
    </span>
  )
}
