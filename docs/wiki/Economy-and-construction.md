# Economy and construction

Current source defines **107 goods, 146 gear items, 40 processing recipes and 130 structure kinds**. These are simulation definitions; art catalogue coverage and complete institutional gameplay are separate matters.

## Resources and work

Timber, loose stone, boulders and ore are finite world resources. Gathering, hunting, fishing, farming, herding and processing produce named goods with bulk, food value and spoilage. Construction clears its footprint and consumes actual inputs and labour. Knowledge and local affordances constrain available projects, and new techniques are found only by experiment (see [Local models](Local-models)).

Required model councils choose labour shares and personal work, construction priorities, experiments, migration and sharing. Physical feasibility and urgent rest constrain execution. Councils can order specific recipes and equipment, choose recipients/output containers, staff public services and set fuel use and battery reserves. Crafters only make what councils order; without an order they tinker.

Gear belongs to individuals and wears out. Clothing choices in the outfit editor request real garments; a suitable crafter still needs knowledge, materials and any required station. Dye choices are independent for the garment's main and trim regions.

## Buildings and interiors

Structures retain stable IDs, footprints, ownership, construction state and condition. Thirty-one specialist layouts now produce real rooms with separately owned fixtures. Fixtures cost materials and can contain their own stock; floor and roof sprites do not create free supplies.

Model councils can propose named layouts from known templates, with 3–12 cell dimensions and up to sixteen known one-cell fixtures. A clear path must connect the south-centre doorway to each fixture. Invalid layouts are rejected before world changes. Accepted designs become costed projects, with material and labour requirements scaled by area.

Regional construction substitutes bamboo for wood/logs in tropical and wetland regions, and reeds for fibre in wetlands. The instance keeps its actual material bill; combined substitutions are charged once per good. Regional construction and institutional behaviour still have room to grow.

## Physical stock and cargo

Backpacks, chests, granaries and other containers own persistent stacks. A stack keeps its identity through a complete transfer; splitting records its parent identity. Bounded merge records identify stacks retired by compaction. Nested room storage contributes to the band's totals once, without duplicating quantities.

Explicit cargo jobs reserve source stock, worker and optional vehicle. A worker reaches a vehicle before collecting cargo; the carrier then visits the source, loads, follows the route and unloads at the destination. Filters, capacity, ownership and reachable paths validate before acceptance. Reserved stock cannot also be consumed or booked for another job, but it can still spoil or be destroyed. Cancellation releases reservations and preserves surviving stock at its actual owner.

Saves preserve jobs in transit. Foot cargo is limited to twenty bulk; vehicles have their own capacities. Moving vehicles no longer retain a blocking footprint at their old parking position.

This does not yet route every harvest deposit, crafting input, feast or trade through a walking carrier. Complete sea-freight economies, moving footprint clearance, traffic and comprehensive theft remain unfinished. Existing voyages still model remembered shores, knowledge-dependent range, migration and loss at sea.

## Manufacturing, utilities and services

An order identifies an adult worker, known product, batch count and destination. Inputs move into the job inventory before labour is spent. Required stations and electrical supply gate work; full outputs wait. Cancellation returns surviving staged inputs to a visible local ground pile. Inputs can spoil while waiting. Equipment is equipped by its intended recipient or retained as spare gear.

Generators burn actual coal, charcoal or wood when the council allows fuel power. Solar panels produce climate-dependent power; batteries store surplus and respect the chosen reserve. Powered manufacturing and cold storage consume electricity. These are community grids, with simplified generation and no wire network yet.

Clinics consume medicine/herbs to treat people. Teaching transfers techniques the teacher knows, respecting prerequisites; research rooms speed the band's experiments. Worship can spread generated fictional doctrine, governance improves trust/belonging and staffed guards improve defence readiness. Inspect a band for power totals and service counters. Complete legal, market and institutional simulation remains future work.

## Roads and loose goods

Councils can connect camp and owned buildings with paths, dirt roads or stone roads. Builders clear vegetation/rock and pay the surface bill per cell. Paths take labour; dirt uses stone and Wheel; stone paving uses stone blocks and Roads. Land freight costs one movement unit off-road, 0.8 on a path, two-thirds on dirt and 0.5 on stone. Repeated hauling can wear an unpaved path. Dynamic obstacles trigger a route recheck.

Surplus is dropped into persistent ground stock, rather than deleted. Its real contents are visible at close zoom, spoil over time and can be recovered with cargo orders. Displayed stock icons summarize quantities; each icon is not a separate invented stack.
