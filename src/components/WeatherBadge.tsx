import type { WeatherForecast } from '../api/client'

interface WeatherBadgeProps {
  forecast: WeatherForecast | null
  loading: boolean
}

/**
 * Mapper Open-Meteo sin WMO weather_code til (emoji, tekstlig beskrivelse).
 * Tabell: https://open-meteo.com/en/docs#weathervariables
 */
function describeWeatherCode(code: number): { icon: string; label: string } {
  if (code === 0) return { icon: '☀️', label: 'Klart' }
  if (code === 1) return { icon: '🌤️', label: 'For det meste klart' }
  if (code === 2) return { icon: '⛅', label: 'Delvis skyet' }
  if (code === 3) return { icon: '☁️', label: 'Overskyet' }
  if (code === 45 || code === 48) return { icon: '🌫️', label: 'Tåke' }
  if (code >= 51 && code <= 57) return { icon: '🌦️', label: 'Yr' }
  if (code >= 61 && code <= 67) return { icon: '🌧️', label: 'Regn' }
  if (code >= 71 && code <= 77) return { icon: '🌨️', label: 'Snø' }
  if (code >= 80 && code <= 82) return { icon: '🌧️', label: 'Regnbyger' }
  if (code === 85 || code === 86) return { icon: '🌨️', label: 'Snøbyger' }
  if (code >= 95 && code <= 99) return { icon: '⛈️', label: 'Tordenvær' }
  return { icon: '🌡️', label: 'Vær' }
}

function formatTemperature(value: number | null): string | null {
  if (value === null || value === undefined || Number.isNaN(value)) return null
  return `${Math.round(value)}°`
}

export default function WeatherBadge({ forecast, loading }: WeatherBadgeProps) {
  if (loading) {
    return (
      <span
        aria-hidden
        className="inline-block h-3.5 w-3.5 animate-pulse rounded-full"
        style={{ backgroundColor: 'var(--color-border)' }}
      />
    )
  }

  if (!forecast) {
    return (
      <span
        title="Værvarsel er tilgjengelig fra ca. 16 dager før kampen"
        aria-label="Værvarsel ikke tilgjengelig ennå"
        className="inline-flex items-center text-[11px] leading-none opacity-50"
        style={{ color: 'var(--color-text-muted)' }}
      >
        <span aria-hidden>🌡️</span>
      </span>
    )
  }

  const { icon, label } = describeWeatherCode(forecast.weatherCode)
  const tempMax = formatTemperature(forecast.tempMaxC)
  const tempMin = formatTemperature(forecast.tempMinC)
  const tempText = tempMax && tempMin ? `${tempMin} / ${tempMax}` : tempMax ?? tempMin

  const titleParts: string[] = [label]
  if (tempText) titleParts.push(tempText)
  if (forecast.precipitationProbabilityPct !== null && forecast.precipitationProbabilityPct !== undefined) {
    titleParts.push(`${forecast.precipitationProbabilityPct}% nedbør`)
  } else if (forecast.precipitationMm !== null && forecast.precipitationMm !== undefined && forecast.precipitationMm > 0) {
    titleParts.push(`${forecast.precipitationMm.toFixed(1)} mm`)
  }

  return (
    <span
      title={titleParts.join(' · ')}
      aria-label={`Vær på stadion: ${titleParts.join(', ')}`}
      className="inline-flex items-center gap-0.5 text-[11px] leading-none"
      style={{ color: 'var(--color-text-muted)' }}
    >
      <span aria-hidden>{icon}</span>
      {tempMax ? <span className="font-medium">{tempMax}</span> : null}
    </span>
  )
}
