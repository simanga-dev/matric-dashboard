<script lang="ts">
	import { Sun, Moon, Monitor } from '@lucide/svelte';
	import { themePresets, appearanceOptions, type Appearance } from '$lib/config/theme-presets';
	import {
		getTheme,
		setTheme,
		getThemePreset,
		setThemePreset,
		getAppearance,
		setAppearance
	} from '$lib/state/theme.svelte';
	import * as m from '$lib/paraglide/messages';

	const modes = [
		{ id: 'light', label: m.theme_light, icon: Sun },
		{ id: 'dark', label: m.theme_dark, icon: Moon },
		{ id: 'system', label: m.theme_system, icon: Monitor }
	] as const;
	const sections = [
		{ key: 'radius', label: m.theme_radius },
		{ key: 'shadow', label: m.theme_shadows },
		{ key: 'font', label: m.theme_font },
		{ key: 'chart', label: m.theme_chart }
	] as const;
	const labels = {
		none: m.theme_none,
		small: m.theme_small,
		medium: m.theme_medium,
		large: m.theme_large,
		default: m.theme_default,
		palette: m.theme_chartPalette,
		sapphire: m.theme_chartSapphire,
		ruby: m.theme_chartRuby,
		emerald: m.theme_chartEmerald,
		daylight: m.theme_chartDaylight,
		midnight: m.theme_chartMidnight
	};
	const chartColors: Record<Appearance['chart'], string | undefined> = {
		default: undefined,
		palette: '#ea580c',
		sapphire: '#2563eb',
		ruby: '#e11d48',
		emerald: '#059669',
		daylight: '#b7791f',
		midnight: '#818cf8'
	};
</script>

<div class="customize-options space-y-7">
	<fieldset class="min-w-0">
		<legend class="mb-2.5 text-sm font-medium">{m.theme_palette()}</legend>
		<div class="grid grid-cols-3 gap-2">
			{#each themePresets as preset (preset.id)}
				<button
					type="button"
					class="customize-choice"
					title={preset.name}
					aria-pressed={getThemePreset() === preset.id}
					onclick={() => setThemePreset(preset.id)}
				>
					<span
						class="size-3.5 shrink-0 rounded-full"
						style:background-color={preset.colors[0]}
						aria-hidden="true"
					></span>
					<span class="truncate">{preset.name}</span>
				</button>
			{/each}
		</div>
	</fieldset>
	{#each sections as section (section.key)}
		<fieldset class="min-w-0">
			<legend class="mb-2.5 text-sm font-medium">{section.label()}</legend>
			<div class="grid grid-cols-3 gap-2">
				{#each appearanceOptions[section.key] as option (option)}
					<button
						type="button"
						class="customize-choice"
						title={labels[option]()}
						aria-pressed={getAppearance()[section.key] === option}
						onclick={() => setAppearance(section.key, option)}
					>
						{#if section.key === 'chart' && chartColors[option as Appearance['chart']]}
							<span
								class="size-3.5 shrink-0 rounded-full"
								style:background-color={chartColors[option as Appearance['chart']]}
								aria-hidden="true"
							></span>
						{/if}
						<span class="truncate">{labels[option]()}</span>
					</button>
				{/each}
			</div>
		</fieldset>
	{/each}
	<fieldset class="min-w-0">
		<legend class="mb-2.5 text-sm font-medium">{m.theme_mode()}</legend>
		<div class="grid grid-cols-3 gap-2">
			{#each modes as mode (mode.id)}
				<button
					type="button"
					class="customize-choice"
					aria-pressed={getTheme() === mode.id}
					onclick={() => setTheme(mode.id)}
				>
					<mode.icon class="size-3.5 shrink-0" aria-hidden="true" />
					<span class="truncate">{mode.label()}</span>
				</button>
			{/each}
		</div>
	</fieldset>
	<p class="text-xs text-muted-foreground">{m.theme_savedLocally()}</p>
</div>
