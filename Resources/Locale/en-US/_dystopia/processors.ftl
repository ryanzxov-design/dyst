# Dystopia: ore processing machines and conveyor devices.

dystopia-processor-work-verb = Work
dystopia-processor-work-crank = Turn crank
dystopia-processor-work-turn = Turn millstones
dystopia-processor-work-rock = Rock the sluice
dystopia-processor-not-accepted = { CAPITALIZE(THE($item)) } doesn't go in here.
dystopia-processor-full = The hopper is full.
dystopia-processor-loaded = You load { THE($item) } ({ $loaded } of { $capacity }).
dystopia-processor-empty = There is nothing to process inside.
dystopia-processor-not-enough = Not enough for a batch.
dystopia-processor-no-reagents = Not enough water.
dystopia-processor-output-full = The output is full. Unload it.
dystopia-processor-not-anchored = The machine is loose. Bolt it to the floor with a wrench.
dystopia-processor-unload-verb = Unload output
dystopia-processor-unloaded = You unload the output.
dystopia-processor-no-output = There is no finished output.
dystopia-processor-eject-verb = Empty out input
dystopia-processor-ejected = You empty out the input.
dystopia-processor-tank-draw-verb = Draw from tank ({ $amount }u)
dystopia-processor-tank-drawn = You draw { $amount }u from the tank.
dystopia-processor-tank-dump-verb = Drain tank onto the floor
dystopia-processor-tank-dumped = You drain the tank onto the floor.
dystopia-processor-examine-load = Input: { $loaded } of { $capacity }.
dystopia-processor-examine-output = Output: { $units } of { $capacity } (unload with an empty hand or the verb).
dystopia-processor-examine-tank = Tank: { $volume } of { $max }u: { $contents }.
dystopia-processor-examine-tank-empty = The tank is empty (holds { $max }u).
dystopia-processor-examine-tank-entry = [color={ $color }]{ $name }[/color] { $amount }
dystopia-processor-examine-cycle = { $batches } { $batches ->
    [one] batch
   *[other] batches
} per { $seconds } s cycle.
dystopia-processor-examine-loose = [color=#d0a040]Not bolted to the floor.[/color]
dystopia-processor-examine-unpowered = [color=#d05050]No power.[/color]

dystopia-conveyor-filter-add = Filter: add { $item }
dystopia-conveyor-filter-remove = Filter: remove { $item }
dystopia-conveyor-sort-green-add = Green output: { $item }
dystopia-conveyor-sort-green-remove = Green output: remove { $item }
dystopia-conveyor-sort-blue-add = Blue output: { $item }
dystopia-conveyor-sort-blue-remove = Blue output: remove { $item }
dystopia-conveyor-clear-verb = Clear filter
dystopia-conveyor-cleared = Filter cleared.
dystopia-conveyor-to-whitelist = Mode: whitelist
dystopia-conveyor-to-blacklist = Mode: blacklist
dystopia-conveyor-mode-whitelist = Now only what is on the list.
dystopia-conveyor-mode-blacklist = Now everything except what is on the list.
dystopia-conveyor-list-empty = empty
dystopia-conveyor-examine-extract = Takes from the storage behind it and puts on the tile in front (along the arrow). Leaves locked and welded storage alone.
dystopia-conveyor-examine-insert = Takes from the tile behind it and puts into the storage in front (along the arrow). Leaves locked and welded storage alone.
dystopia-conveyor-examine-whitelist = Whitelist, only: { $items }.
dystopia-conveyor-examine-blacklist = Blacklist, everything except: { $items }.
dystopia-conveyor-examine-sort = Takes from the tile behind it. Everything else goes to the [color=#e0c040]yellow[/color] output (forward).
dystopia-conveyor-examine-green = [color=#50c050]Green[/color] output: { $items }.
dystopia-conveyor-examine-blue = [color=#5080e0]Blue[/color] output: { $items }.

# Conveyor device menu
dystopia-industry-ui-sub = City Industrial Service  ·  Factory equipment
dystopia-industry-ui-slogan = The plan is law. The quota is duty.
dystopia-conveyor-ui-title = Conveyor device
dystopia-conveyor-ui-extract-hint = Takes out of the storage behind it and puts on the tile the arrow points to.
dystopia-conveyor-ui-insert-hint = Takes from the tile behind it and puts into the storage the arrow points to.
dystopia-conveyor-ui-sort-hint = Takes from the tile behind it and splits into outputs: green is left, blue is right, yellow is forward.
dystopia-conveyor-ui-whitelist = Whitelist
dystopia-conveyor-ui-blacklist = Blacklist
dystopia-conveyor-ui-list-white = Let through only
dystopia-conveyor-ui-list-black = Let through everything except
dystopia-conveyor-ui-green = Green output (left)
dystopia-conveyor-ui-blue = Blue output (right)
dystopia-conveyor-ui-yellow = Yellow output (forward)
dystopia-conveyor-ui-yellow-hint = Everything not sent to green or blue.
dystopia-conveyor-ui-add-held = Add held item
dystopia-conveyor-ui-clear = Clear
dystopia-conveyor-ui-empty = Empty.
dystopia-conveyor-ui-remove = Remove
dystopia-conveyor-ui-search = Item search
dystopia-conveyor-ui-search-placeholder = Item name…
dystopia-conveyor-ui-search-hint = Type at least two letters of the name.
dystopia-conveyor-ui-nothing = Nothing found.
dystopia-conveyor-ui-add = Add
dystopia-conveyor-ui-to-green = To green
dystopia-conveyor-ui-to-blue = To blue
dystopia-conveyor-ui-hand-empty = Your hand is empty.
