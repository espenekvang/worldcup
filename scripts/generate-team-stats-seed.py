#!/usr/bin/env python3
"""Genererer seed-data for team stats og H2H.

Tallene er plausible mock-data — oppdater manuelt før VM eller bytt ut
med ekte data via en IExternalTeamStatsClient-implementasjon. Hele poenget
med å generere det programmatisk er at strukturen er konsistent og lett
å endre når oppdaterte tall foreligger.
"""

import json, random, datetime as dt
from pathlib import Path

random.seed(2026)

# (kode, navn-i-tekst, fifa-rank, manager, star, formation, last-wc)
TEAMS = [
    (
        "ALG",
        "Algerie",
        41,
        "Vladimir Petković",
        "Riyad Mahrez",
        "4-3-3",
        "Gruppespill 2014",
    ),
    ("ARG", "Argentina", 1, "Lionel Scaloni", "Lionel Messi", "4-3-3", "Vinner 2022"),
    (
        "AUS",
        "Australia",
        24,
        "Tony Popovic",
        "Mat Ryan",
        "4-2-3-1",
        "8-delsfinale 2022",
    ),
    (
        "AUT",
        "Østerrike",
        25,
        "Ralf Rangnick",
        "Marcel Sabitzer",
        "4-2-2-2",
        "Ikke kvalifisert 2022",
    ),
    (
        "BEL",
        "Belgia",
        8,
        "Rudi Garcia",
        "Kevin De Bruyne",
        "3-4-2-1",
        "Gruppespill 2022",
    ),
    (
        "BIH",
        "Bosnia-Hercegovina",
        76,
        "Sergej Barbarez",
        "Edin Džeko",
        "4-2-3-1",
        "Ikke kvalifisert 2022",
    ),
    (
        "BRA",
        "Brasil",
        5,
        "Carlo Ancelotti",
        "Vinícius Júnior",
        "4-2-3-1",
        "Kvartfinale 2022",
    ),
    (
        "CAN",
        "Canada",
        31,
        "Jesse Marsch",
        "Alphonso Davies",
        "4-3-3",
        "Gruppespill 2022",
    ),
    (
        "CIV",
        "Elfenbenskysten",
        39,
        "Emerse Faé",
        "Sébastien Haller",
        "4-2-3-1",
        "Ikke kvalifisert 2022",
    ),
    (
        "COD",
        "DR Kongo",
        56,
        "Sébastien Desabre",
        "Cédric Bakambu",
        "4-3-3",
        "Ikke deltatt",
    ),
    (
        "COL",
        "Colombia",
        13,
        "Néstor Lorenzo",
        "Luis Díaz",
        "4-2-3-1",
        "Ikke kvalifisert 2022",
    ),
    ("CPV", "Kapp Verde", 70, "Bubista", "Ryan Mendes", "4-3-3", "Debutant"),
    ("CRO", "Kroatia", 9, "Zlatko Dalić", "Luka Modrić", "4-3-3", "Bronse 2022"),
    ("CUW", "Curaçao", 82, "Dick Advocaat", "Leandro Bacuna", "4-4-2", "Debutant"),
    (
        "CZE",
        "Tsjekkia",
        38,
        "Ivan Hašek",
        "Patrik Schick",
        "4-2-3-1",
        "Ikke kvalifisert 2022",
    ),
    (
        "ECU",
        "Ecuador",
        33,
        "Sebastián Beccacece",
        "Moisés Caicedo",
        "4-3-3",
        "Gruppespill 2022",
    ),
    (
        "EGY",
        "Egypt",
        35,
        "Hossam Hassan",
        "Mohamed Salah",
        "4-2-3-1",
        "Ikke kvalifisert 2022",
    ),
    (
        "ENG",
        "England",
        4,
        "Thomas Tuchel",
        "Jude Bellingham",
        "4-2-3-1",
        "Kvartfinale 2022",
    ),
    (
        "ESP",
        "Spania",
        2,
        "Luis de la Fuente",
        "Lamine Yamal",
        "4-3-3",
        "8-delsfinale 2022",
    ),
    (
        "FRA",
        "Frankrike",
        3,
        "Didier Deschamps",
        "Kylian Mbappé",
        "4-2-3-1",
        "Finale 2022",
    ),
    (
        "GER",
        "Tyskland",
        10,
        "Julian Nagelsmann",
        "Florian Wirtz",
        "4-2-3-1",
        "Gruppespill 2022",
    ),
    ("GHA", "Ghana", 73, "Otto Addo", "Mohammed Kudus", "4-3-3", "Gruppespill 2022"),
    ("HAI", "Haiti", 83, "Sébastien Migné", "Duckens Nazon", "4-4-2", "1974"),
    ("IRN", "Iran", 18, "Amir Ghalenoei", "Mehdi Taremi", "3-5-2", "Gruppespill 2022"),
    ("IRQ", "Irak", 58, "Graham Arnold", "Aymen Hussein", "4-2-3-1", "Debutant"),
    ("JOR", "Jordan", 64, "Jamal Sellami", "Musa Al-Taamari", "4-3-3", "Debutant"),
    (
        "JPN",
        "Japan",
        15,
        "Hajime Moriyasu",
        "Takefusa Kubo",
        "4-3-3",
        "8-delsfinale 2022",
    ),
    (
        "KOR",
        "Sør-Korea",
        22,
        "Hong Myung-bo",
        "Son Heung-min",
        "4-2-3-1",
        "8-delsfinale 2022",
    ),
    (
        "KSA",
        "Saudi-Arabia",
        57,
        "Hervé Renard",
        "Salem Al-Dawsari",
        "4-1-4-1",
        "Gruppespill 2022",
    ),
    ("MAR", "Marokko", 14, "Walid Regragui", "Achraf Hakimi", "4-3-3", "4. plass 2022"),
    (
        "MEX",
        "Mexico",
        19,
        "Javier Aguirre",
        "Edson Álvarez",
        "4-3-3",
        "Gruppespill 2022",
    ),
    ("NED", "Nederland", 7, "Ronald Koeman", "Cody Gakpo", "4-3-3", "Kvartfinale 2022"),
    ("NOR", "Norge", 32, "Ståle Solbakken", "Erling Haaland", "4-3-3", "1998"),
    ("NZL", "New Zealand", 88, "Darren Bazeley", "Chris Wood", "4-4-2", "2010"),
    ("PAN", "Panama", 36, "Thomas Christiansen", "Aníbal Godoy", "4-2-3-1", "2018"),
    ("PAR", "Paraguay", 49, "Gustavo Alfaro", "Miguel Almirón", "4-3-3", "2010"),
    (
        "POR",
        "Portugal",
        6,
        "Roberto Martínez",
        "Bruno Fernandes",
        "4-2-3-1",
        "Kvartfinale 2022",
    ),
    (
        "QAT",
        "Qatar",
        53,
        "Bartolomé Márquez",
        "Akram Afif",
        "4-3-3",
        "Gruppespill 2022",
    ),
    ("RSA", "Sør-Afrika", 55, "Hugo Broos", "Themba Zwane", "4-3-3", "2010"),
    ("SCO", "Skottland", 40, "Steve Clarke", "Scott McTominay", "3-4-2-1", "1998"),
    ("SEN", "Senegal", 17, "Pape Thiaw", "Sadio Mané", "4-3-3", "8-delsfinale 2022"),
    (
        "SUI",
        "Sveits",
        20,
        "Murat Yakin",
        "Granit Xhaka",
        "4-2-3-1",
        "8-delsfinale 2022",
    ),
    (
        "SWE",
        "Sverige",
        42,
        "Jon Dahl Tomasson",
        "Alexander Isak",
        "4-4-2",
        "Ikke kvalifisert 2022",
    ),
    (
        "TUN",
        "Tunisia",
        47,
        "Sami Trabelsi",
        "Hannibal Mejbri",
        "4-3-3",
        "Gruppespill 2022",
    ),
    (
        "TUR",
        "Tyrkia",
        26,
        "Vincenzo Montella",
        "Arda Güler",
        "4-2-3-1",
        "Ikke kvalifisert 2022",
    ),
    (
        "URU",
        "Uruguay",
        16,
        "Marcelo Bielsa",
        "Federico Valverde",
        "4-3-3",
        "Gruppespill 2022",
    ),
    (
        "USA",
        "USA",
        16,
        "Mauricio Pochettino",
        "Christian Pulisic",
        "4-3-3",
        "8-delsfinale 2022",
    ),
    ("UZB", "Usbekistan", 57, "Timur Kapadze", "Eldor Shomurodov", "4-3-3", "Debutant"),
]

# H2H-par vi har "ekte"-ish historikk for. Resten faller tilbake til 404.
H2H_PAIRS = [
    ("ARG", "BRA", 110, 41, 26, 43),
    ("ENG", "GER", 33, 13, 5, 15),
    ("ESP", "POR", 38, 17, 12, 9),
    ("FRA", "GER", 31, 14, 8, 9),
    ("BRA", "GER", 23, 12, 5, 6),
    ("ARG", "GER", 22, 9, 5, 8),
    ("ESP", "FRA", 36, 16, 7, 13),
    ("ENG", "FRA", 31, 17, 5, 9),
    ("BEL", "NED", 128, 41, 30, 57),
    ("ITA", "ESP", 0, 0, 0, 0),  # Italia ikke i VM, men beholdt struktur
    ("URU", "ARG", 200, 56, 47, 97),
    ("MEX", "USA", 76, 36, 15, 25),
    ("NOR", "SWE", 105, 41, 22, 42),
    ("CRO", "FRA", 10, 3, 1, 6),
    ("MAR", "FRA", 7, 1, 1, 5),
    ("POR", "MAR", 5, 3, 1, 1),
    ("KOR", "JPN", 80, 16, 23, 41),
    ("BEL", "FRA", 76, 30, 19, 27),
    ("BRA", "FRA", 12, 1, 4, 7),
    ("ARG", "FRA", 12, 6, 3, 3),
    ("ENG", "ARG", 17, 6, 5, 6),
    ("ESP", "ENG", 27, 13, 3, 11),
    ("USA", "CAN", 43, 19, 14, 10),
    ("AUT", "GER", 41, 6, 5, 30),
    ("SCO", "ENG", 115, 41, 24, 50),
    ("SUI", "FRA", 39, 9, 6, 24),
    ("SEN", "MAR", 18, 6, 5, 7),
    ("EGY", "TUN", 30, 14, 8, 8),
    ("COL", "BRA", 35, 2, 9, 24),
    ("URU", "BRA", 78, 20, 19, 39),
    ("JPN", "AUS", 27, 8, 8, 11),
    ("IRN", "KOR", 33, 9, 11, 13),
]

COMPS = [
    "Vennskapskamp",
    "Nations League",
    "VM-kvalifisering",
    "EM-kvalifisering",
    "Africa Cup of Nations",
    "Copa América",
    "AFC Asian Cup",
    "CONCACAF Nations",
]


def gen_recent_matches(team_code, strength):
    """Generer 5 plausible kamper. strength: lavere FIFA-rank → flere seire."""
    out = []
    today = dt.date(2026, 6, 1)
    win_prob = max(0.25, min(0.75, 1.0 - strength / 80.0))
    for i in range(5):
        d = today - dt.timedelta(days=20 + i * 14 + random.randint(0, 6))
        opp = random.choice([t[0] for t in TEAMS if t[0] != team_code])
        r = random.random()
        if r < win_prob:
            gf, ga = random.choice([(2, 0), (2, 1), (3, 1), (1, 0), (3, 2)])
            res = "W"
        elif r < win_prob + 0.25:
            gf = ga = random.choice([0, 1, 1, 2])
            res = "D"
        else:
            gf, ga = random.choice([(0, 1), (1, 2), (0, 2), (1, 3)])
            res = "L"
        out.append(
            {
                "date": d.isoformat(),
                "opponent": opp,
                "venue": random.choice(["home", "away", "neutral"]),
                "goalsFor": gf,
                "goalsAgainst": ga,
                "result": res,
                "competition": random.choice(COMPS),
            }
        )
    return out


def gen_team(t):
    code, name, rank, mgr, star, form, lastwc = t
    matches = gen_recent_matches(code, rank)
    form_str = "".join(m["result"] for m in reversed(matches))
    gf = sum(m["goalsFor"] for m in matches)
    ga = sum(m["goalsAgainst"] for m in matches)
    absences = []
    if random.random() < 0.4:
        absences.append(f"Skadet: spiller-{random.randint(1, 30)}")
    if random.random() < 0.2:
        absences.append("Suspendert etter gult kort i kvalik")
    return {
        "teamCode": code,
        "fifaRank": rank,
        "manager": mgr,
        "starPlayer": star,
        "preferredFormation": form,
        "goalsScoredAvg": round(gf / 5, 2),
        "goalsConcededAvg": round(ga / 5, 2),
        "recentForm": form_str,
        "recentMatches": matches,
        "keyAbsences": absences,
        "lastWorldCupResult": lastwc,
    }


def gen_h2h(pair):
    a, b, total, awins, draws, bwins = pair
    if a > b:
        a, b = b, a
        awins, bwins = bwins, awins
    if total == 0:
        return None
    # Tilfeldig fordeling av mål
    a_goals = awins * 2 + draws + bwins // 2
    b_goals = bwins * 2 + draws + awins // 2
    # Generer 5 nyeste møter
    recent = []
    today = dt.date(2025, 11, 1)
    for i in range(5):
        d = today - dt.timedelta(days=180 * i + random.randint(0, 60))
        host = random.choice([a, b])
        guest = b if host == a else a
        r = random.random()
        if r < 0.4:
            hs, as_ = random.choice([(2, 1), (1, 0), (3, 2)])
        elif r < 0.7:
            hs = as_ = random.choice([1, 2])
        else:
            hs, as_ = random.choice([(0, 1), (1, 2), (0, 2)])
        recent.append(
            {
                "date": d.isoformat(),
                "homeTeam": host,
                "awayTeam": guest,
                "homeScore": hs,
                "awayScore": as_,
                "competition": random.choice(COMPS + ["VM-finale", "VM-gruppespill"]),
                "venue": None,
            }
        )
    return {
        "teamA": a,
        "teamB": b,
        "totalMatches": total,
        "teamAWins": awins,
        "draws": draws,
        "teamBWins": bwins,
        "teamAGoals": a_goals,
        "teamBGoals": b_goals,
        "recentMatches": recent,
    }


seed = {
    "teams": {t[0]: gen_team(t) for t in TEAMS},
    "headToHead": {},
}
for pair in H2H_PAIRS:
    h = gen_h2h(pair)
    if h is None:
        continue
    seed["headToHead"][f"{h['teamA']}-{h['teamB']}"] = h

out_path = (
    Path(__file__).resolve().parent.parent
    / "api"
    / "WorldCup.Api"
    / "data"
    / "teamStats.json"
)
out_path.write_text(json.dumps(seed, indent=2, ensure_ascii=False))
print(
    f"Wrote {out_path} ({len(seed['teams'])} teams, {len(seed['headToHead'])} H2H pairs)"
)
