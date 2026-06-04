#!/usr/bin/env python3
"""Henter VM-2026-tropper fra Wikipedia og fyller inn "squad" i teamStats.json.

Kilde: artikkelen «2026 FIFA World Cup squads» på en.wikipedia.org. Den lister
alle troppene med standard-malen {{nat fs ... player |no= |pos= |name= |age=
{{Birth date and age|YYYY|MM|DD}} |caps= |goals= |club= |clubnat=}}. Vi henter
rå wikitekst via MediaWiki-API-et og parser malene deterministisk.

For hver spiller henter vi:
  - name   (utpakket fra [[wikilenke|visningstekst]])
  - position (GK/DF/MF/FW)
  - shirtNumber (no=)
  - age    (regnet ut fra {{Birth date and age}} per turneringsstart 2026-06-11)
  - club   (utpakket fra [[wikilenke|visningstekst]])

Resultatet skrives inn som feltet "squad" på hvert lag i
api/WorldCup.Api/data/teamStats.json. Andre felter (form, manager osv.) røres ikke.

Bruk:
    python3 scripts/scrape-wc2026-squads.py [--dry-run] [--only NOR,BRA]

NB: Krever utgående nett til en.wikipedia.org. Kjør lokalt eller i CI — i et
sandkasse-miljø med GitHub-only nettpolicy vil API-kallet feile med 403.
Scriptet er idempotent: kjør så ofte du vil, troppene overskrives med ferske tall.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import sys
import urllib.parse
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
API_DIR = REPO_ROOT / "api" / "WorldCup.Api"
# Katalogen heter «Data» (stor D) i repoet, men søsterscriptene bruker «data».
# Filsystem på Linux/CI er case-sensitivt, så vi velger den som faktisk finnes.
SEED_PATH = next(
    (
        p
        for p in (API_DIR / "Data" / "teamStats.json", API_DIR / "data" / "teamStats.json")
        if p.exists()
    ),
    API_DIR / "Data" / "teamStats.json",
)

USER_AGENT = "WorldCupTippeApp/1.0 (scrape-wc2026-squads; contact: hobby)"
SQUADS_PAGE = "2026 FIFA World Cup squads"

# Alder regnes per turneringsstart slik at tallet er stabilt gjennom hele VM.
TOURNAMENT_START = dt.date(2026, 6, 11)

# Wikipedia-overskrift (landsnavn) → FIFA-kode brukt i teamStats.json/teams.json.
# Dekker varianter Wikipedia kan bruke i seksjonsoverskriftene.
NAME_TO_CODE: dict[str, str] = {
    "Algeria": "ALG",
    "Argentina": "ARG",
    "Australia": "AUS",
    "Austria": "AUT",
    "Belgium": "BEL",
    "Bosnia and Herzegovina": "BIH",
    "Brazil": "BRA",
    "Canada": "CAN",
    "Cape Verde": "CPV",
    "Cabo Verde": "CPV",
    "Colombia": "COL",
    "Croatia": "CRO",
    "Curaçao": "CUW",
    "Czech Republic": "CZE",
    "Czechia": "CZE",
    "DR Congo": "COD",
    "Democratic Republic of the Congo": "COD",
    "Ecuador": "ECU",
    "Egypt": "EGY",
    "England": "ENG",
    "France": "FRA",
    "Germany": "GER",
    "Ghana": "GHA",
    "Haiti": "HAI",
    "Iran": "IRN",
    "IR Iran": "IRN",
    "Iraq": "IRQ",
    "Ivory Coast": "CIV",
    "Côte d'Ivoire": "CIV",
    "Japan": "JPN",
    "Jordan": "JOR",
    "Mexico": "MEX",
    "Morocco": "MAR",
    "Netherlands": "NED",
    "New Zealand": "NZL",
    "Norway": "NOR",
    "Panama": "PAN",
    "Paraguay": "PAR",
    "Portugal": "POR",
    "Qatar": "QAT",
    "Saudi Arabia": "KSA",
    "Scotland": "SCO",
    "Senegal": "SEN",
    "South Africa": "RSA",
    "South Korea": "KOR",
    "Korea Republic": "KOR",
    "Republic of Korea": "KOR",
    "Spain": "ESP",
    "Sweden": "SWE",
    "Switzerland": "SUI",
    "Tunisia": "TUN",
    "Turkey": "TUR",
    "Türkiye": "TUR",
    "United States": "USA",
    "Uruguay": "URU",
    "Uzbekistan": "UZB",
}


def http_get(url: str) -> str:
    """HTTP GET med User-Agent — Wikipedia krever en beskrivende agent."""
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=30) as resp:
        return resp.read().decode("utf-8", errors="replace")


def fetch_wikitext(page_title: str) -> str:
    """Henter full wikitekst for en artikkel via MediaWiki parse-API-et."""
    encoded = urllib.parse.quote(page_title, safe="")
    url = (
        f"https://en.wikipedia.org/w/api.php?action=parse&page={encoded}"
        "&prop=wikitext&format=json&formatversion=2"
    )
    data = json.loads(http_get(url))
    if "error" in data:
        raise RuntimeError(data["error"].get("info", "ukjent API-feil"))
    return data["parse"]["wikitext"]


# --- Parsing-hjelpere ---------------------------------------------------------

HEADER_RE = re.compile(r"^={2,}\s*(.+?)\s*={2,}\s*$", re.MULTILINE)
WIKILINK_RE = re.compile(r"\[\[(?:[^\]|]*\|)?([^\]]+)\]\]")
# Start på en spiller-rad: {{nat fs g player|, {{nat fs r player|, {{nat fs player|
PLAYER_START_RE = re.compile(r"\{\{\s*nat fs (?:g |r )?player\s*\|", re.IGNORECASE)
BIRTH_RE = re.compile(
    r"\{\{\s*birth date and age\s*\|([^}]*)\}\}", re.IGNORECASE
)


def iter_player_bodies(text: str):
    """Yield parameter-kroppen til hver spiller-mal, med balanserte {{...}}.

    En enkel regex duger ikke fordi |age={{Birth date and age|...}} inneholder
    nøstede klammer. Vi finner mal-starten og skanner fram til klammedybden er 0.
    """
    for m in PLAYER_START_RE.finditer(text):
        depth = 1  # vi står rett etter den åpnende «{{»
        i = m.end()
        n = len(text)
        while i < n and depth > 0:
            if text.startswith("{{", i):
                depth += 1
                i += 2
            elif text.startswith("}}", i):
                depth -= 1
                i += 2
            else:
                i += 1
        # body = alt mellom «player|» og den avsluttende «}}»
        yield text[m.end() : i - 2]


def unwrap_links(value: str) -> str:
    """[[Sevilla FC|Sevilla]] → Sevilla, [[Erling Haaland]] → Erling Haaland."""
    s = WIKILINK_RE.sub(lambda m: m.group(1), value)
    s = re.sub(r"<[^>]+>", "", s)  # fjern evt. HTML-tags
    s = re.sub(r"\{\{[^{}]*\}\}", "", s)  # fjern resterende maler (flagicon o.l.)
    return s.strip().strip("'").strip()


def parse_template_params(body: str) -> dict[str, str]:
    """Deler «|key=value|key=value» til dict. Respekterer nested {{...}}/[[...]]."""
    params: dict[str, str] = {}
    depth_brace = depth_brack = 0
    buf: list[str] = []
    parts: list[str] = []
    for ch in body:
        if ch == "{":
            depth_brace += 1
        elif ch == "}":
            depth_brace = max(0, depth_brace - 1)
        elif ch == "[":
            depth_brack += 1
        elif ch == "]":
            depth_brack = max(0, depth_brack - 1)
        if ch == "|" and depth_brace == 0 and depth_brack == 0:
            parts.append("".join(buf))
            buf = []
        else:
            buf.append(ch)
    parts.append("".join(buf))
    for part in parts:
        if "=" not in part:
            continue
        key, _, val = part.partition("=")
        params[key.strip().lower()] = val.strip()
    return params


def compute_age(birth_param: str) -> int | None:
    """Regner ut alder per TOURNAMENT_START fra {{Birth date and age|Y|M|D}}."""
    m = BIRTH_RE.search(birth_param)
    if not m:
        return None
    nums = re.findall(r"\d+", m.group(1))
    if len(nums) < 3:
        return None
    try:
        born = dt.date(int(nums[0]), int(nums[1]), int(nums[2]))
    except ValueError:
        return None
    ref = TOURNAMENT_START
    age = ref.year - born.year - ((ref.month, ref.day) < (born.month, born.day))
    return age if 14 <= age <= 55 else None


def parse_player(body: str) -> dict | None:
    p = parse_template_params(body)
    name = unwrap_links(p.get("name", ""))
    if not name:
        return None
    pos = unwrap_links(p.get("pos", "")).upper() or None
    club = unwrap_links(p.get("club", "")) or None
    age = compute_age(p.get("age", ""))
    number = None
    no_raw = re.sub(r"\D", "", p.get("no", ""))
    if no_raw:
        number = int(no_raw)
    return {
        "name": name,
        "position": pos,
        "shirtNumber": number,
        "age": age,
        "club": club,
    }


def split_into_team_sections(wikitext: str) -> dict[str, str]:
    """Returnerer { FIFA-kode: seksjons-wikitekst } basert på lag-overskrifter."""
    sections: dict[str, str] = {}
    matches = list(HEADER_RE.finditer(wikitext))
    for i, m in enumerate(matches):
        header = unwrap_links(m.group(1))
        code = NAME_TO_CODE.get(header)
        if not code:
            continue
        start = m.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(wikitext)
        # Hvis samme lag dukker opp flere ganger, behold den lengste seksjonen.
        body = wikitext[start:end]
        if code not in sections or len(body) > len(sections[code]):
            sections[code] = body
    return sections


def scrape_squads(only: set[str] | None) -> dict[str, list[dict]]:
    print(f"Henter wikitekst for «{SQUADS_PAGE}» …")
    wikitext = fetch_wikitext(SQUADS_PAGE)
    sections = split_into_team_sections(wikitext)
    print(f"  → fant {len(sections)} lag-seksjoner")

    squads: dict[str, list[dict]] = {}
    for code, body in sorted(sections.items()):
        if only and code not in only:
            continue
        players = [
            pl
            for body_tpl in iter_player_bodies(body)
            if (pl := parse_player(body_tpl)) is not None
        ]
        if players:
            squads[code] = players
            with_club = sum(1 for p in players if p["club"])
            with_age = sum(1 for p in players if p["age"] is not None)
            print(
                f"  {code}: {len(players)} spillere "
                f"({with_age} m/alder, {with_club} m/klubb)"
            )
        else:
            print(f"  {code}: ingen spillere parset (tropp ikke publisert ennå?)")
    return squads


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--dry-run", action="store_true", help="Ikke skriv fil, bare vis resultat"
    )
    parser.add_argument("--only", help="Komma-separert liste av FIFA-koder")
    args = parser.parse_args()

    if not SEED_PATH.exists():
        print(f"FEIL: {SEED_PATH} mangler", file=sys.stderr)
        return 1

    only = {c.strip().upper() for c in args.only.split(",")} if args.only else None

    try:
        squads = scrape_squads(only)
    except Exception as exc:  # noqa: BLE001 — vis tydelig feil til bruker
        print(f"\nFEIL under henting/parsing: {exc}", file=sys.stderr)
        print(
            "Tips: kjør fra et miljø med utgående nett til en.wikipedia.org.",
            file=sys.stderr,
        )
        return 2

    if not squads:
        print("\nIngen tropper parset — skriver ikke fil.")
        return 3

    with SEED_PATH.open("r", encoding="utf-8") as f:
        seed = json.load(f)
    teams = seed.setdefault("teams", {})

    updated = 0
    for code, players in squads.items():
        team = teams.get(code)
        if team is None:
            print(f"  ! {code} finnes ikke i seed — hopper over", file=sys.stderr)
            continue
        team["squad"] = players
        updated += 1

    print(f"\nOppdaterte tropper for {updated} lag.")
    if args.dry_run:
        print("--dry-run satt — skriver ikke fil.")
        return 0

    with SEED_PATH.open("w", encoding="utf-8") as f:
        json.dump(seed, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Skrev {SEED_PATH.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
