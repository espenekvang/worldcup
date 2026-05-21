import { useEffect, useState } from 'react'
import { getWeatherForVenue, type WeatherForecast } from '../api/client'

/**
 * Returnerer datoen for en kamp i stadionets lokale tidssone som "yyyy-MM-dd".
 * Bruker Intl.DateTimeFormat med IANA-tidssonen fra venue, så vi unngår
 * å bomme på dato når kampen spilles like før/etter midnatt UTC.
 */
export function localMatchDate(isoUtc: string, timezone: string): string {
  const date = new Date(isoUtc)
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: timezone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(date)

  const year = parts.find(p => p.type === 'year')?.value
  const month = parts.find(p => p.type === 'month')?.value
  const day = parts.find(p => p.type === 'day')?.value
  return `${year}-${month}-${day}`
}

type CacheEntry = WeatherForecast | null
const memoryCache = new Map<string, CacheEntry>()
const inFlight = new Map<string, Promise<CacheEntry>>()

export interface UseWeatherResult {
  forecast: WeatherForecast | null
  loading: boolean
}

/**
 * Henter værvarsel for en kamp. Returnerer `null` når det ikke finnes prognose
 * (typisk for kamper > 16 dager frem i tid eller ukjent stadion).
 * Cache er prosess-lokal — backendet har sin egen cache for selve Open-Meteo-kallene.
 */
export function useWeather(venueId: string | undefined, isoUtc: string, timezone: string | undefined): UseWeatherResult {
  const key = venueId && timezone ? `${venueId}:${localMatchDate(isoUtc, timezone)}` : null
  const [forecast, setForecast] = useState<WeatherForecast | null>(() =>
    key !== null && memoryCache.has(key) ? (memoryCache.get(key) ?? null) : null,
  )
  const [loading, setLoading] = useState<boolean>(() => key !== null && !memoryCache.has(key))

  useEffect(() => {
    if (!key || !venueId || !timezone) {
      setForecast(null)
      setLoading(false)
      return
    }

    if (memoryCache.has(key)) {
      setForecast(memoryCache.get(key) ?? null)
      setLoading(false)
      return
    }

    let cancelled = false
    setLoading(true)

    let promise = inFlight.get(key)
    if (!promise) {
      const date = localMatchDate(isoUtc, timezone)
      promise = getWeatherForVenue(venueId, date)
        .then((result): CacheEntry => result)
        .catch((): CacheEntry => null)
        .then(result => {
          memoryCache.set(key, result)
          inFlight.delete(key)
          return result
        })
      inFlight.set(key, promise)
    }

    promise.then(result => {
      if (!cancelled) {
        setForecast(result)
        setLoading(false)
      }
    })

    return () => {
      cancelled = true
    }
  }, [key, venueId, isoUtc, timezone])

  return { forecast, loading }
}
