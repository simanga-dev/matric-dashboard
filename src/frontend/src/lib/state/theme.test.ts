import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import {
	defaultThemePreset,
	themePresets,
	appearanceOptions,
	defaultAppearance,
	parseAppearance,
	type Appearance
} from '$lib/config/theme-presets';
import {
	getTheme,
	getAppearance,
	setAppearance,
	getThemePreset,
	initTheme,
	setTheme,
	setThemePreset,
	toggleTheme
} from './theme.svelte';

describe('appearance preferences', () => {
	let storage: Map<string, string>;
	let dark: boolean;
	let classes: Set<string>;
	let root: {
		dataset: Record<string, string>;
		style: Record<string, string>;
		classList: { add: (value: string) => void; remove: (value: string) => void };
	};
	let mediaQuery: {
		readonly matches: boolean;
		addEventListener: ReturnType<typeof vi.fn>;
		removeEventListener: ReturnType<typeof vi.fn>;
	};
	let cleanup: (() => void) | undefined;

	beforeEach(() => {
		storage = new Map();
		dark = false;
		classes = new Set();
		root = {
			dataset: {},
			style: {},
			classList: {
				add: (value) => classes.add(value),
				remove: (value) => classes.delete(value)
			}
		};
		mediaQuery = {
			get matches() {
				return dark;
			},
			addEventListener: vi.fn(),
			removeEventListener: vi.fn()
		};
		vi.stubGlobal('document', { documentElement: root });
		vi.stubGlobal('window', { matchMedia: vi.fn(() => mediaQuery) });
		vi.stubGlobal('localStorage', {
			getItem: (key: string) => storage.get(key) ?? null,
			setItem: (key: string, value: string) => storage.set(key, value)
		});
		cleanup = initTheme();
	});

	afterEach(() => {
		cleanup?.();
		vi.unstubAllGlobals();
	});

	it('defaults to Ocean with system display mode', () => {
		expect(getThemePreset()).toBe(defaultThemePreset);
		expect(getTheme()).toBe('system');
		expect(root.dataset.themePreset).toBe('ocean');
		expect(root.style.colorScheme).toBe('light');
	});

	it.each(themePresets)(
		'applies and persists the $name palette without changing mode',
		({ id }) => {
			setTheme('dark');
			setThemePreset(id);
			expect(getThemePreset()).toBe(id);
			expect(root.dataset.themePreset).toBe(id);
			expect(storage.get('theme-preset')).toBe(id);
			expect(getTheme()).toBe('dark');
			expect(classes.has('dark')).toBe(true);
		}
	);

	it('restores both saved preferences', () => {
		storage.set('theme', 'dark');
		storage.set('theme-preset', 'iris');
		cleanup?.();
		cleanup = initTheme();
		expect(getTheme()).toBe('dark');
		expect(getThemePreset()).toBe('iris');
		expect(root.style.colorScheme).toBe('dark');
	});

	const appearanceCases = (Object.keys(appearanceOptions) as (keyof Appearance)[]).flatMap((key) =>
		appearanceOptions[key].map((value) => ({ key, value }))
	);

	it.each(appearanceCases)('applies and restores $key = $value', ({ key, value }) => {
		setAppearance(key, value);
		expect(getAppearance()[key]).toBe(value);
		const datasetKey = 'theme' + key[0]?.toUpperCase() + key.slice(1);
		expect(root.dataset[datasetKey]).toBe(value);
		expect(JSON.parse(storage.get('appearance') ?? '{}')[key]).toBe(value);
		cleanup?.();
		cleanup = initTheme();
		expect(getAppearance()[key]).toBe(value);
	});

	it('validates each appearance preference independently', () => {
		expect(
			parseAppearance({ radius: 'large', shadow: 'invalid', font: 'small', chart: 'ruby' })
		).toEqual({ radius: 'large', shadow: 'small', font: 'small', chart: 'ruby' });
		expect(parseAppearance(null)).toEqual(defaultAppearance);
		expect(parseAppearance('invalid')).toEqual(defaultAppearance);
	});

	it('ignores malformed appearance JSON', () => {
		storage.set('appearance', '{broken');
		cleanup?.();
		cleanup = initTheme();
		expect(getAppearance()).toEqual(defaultAppearance);
	});

	it('falls back to defaults for invalid stored values', () => {
		storage.set('theme', 'invalid');
		storage.set('theme-preset', 'invalid');
		cleanup?.();
		cleanup = initTheme();
		expect(getTheme()).toBe('system');
		expect(getThemePreset()).toBe('ocean');
	});

	it('keeps working when browser storage is blocked', () => {
		vi.stubGlobal('localStorage', {
			getItem: () => {
				throw new Error('Blocked');
			},
			setItem: () => {
				throw new Error('Blocked');
			}
		});
		cleanup?.();
		cleanup = initTheme();
		setThemePreset('forest');
		setTheme('dark');
		setAppearance('radius', 'large');
		expect(root.dataset.themePreset).toBe('forest');
		expect(root.dataset.themeRadius).toBe('large');
		expect(classes.has('dark')).toBe(true);
	});

	it('follows system changes only in system mode', () => {
		const onChange = mediaQuery.addEventListener.mock.calls[0]?.[1];
		if (!onChange) throw new Error('Missing system theme listener');
		dark = true;
		onChange();
		expect(classes.has('dark')).toBe(true);
		setTheme('light');
		onChange();
		expect(classes.has('dark')).toBe(false);
	});

	it('keeps the selected palette when toggling display mode', () => {
		setThemePreset('rose');
		toggleTheme();
		expect(getTheme()).toBe('dark');
		toggleTheme();
		expect(getTheme()).toBe('light');
		expect(getThemePreset()).toBe('rose');
	});

	it('removes the system listener on cleanup', () => {
		cleanup?.();
		expect(mediaQuery.removeEventListener).toHaveBeenCalledWith(
			'change',
			mediaQuery.addEventListener.mock.calls[0]?.[1]
		);
		cleanup = undefined;
	});

	describe('pre-paint startup script', () => {
		const html = readFileSync(new URL('../../app.html', import.meta.url), 'utf8');
		const script = html.match(/<script[^>]*>([\s\S]*?)<\/script>/)?.[1];

		function runStartup() {
			if (!script) throw new Error('Missing appearance startup script');
			runInNewContext(script, { document, window, localStorage });
		}

		it.each(themePresets)('restores $name before hydration', ({ id }) => {
			storage.set('theme-preset', id);
			storage.set('theme', 'dark');
			runStartup();
			expect(root.dataset.themePreset).toBe(id);
			expect(classes.has('dark')).toBe(true);
		});

		it.each(appearanceCases)('restores $key = $value before hydration', ({ key, value }) => {
			storage.set('appearance', JSON.stringify({ [key]: value }));
			runStartup();
			const startupData = { ...root.dataset };
			cleanup?.();
			cleanup = initTheme();
			expect(root.dataset).toEqual(startupData);
		});

		it.each(['{broken', 'null', '123', '"invalid"', '{"radius":"invalid","chart":"ruby"}'])(
			'handles invalid stored appearance %s before hydration',
			(saved) => {
				storage.set('appearance', saved);
				runStartup();
				const startupData = { ...root.dataset };
				cleanup?.();
				cleanup = initTheme();
				expect(root.dataset).toEqual(startupData);
			}
		);

		it('uses system mode and the default preset for invalid values', () => {
			storage.set('theme', 'invalid');
			storage.set('theme-preset', 'invalid');
			dark = true;
			runStartup();
			expect(root.dataset.themePreset).toBe(defaultThemePreset);
			expect(classes.has('dark')).toBe(true);
		});
	});
});
