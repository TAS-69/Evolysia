# Worlds and people

The default planet contains 800 × 400 cells and wraps east-west. Generation combines continents, ridged mountains, temperature, rainfall, river drainage, lakes, faults and volcanoes. Climate and altitude select biomes. Seasons, regional rain anomalies and disasters change ecological stocks over time.

Timber regrows slowly. Loose stone, boulders and ore deposits can be depleted. Plant, prey and fish stocks influence where bands forage, hunt, fish and settle. Wildlife visuals represent ecological stocks; every visible animal is not a separately simulated creature with its own life history.

## People

Each person has a genome, age, kinship, aptitudes, needs, techniques, possessions and psychology. Children blend parental traits with variation. Temperament affects behaviour; lived experience shifts character and learned fears. Emotions include calm, joy, pride, love, surprise, fear, anger, disgust, sadness, shame and envy.

Fear learning distinguishes avoidance, facing danger successfully and being hurt. Stories and witnessed terror can spread weaker fears. Hunger, thirst, cold, illness, injury, childbirth and age affect survival.

Activities are chosen by each band's council (the required model), never by direct player orders; the rules-only diagnostic mode uses utility scores instead. People forage, hunt, fish, farm, herd, build, craft, gather materials, experiment, teach, court, care, mourn and play. Their work has a tile location; the renderer animates travel between home and work.

## Society and knowledge

Bands migrate, split, merge, meet neighbours, exchange techniques, raid and settle. Culture includes shared language, norms, myths, taboos and beliefs. Leaders and social responses affect whether an unusual invention or idea is accepted, suppressed or carried into a new band.

The simulation defines 106 techniques, from early tools to advanced technology. Individuals know and teach techniques subject to prerequisites and opportunity. New ones are discovered only by experiment: a council proposes an action on things its people can perceive, and the simulation judges it against each technique's hidden way in (see [Local models](Local-models)). There are no fixed era switches. A catalogue definition is not a promise that a normal alpha run will reliably discover and support every late-game activity.

Ordinary simulation results are deterministic for a seed, ordered player actions and accepted decisions. Band work uses separate random streams and ordered commits. Tests compare histories across one, four and sixteen threads. Unconstrained external-model responses are outside that guarantee.
