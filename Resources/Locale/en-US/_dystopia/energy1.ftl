# Dystopia: энергетика, шаг 1 — котёл, турбина, свинец, заражение, лучевая болезнь (gen_energy1.py).

dystopia-steamboiler-ports = [color=#78bee1]Water[/color]: a liquid exporter right against the blue tap on the left (middle cell). [color=#c8e4f0]Steam[/color]: a gas pipe from the branch pipe at the back centre (white flange). [color=#d2ae68]Firebox[/color]: front centre, where a fuel oil burner or a conveyor inserter faces.
dystopia-steamturbine-ports = [color=#c8e4f0]Fresh steam[/color]: a gas pipe into the branch pipe at the middle of the back end (arrow in). [color=#c8e4f0]Exhaust[/color]: out of the branch pipe in a corner of the front end (arrow out), then to a cooling tower. [color=#e0b830]Power[/color]: a high-voltage cable under the yellow terminal in the other front corner. [color=#d2ae68]Oil[/color]: an exporter right against the brass tap on the side near the front end.
reagent-name-dystopia-radioactive-dust = radioactive dust
reagent-desc-dystopia-radioactive-dust = Grey-green dust left after an accident. It shines while it lies there: mop it up or neutralise it with space cleaner.
reagent-physical-desc-dystopia-dusty = dusty
disease-effect-popup-hair-loss = A clump of hair comes away in your hand.
disease-effect-popup-bleeding-gums = You taste blood: your gums are bleeding.
dystopia-machine-flip-verb = Turn around 180°
dystopia-machine-flip-start = You start turning the machine around on its bed…
dystopia-machine-flip-done = Turned around: the ends have swapped.
dystopia-boiler-no-fuel = There is nothing to burn in the firebox.
dystopia-boiler-lit = The firebox catches.
dystopia-boiler-fuel-full = The firebox is full.
dystopia-boiler-fuel-added = Fuel in the firebox: { $seconds } s.
dystopia-boiler-examine-lit = [color=#ff9a50]The firebox is burning[/color], fuel for { $fuel } s.
dystopia-boiler-examine-cold = The firebox is out, fuel for { $fuel } s.
dystopia-boiler-examine-state = Steam { $temperature } °C, pressure { $pressure } kPa, warmed up { $heat }%.
dystopia-boiler-examine-danger = [color=#e05040]The drum is groaning under pressure![/color]
dystopia-boiler-ui-title = Steam boiler
dystopia-boiler-ui-extinguish = PUT OUT FIRE
dystopia-boiler-ui-firebox-header = Firebox
dystopia-boiler-ui-fuel = Fuel: { $fuel } of { $max } s of burning · use { $rate } s/s · lasts { $minutes } min
dystopia-boiler-ui-fuel-hint = Coal, coke, planks: by hand or an inserter; liquid fuel: a fuel oil burner at the door. Light it with a flame (lighter, matches, torch).
dystopia-boiler-ui-mode-header = Mode: steam temperature
dystopia-boiler-ui-mode-info = Steam { $steam } mol/s · fuel { $fuel } s of burning per second
dystopia-boiler-ui-mode-hint = Hotter steam carries more energy per mole to the turbine but burns more fuel.
dystopia-boiler-ui-drum-header = Drum
dystopia-boiler-ui-heat = Warmed up: { $heat }% (steam from 40%)
dystopia-boiler-ui-pressure = Pressure: { $pressure } kPa · alarm from { $warn } · bursts from { $max }
dystopia-boiler-ui-steam = Steam: { $produced } mol/s made → { $out } mol/s into the pipe (pipe { $pipe } kPa, drum { $drum } mol)
dystopia-boiler-ui-steam-disconnected = The steam branch pipe is not connected: steam builds up in the drum ({ $drum } mol)!
dystopia-boiler-ui-vent-open = Vent steam
dystopia-boiler-ui-vent-close = Close valve
dystopia-boiler-ui-vent-hint = Vented steam is hot: the room will heat up.
dystopia-boiler-ui-water-header = Water and boiler
dystopia-boiler-ui-water = Water: { $amount } of { $max } · use { $rate } u/s
dystopia-boiler-ui-wear = Damage: { $wear }% (repair by welding)
dystopia-boiler-ui-water-hint = Without water a burning firebox burns through the tubes. Water: an exporter at the blue tap on the left.
dystopia-boiler-ui-status-cold = COLD
dystopia-boiler-ui-status-warming = WARMING UP
dystopia-boiler-ui-status-producing = MAKING STEAM
dystopia-boiler-ui-status-nowater = NO WATER!
dystopia-boiler-ui-status-danger = DANGEROUS PRESSURE!
dystopia-turbine-examine-state = Speed { $speed }%, power { $power } kW.
dystopia-turbine-examine-dry = [color=#e05040]The bearings are screaming: no oil![/color]
dystopia-turbine-ui-title = Steam turbine
dystopia-turbine-ui-start = Start
dystopia-turbine-ui-stop = Stop
dystopia-turbine-ui-power-header = Generator
dystopia-turbine-ui-power = Power: { $power } kW (limit { $max } kW)
dystopia-turbine-ui-speed = Speed: { $speed }% · steam { $flow } of { $max } mol/s
dystopia-turbine-ui-valve = Control valve: { $valve }%
dystopia-turbine-ui-valve-hint = Power ∝ steam flow × temperature drop. The more the turbine takes, the lower the boiler pressure.
dystopia-turbine-ui-steam-header = Steam
dystopia-turbine-ui-inlet = Inlet: { $temperature } °C, { $pressure } kPa (blades take up to { $max } °C)
dystopia-turbine-ui-exhaust = Exhaust: { $temperature } °C, { $pressure } kPa (stalls from { $max } kPa)
dystopia-turbine-ui-links = Connected: fresh steam — { $inlet }, exhaust — { $outlet }, cable — { $power }
dystopia-turbine-ui-link-yes = yes
dystopia-turbine-ui-link-no = NO
dystopia-turbine-ui-condition-header = Oil and wear
dystopia-turbine-ui-oil = Oil: { $amount } of { $max } · use { $rate } u/s
dystopia-turbine-ui-wear = Wear: { $wear }% (at 100% the rotor seizes; repair by welding)
dystopia-turbine-ui-oil-hint = Lubricating oil: an exporter at the brass tap, or a bucket. Without oil wear is 25 times faster.
dystopia-turbine-ui-status-off = STOPPED
dystopia-turbine-ui-status-running = RUNNING
dystopia-turbine-ui-status-nosteam = NO STEAM
dystopia-turbine-ui-status-blocked = EXHAUST BLOCKED!
dystopia-turbine-ui-status-seized = ROTOR SEIZED
health-analyzer-window-radiation-line = [color=#e0c040]Radiation sickness:[/color] dose { $dose } rad, severity { $severity }% (sick from 40 rad, full force from 400)
