# Dystopia: hand foundry.

reagent-name-dystopia-molten-slag = slag
reagent-desc-dystopia-molten-slag = Molten waste rock. It floats on top of the metal; skim it off or the casting comes out spoiled.

dystopia-foundry-temperature = Temperature: [color=#ff9a50]{ $temperature } °C[/color].
dystopia-foundry-contents-empty = It is empty.
dystopia-foundry-contents-liquid = Inside: { $volume } of { $max } u, [color=#ff9a50]liquid[/color]:
dystopia-foundry-contents-solid = Inside: { $volume } of { $max } u, [color=#9aa4b0]solidified[/color]:
dystopia-foundry-contents-entry = — [color={ $color }]{ $name }[/color]: { $amount } u

dystopia-hot-item-need-tongs = { CAPITALIZE(THE($item)) } is red-hot. Use tongs.
dystopia-hot-item-burn = You burn your hand on { THE($item) }!

dystopia-crucible-solidified = The metal in the crucible has solidified. Heat it up first.
dystopia-crucible-full = The crucible is full.
dystopia-crucible-loaded = You put { THE($item) } into the crucible.
dystopia-crucible-not-empty-item = Empty { THE($item) } first.
dystopia-crucible-charge = Waiting to melt: { $material } of { $max } units, melts at { $temperature } °C.
dystopia-crucible-skim-verb = Skim slag
dystopia-crucible-skim-need-tongs = Slag is skimmed with tongs.
dystopia-crucible-skim-too-little = There is no slag.
dystopia-crucible-has-slag = [color=#c08050]Slag floats on top[/color]. Skim it before pouring (tongs in hand, "Skim slag") or the casting will be spoiled.
dystopia-crucible-skimmed = You skim off { $count } lumps of slag.

dystopia-forge-no-fuel = The forge has no fuel.
dystopia-forge-lit = The forge flares up.
dystopia-forge-not-lit = The forge is not lit.
dystopia-forge-fuel-full = The forge is full of fuel.
dystopia-forge-fuel-added = Fuel for { $seconds } s.
dystopia-forge-examine-lit = The forge is [color=#ff8a30]burning[/color].
dystopia-forge-examine-cold = The forge is cold.
dystopia-forge-examine-fuel = Fuel for { $seconds } s.
dystopia-forge-examine-max = Heat: up to { $base } °C, with bellows next to it up to { $bellows } °C.
dystopia-forge-examine-bellows = The bellows fan the flame.

dystopia-bellows-verb = Pump bellows
dystopia-bellows-not-anchored = The bellows are loose. Bolt them to the floor with a wrench.
dystopia-bellows-no-forge = There is no burning forge next to them.
dystopia-bellows-examine = Pump by hand: they fan a burning forge on the next tile.
dystopia-bellows-examine-loose = [color=#d0a040]Not bolted to the floor.[/color]

dystopia-tongs-gripped = You grip { THE($item) } with the tongs.
dystopia-tongs-released = You open the tongs and set down { THE($item) }.
dystopia-tongs-release-verb = Open tongs
dystopia-tongs-holding = { CAPITALIZE(THE($item)) } is gripped in the tongs.
dystopia-tongs-anchored = { CAPITALIZE(THE($item)) } is bolted to the floor. Unbolt it first.
dystopia-tongs-cant-place = You can't put it there.
dystopia-tongs-surface-full = Something is already there.
dystopia-tongs-poured = You pour { $amount } u into { THE($target) }.
dystopia-tongs-pour-empty = The crucible is empty.
dystopia-tongs-pour-full = It won't hold any more.

dystopia-tongs-too-heavy = { CAPITALIZE(THE($item)) } is too heavy for tongs. It gets set right into the forge.

# Workshop: forge with a large crucible, casting channels, casting table.
dystopia-tilt-pour-verb = Tilt crucible
dystopia-tilt-pour-no-target = No casting channel leads to the forge, and there is no casting table next to it.
dystopia-tilt-pour-done = You tilt the crucible and pour out { $amount } u.
dystopia-channel-no-moulds = The channels lead nowhere: none of their ends reaches a casting table.
dystopia-channel-moulds-full = The forms are already full (or the tables have no forms).
dystopia-crucible-load-verb = Load into crucible
dystopia-forge-mount-occupied = There is already a crucible set into the forge.
dystopia-forge-mount-clear = Take the small crucible off the forge first.
dystopia-forge-mounted = You set the large crucible into the forge.
dystopia-forge-unmounted = You take the large crucible out of the forge.
dystopia-forge-unmount-verb = Remove large crucible
dystopia-forge-mounted-examine = Coal or planks go into the firebox; ore and scrap go into the crucible; to put coal into the crucible (for steel) use "Load into crucible". "Tilt crucible" pours into a channel leading to the forge.
dystopia-casting-table-slot = casting form
dystopia-casting-table-no-form = No form. Insert a casting form.
dystopia-casting-table-form-busy = There is metal in the form. Wait for the casting to come out.

dystopia-quench-hiss = { CAPITALIZE(THE($item)) } hisses as the water flashes to steam.

dystopia-mould-shape = Shape: { $shape }, { $volume } u of molten metal per casting.
dystopia-mould-shape-ingot = ingot
dystopia-mould-shape-sheet = sheet
dystopia-mould-shape-rod = rod

stack-dystopia-slag = slag
construction-category-dystopia-industry = Industry

# Casting table: castings stay inside
dystopia-casting-table-nothing = There are no finished castings.
dystopia-casting-table-taken = You take the castings out of the table.
dystopia-casting-table-take-verb = Take castings
dystopia-casting-table-inside = Finished castings inside: { $count } (take with an empty hand or the verb).

# Electric forge
dystopia-forge-electric-on-verb = Switch on forge
dystopia-forge-electric-off-verb = Switch off forge
dystopia-forge-electric-turned-on = You switch the forge on; the heaters start to glow.
dystopia-forge-electric-turned-off = You switch the forge off.
dystopia-forge-electric-examine-working = The forge is [color=#ff8a30]on[/color] and heating.
dystopia-forge-electric-examine-unpowered = The forge is on, but there is [color=#d05050]no power[/color].
dystopia-forge-electric-examine-off = The forge is off (verb to switch it on).
dystopia-forge-electric-examine-max = Heat: up to { $max } °C, no bellows needed.
dystopia-forge-upgrade-already = This forge is already electric.
dystopia-forge-upgraded = You fit the heaters; the forge is electric now.
dystopia-forge-mounted-electric-examine = Anything that melts goes into the crucible on click (coal too). Switch on with the verb. "Tilt crucible" pours into a channel leading to the forge.

# Electric arc furnace
dystopia-arc-furnace-slag-ejected = You unload the slag from the furnace.
dystopia-arc-furnace-examine-on = The furnace is [color=#a0c8ff]on[/color].
dystopia-arc-furnace-examine-off = The furnace is off.
dystopia-arc-furnace-examine-selected = Tapping: { $alloy }.
dystopia-arc-furnace-examine-slag = Skimmed slag: { $count }.
dystopia-arc-furnace-ui-title = Electric arc furnace
dystopia-arc-furnace-ui-turn-on = Switch on
dystopia-arc-furnace-ui-turn-off = Switch off
dystopia-arc-furnace-ui-status-off = Furnace is off
dystopia-arc-furnace-ui-status-on = Arc is burning
dystopia-arc-furnace-ui-status-unpowered = No power
dystopia-arc-furnace-ui-temperature = Temperature: { $temperature } °C of { $max } °C
dystopia-arc-furnace-ui-charge = Charge: { $charge } of { $max } chunks. Melt: { $volume } of { $maxVolume } u.
dystopia-arc-furnace-ui-channel-yes = A casting channel is attached.
dystopia-arc-furnace-ui-channel-no = No casting channel attached: nowhere to pour.
dystopia-arc-furnace-ui-melt = Melt
dystopia-arc-furnace-ui-empty = Empty.
dystopia-arc-furnace-ui-amount = { $amount } u
dystopia-arc-furnace-ui-pour = Tapping
dystopia-arc-furnace-ui-pour-hint = The furnace pours only the selected alloy, and only while it is liquid; everything else stays inside.
dystopia-arc-furnace-ui-no-pour = Don't pour
dystopia-arc-furnace-ui-slag = Slag
dystopia-arc-furnace-ui-slag-count = Skimmed lumps: { $count } of { $max }.
dystopia-arc-furnace-ui-eject-slag = Unload slag
