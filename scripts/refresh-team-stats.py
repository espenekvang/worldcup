#!/usr/bin/env python3
"""Henter ekte lag-data fra Wikipedia og oppdaterer api/WorldCup.Api/data/teamStats.json.

Strategi:
  1) FIFA-rangering: hentes fra Module:SportsRankings/data/FIFA World Rankings
     på en.wikipedia.org. Dataene oppdateres månedlig av Wikipedia-frivillige
     rett etter at FIFA publiserer en ny rangering.
  2) Manager + nøkkelspiller: hentes fra hver landslag-side sin infoboks
     (felt 'Coach' og 'Top scorer' / 'Captain').
  3) Formasjon + forrige VM: beholdes fra eksisterende seed-fil (statisk,
     trenger ikke oppdateres ofte).
  4) Form / recent matches / H2H: beholdes fra eksisterende seed. Det finnes
     ingen pålitelig gratis kilde for dette på landslag-nivå — fyll inn
     manuelt før VM hvis du vil ha ferske tall.

Bruk:
    python3 scripts/refresh-team-stats.py [--dry-run]

Scriptet er idempotent: kjør så ofte du vil. Felt som ikke kunne hentes
beholder eksisterende seed-verdier.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SEED_PATH = REPO_ROOT / "api" / "WorldCup.Api" / "Data" / "teamStats.json"

USER_AGENT = "WorldCupTippeApp/1.0 (refresh-team-stats; contact: hobby)"

# FIFA-kode → engelsk Wikipedia-navn (uten "national football team"-suffiks).
# Brukes både for å slå opp rank i Wikipedia-modulen og for å bygge URL til
# landslagsartikkelen. Holdes synkronisert med src/data/teams.json.
FIFA_TO_NAME: dict[str, str] = {
    "ALG": "Algeria",
    "ARG": "Argentina",
    "AUS": "Australia",
    "AUT": "Austria",
    "BEL": "Belgium",
    "BIH": "Bosnia and Herzegovina",
    "BRA": "Brazil",
    "CAN": "Canada",
    "CIV": "Ivory Coast",
    "COD": "DR Congo",
    "COL": "Colombia",
    "CPV": "Cape Verde",
    "CRO": "Croatia",
    "CUW": "Curaçao",
    "CZE": "Czech Republic",
    "ECU": "Ecuador",
    "EGY": "Egypt",
    "ENG": "England",
    "ESP": "Spain",
    "FRA": "France",
    "GER": "Germany",
    "GHA": "Ghana",
    "HAI": "Haiti",
    "IRN": "Iran",
    "IRQ": "Iraq",
    "JOR": "Jordan",
    "JPN": "Japan",
    "KOR": "South Korea",
    "KSA": "Saudi Arabia",
    "MAR": "Morocco",
    "MEX": "Mexico",
    "NED": "Netherlands",
    "NOR": "Norway",
    "NZL": "New Zealand",
    "PAN": "Panama",
    "PAR": "Paraguay",
    "POR": "Portugal",
    "QAT": "Qatar",
    "RSA": "South Africa",
    "SCO": "Scotland",
    "SEN": "Senegal",
    "SUI": "Switzerland",
    "SWE": "Sweden",
    "TUN": "Tunisia",
    "TUR": "Turkey",
    "URU": "Uruguay",
    "USA": "United States",
    "UZB": "Uzbekistan",
}


def http_get(url: str) -> str:
    """Enkel HTTP GET med User-Agent — Wikipedia krever det."""
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=20) as resp:
        return resp.read().decode("utf-8", errors="replace")


def fetch_fifa_rankings() -> dict[str, int]:
    """Returnerer { engelsk-landsnavn: rank } fra Wikipedia-modulen.

    Modulen er en Lua-tabell på formen
        { "France", 1, 2, 1877.32 },
        { "Spain",  2, -1, 1876.40 },
        ...
    Vi regex-parser linjene — Lua-syntaks er enkel nok til at en full Lua-
    parser er overkill.
    """
    url = "https://en.wikipedia.org/w/index.php?title=Module:SportsRankings/data/FIFA_World_Rankings&action=raw"
    text = http_get(url)
    rankings: dict[str, int] = {}
    pattern = re.compile(r'\{\s*"([^"]+)"\s*,\s*(\d+)\s*,')
    in_table = False
    for line in text.splitlines():
        if "data.rankings" in line:
            in_table = True
            continue
        if in_table:
            if line.strip().startswith("}") and "{" not in line:
                break
            m = pattern.search(line)
            if m:
                rankings[m.group(1)] = int(m.group(2))
    return rankings


def fetch_wikitext(page_title: str) -> str | None:
    """Henter wikitext for første seksjon (infoboks ligger der) via MediaWiki API."""
    encoded = urllib.parse.quote(page_title, safe="")
    url = (
        f"https://en.wikipedia.org/w/api.php?action=parse&page={encoded}"
        "&prop=wikitext&section=0&format=json"
    )
    try:
        raw = http_get(url)
        data = json.loads(raw)
        return data.get("parse", {}).get("wikitext", {}).get("*")
    except Exception as exc:
        print(f"  ! kunne ikke hente '{page_title}': {exc}", file=sys.stderr)
        return None


WIKILINK_RE = re.compile(r"\[\[(?:[^\]|]*\|)?([^\]]+)\]\]")
TEMPLATE_RE = re.compile(r"\{\{[^{}]*\}\}")
HTML_TAG_RE = re.compile(r"<[^>]+>")
PAREN_TAIL_RE = re.compile(r"\s*\(.*?\)\s*$")


def clean_infobox_value(raw: str) -> str:
    """Pakker ut [[Link|text]] → text, fjerner templates/HTML-tags og kommentarer."""
    s = raw
    # Fjern HTML-kommentarer
    s = re.sub(r"<!--.*?-->", "", s, flags=re.DOTALL)
    # Fjern templates (kan være nested — kjør flere passes)
    for _ in range(4):
        new = TEMPLATE_RE.sub("", s)
        if new == s:
            break
        s = new
    # Fjern HTML-tags
    s = HTML_TAG_RE.sub("", s)
    # Pakk ut wikilenker
    s = WIKILINK_RE.sub(lambda m: m.group(1), s)
    # Fjern "(110)" osv på slutten — viser ofte målantall/caps
    s = PAREN_TAIL_RE.sub("", s.strip())
    return s.strip().strip("'").strip()


def extract_infobox_field(wikitext: str, field: str) -> str | None:
    """Henter ut '| field = value' fra en MediaWiki-infoboks."""
    # | felt = verdi  (verdien går til neste linje som starter med | eller })
    pattern = re.compile(
        rf"^\|\s*{re.escape(field)}\s*=\s*(.*?)(?=^\s*\||^\s*\}}\}})",
        re.MULTILINE | re.DOTALL,
    )
    m = pattern.search(wikitext)
    if not m:
        return None
    value = clean_infobox_value(m.group(1))
    return value or None


def enrich_team(
    code: str, name: str, existing: dict, rank: int | None
) -> tuple[dict, dict[str, str]]:
    """Returnerer (oppdatert team-objekt, statistikk over hva som ble endret)."""
    changes: dict[str, str] = {}
    updated = (
        dict(existing)
        if existing
        else {
            "teamCode": code,
            "fifaRank": None,
            "manager": None,
            "starPlayer": None,
            "preferredFormation": None,
            "goalsScoredAvg": None,
            "goalsConcededAvg": None,
            "recentForm": None,
            "recentMatches": [],
            "keyAbsences": [],
            "lastWorldCupResult": None,
        }
    )
    updated["teamCode"] = code

    if rank is not None and updated.get("fifaRank") != rank:
        changes["fifaRank"] = f"{updated.get('fifaRank')} → {rank}"
        updated["fifaRank"] = rank

    page = f"{name} national football team"
    wt = fetch_wikitext(page)
    if wt:
        coach = extract_infobox_field(wt, "Coach")
        if coach and updated.get("manager") != coach:
            changes["manager"] = f"{updated.get('manager')!r} → {coach!r}"
            updated["manager"] = coach
        # Captain er ofte mer "stjernen" enn top scorer, men begge er nyttige.
        captain = extract_infobox_field(wt, "Captain")
        top_scorer = extract_infobox_field(wt, "Top scorer")
        star = captain or top_scorer
        if star and updated.get("starPlayer") != star:
            changes["starPlayer"] = f"{updated.get('starPlayer')!r} → {star!r}"
            updated["starPlayer"] = star

    return updated, changes


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Ikke skriv filen, bare vis hva som ville endret seg",
    )
    parser.add_argument("--only", help="Komma-separert liste av FIFA-koder for testing")
    args = parser.parse_args()

    if not SEED_PATH.exists():
        print(f"FEIL: {SEED_PATH} mangler", file=sys.stderr)
        return 1

    with SEED_PATH.open("r", encoding="utf-8") as f:
        seed = json.load(f)
    teams = seed.get("teams", {})
    h2h = seed.get("headToHead", {})

    print("Henter FIFA-rangering fra Wikipedia ...")
    try:
        rankings = fetch_fifa_rankings()
        print(f"  → {len(rankings)} lag i rangerings-tabellen")
    except Exception as exc:
        print(f"  ! feilet: {exc}", file=sys.stderr)
        rankings = {}

    only = set(c.strip().upper() for c in args.only.split(",")) if args.only else None
    target_codes = [c for c in FIFA_TO_NAME if not only or c in only]

    print(f"Beriker {len(target_codes)} lag fra Wikipedia-infobokser ...")
    total_changes = 0
    for code in target_codes:
        name = FIFA_TO_NAME[code]
        rank = rankings.get(name)
        # Spesialcase: Wikipedia bruker "United States" mens FIFA listen kan ha "USA"
        if rank is None and name == "United States":
            rank = rankings.get("USA")
        existing = teams.get(code, {})
        updated, changes = enrich_team(code, name, existing, rank)
        teams[code] = updated
        if changes:
            total_changes += len(changes)
            print(f"  {code} ({name}):")
            for field, diff in changes.items():
                print(f"    - {field}: {diff}")
        else:
            print(f"  {code} ({name}): ingen endringer")
        time.sleep(0.4)  # snill mot Wikipedia

    seed["teams"] = teams
    seed["headToHead"] = h2h  # uberørt

    print(f"\nTotalt {total_changes} feltendringer.")
    if args.dry_run:
        print("--dry-run satt — skriver ikke fil.")
        return 0

    with SEED_PATH.open("w", encoding="utf-8") as f:
        json.dump(seed, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Oppdatert {SEED_PATH.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
