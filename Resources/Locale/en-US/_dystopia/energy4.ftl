# Dystopia: энергетика, шаг 4 — нефтепровод, ЛЭП, трансформаторы (gen_energy4.py).

stack-dystopia-pipeline-section = trunk pipe sections
stack-dystopia-pylon-wire = power line wire
dystopia-pylon-first = The wire end is fixed. Now poke the second pylon.
dystopia-pylon-already = These pylons are already joined.
dystopia-pylon-other-grid = The pylons must stand in the same world.
dystopia-pylon-too-far = A { $span }-tile span is longer than { $max }.
dystopia-pylon-full = There is no room for another wire on the pylon.
dystopia-pylon-not-enough = Not enough wire: { $need } needed.
dystopia-pylon-linked = Wire strung: a { $span }-tile span.
dystopia-pylon-cut = The wires are down.
dystopia-pylon-examine = Wires: { $links } of { $max }, spans up to { $span } tiles.
dystopia-transformer-examine = Passing up { $power } kW of { $max } kW, { $loss }% loss.
dystopia-pipeline-first = The pipe starts at this terminal. Now poke the second one.
dystopia-pipeline-already = This terminal already has a pipeline.
dystopia-pipeline-other-grid = The terminals must stand in the same world.
dystopia-pipeline-too-far = A { $length }-tile route is longer than { $max }.
dystopia-pipeline-gap = The route cannot cross empty space.
dystopia-pipeline-not-enough = Not enough sections: { $need } needed.
dystopia-pipeline-linked = Pipeline laid: { $length } tiles, { $sections } sections used.
dystopia-pipeline-unlinked = The pipeline is taken up; the sections lie by the terminal.
dystopia-pipeline-verb-unlink = Take up the pipeline
dystopia-pipeline-examine-unlinked = No pipeline is laid.
dystopia-pipeline-examine-linked = Route { $length } tiles. Mode: { $mode }.
dystopia-pipeline-examine-leaks = [color=#e0b830]Leaking manholes: { $leaks }.[/color]
dystopia-pipeline-examine-broken = [color=#e05040]The pipe has burst![/color]
dystopia-pipeline-mode-send = Send
dystopia-pipeline-mode-receive = Receive
dystopia-pipeline-mode-stop = Stop
dystopia-pipeline-ui-title = Pipeline terminal
dystopia-pipeline-ui-linked = Route: { $length } tiles
dystopia-pipeline-ui-unlinked = No pipeline: poke this and the other terminal with a stack of sections
dystopia-pipeline-ui-ok = The pipe is sound
dystopia-pipeline-ui-leaks = LEAK: { $leaks } manholes, weld them
dystopia-pipeline-ui-broken = PIPE BURST: everything pours out
dystopia-pipeline-ui-no-power = No power: the pump is idle
dystopia-pipeline-ui-mode = Mode
dystopia-pipeline-ui-rate = Pump: { $rate } of { $max } u/s
dystopia-pipeline-ui-tank = Tank
dystopia-pipeline-ui-volume = { $volume } / { $max } u
dystopia-pipeline-ui-empty = empty
dystopia-pipeline-ui-flow = Flow: { $flow } u/s
