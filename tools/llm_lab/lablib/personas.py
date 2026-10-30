"""Numeric trait vectors -> short adjective character sheets the model can act on.

Temperament (inherited): NS novelty_seeking, HA harm_avoidance, RD reward_dependence, PS persistence.
Character (learned):     SD self_directedness, CO cooperativeness, ST self_transcendence.
Values are 0..1; 0.5 is average.
"""

import random
from dataclasses import dataclass

TRAITS = ("NS", "HA", "RD", "PS", "SD", "CO", "ST")

PHRASES = {
    "NS": ("restless and drawn to anything new or risky", "prefers the familiar and distrusts anything new"),
    "HA": ("fearful and quick to sense danger", "fearless and rarely worried about danger"),
    "RD": ("craves approval and is deeply hurt by scorn", "indifferent to praise or scorn"),
    "PS": ("stubborn, never gives up once set on something", "gives up easily when things go badly"),
    "SD": ("self-reliant, trusts their own judgment over the group's", "unsure of themself, lets others decide"),
    "CO": ("generous, puts the group first", "selfish and distrustful, puts themself first"),
    "ST": ("spiritual, sees spirits and omens everywhere", "practical, scoffs at spirits and omens"),
}


@dataclass
class Persona:
    name: str
    age: int
    role: str
    traits: dict[str, float]
    archetype: str = "random"

    def centred(self) -> dict[str, float]:
        return {t: (self.traits[t] - 0.5) * 2 for t in TRAITS}

    def temperature(self) -> float:
        # Novelty seekers behave less predictably.
        return round(0.6 + 0.5 * self.traits["NS"], 2)

    def sheet(self) -> str:
        parts = []
        for t, c in sorted(self.centred().items(), key=lambda kv: -abs(kv[1])):
            if abs(c) < 0.35:
                continue
            phrase = PHRASES[t][0 if c > 0 else 1]
            parts.append(("extremely " if abs(c) >= 0.7 else "") + phrase)
        temperament = "; ".join(parts) if parts else "fairly ordinary in temperament"
        return f"{self.name}, {self.age}, {self.role}. Personality: {temperament}."


def _p(name, age, role, archetype, **t):
    traits = {k: 0.5 for k in TRAITS}
    traits.update(t)
    return Persona(name, age, role, traits, archetype)


ARCHETYPES = [
    _p("Ama", 24, "gatherer", "timid conformist", NS=0.2, HA=0.9, RD=0.9, PS=0.3, SD=0.15, CO=0.7),
    _p("Bren", 31, "hunter", "bold rebel", NS=0.9, HA=0.1, RD=0.15, PS=0.9, SD=0.9, CO=0.35, ST=0.3),
    _p("Cyra", 58, "healer", "pious elder", HA=0.6, RD=0.6, CO=0.85, ST=0.95, NS=0.25),
    _p("Doru", 40, "trapper", "ruthless loner", CO=0.05, RD=0.1, SD=0.8, HA=0.3, ST=0.3),
    _p("Esk", 19, "toolmaker", "curious tinkerer", NS=0.95, PS=0.8, HA=0.25, SD=0.7, ST=0.2),
    _p("Fen", 27, "weaver", "anxious clinger", HA=0.95, RD=0.95, SD=0.1, PS=0.3, NS=0.2),
    _p("Galo", 45, "fisher", "stubborn skeptic", PS=0.95, ST=0.05, SD=0.85, RD=0.25, NS=0.4),
    _p("Hesa", 36, "potter", "warm peacemaker", CO=0.95, RD=0.75, HA=0.5, NS=0.4, ST=0.6),
]

NAMES = ["Ilo", "Juno", "Kesh", "Lira", "Moro", "Nadi", "Oru", "Pell", "Quen", "Rasa", "Sefu", "Tavi"]
ROLES = ["gatherer", "hunter", "fisher", "builder", "herder", "toolmaker", "weaver", "scout"]


def random_personas(n: int, seed: int = 7) -> list[Persona]:
    rng = random.Random(seed)
    out = []
    for i in range(n):
        traits = {t: round(rng.betavariate(0.8, 0.8), 2) for t in TRAITS}  # favour extremes a bit
        out.append(Persona(NAMES[i % len(NAMES)], rng.randint(16, 60), rng.choice(ROLES), traits))
    return out
