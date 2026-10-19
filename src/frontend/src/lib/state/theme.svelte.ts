import { browser } from '$app/environment';
import {
	defaultThemePreset,
	isThemePreset,
	type ThemePreset,
	defaultAppearance,
	parseAppearance,
	type Appearance
} from '$lib/config/theme-presets';

export type Theme = 'light' | 'dark' | 'system';

let theme = $state<Theme>('system');
let preset = $state<ThemePreset>(defaultThemePreset);
let appearance = $state<Appearance>({ ...defaultAppearance });

export function getAppearance() {
	return appearance;
}

function applyAppearance() {
	if (!browser) return;
	const data = document.documentElement.dataset;
	data.themeRadius = appearance.radius;
	data.themeShadow = appearance.shadow;
	data.themeFont = appearance.font;
	data.themeChart = appearance.chart;
}

export function setAppearance<K extends keyof Appearance>(key: K, value: Appearance[K]) {
	appearance = parseAppearance({ ...appearance, [key]: value });
	if (browser) {
		try {
			localStorage.setItem('appearance', JSON.stringify(appearance));
		} catch {
			// Apply preferences even when storage is blocked.
		}
		applyAppearance();
	}
}

export function getThemePreset() {
	return preset;
}

export function setThemePreset(newPreset: ThemePreset) {
	if (!isThemePreset(newPreset)) return;
	preset = newPreset;
	if (browser) {
		try {
			localStorage.setItem('theme-preset', newPreset);
		} catch {
			// The selection still works when storage is unavailable.
		}
		document.documentElement.dataset.themePreset = newPreset;
	}
}

export function getTheme() {
	return theme;
}

export function setTheme(newTheme: Theme) {
	theme = newTheme;
	if (browser) {
		try {
			localStorage.setItem('theme', newTheme);
		} catch {
			// Ignore write errors
		}
		applyTheme(newTheme);
	}
}

export function toggleTheme() {
	if (!browser) return;

	const current = getTheme();
	if (current === 'light') {
		setTheme('dark');
	} else if (current === 'dark') {
		setTheme('light');
	} else {
		// system
		if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
			setTheme('light');
		} else {
			setTheme('dark');
		}
	}
}

function applyTheme(t: Theme) {
	if (!browser) return;

	const root = document.documentElement;
	const isDark =
		t === 'dark' || (t === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
	root.style.colorScheme = isDark ? 'dark' : 'light';

	if (isDark) {
		root.classList.add('dark');
	} else {
		root.classList.remove('dark');
	}
}

export function initTheme() {
	if (!browser) return;

	theme = 'system';
	preset = defaultThemePreset;
	appearance = { ...defaultAppearance };
	try {
		const saved = localStorage.getItem('theme');
		if (saved === 'light' || saved === 'dark' || saved === 'system') {
			theme = saved;
		}
		const savedPreset = localStorage.getItem('theme-preset');
		if (isThemePreset(savedPreset)) {
			preset = savedPreset;
		}
		try {
			appearance = parseAppearance(JSON.parse(localStorage.getItem('appearance') ?? 'null'));
		} catch {
			appearance = { ...defaultAppearance };
		}
	} catch {
		theme = 'system';
	}
	applyTheme(theme);
	document.documentElement.dataset.themePreset = preset;
	applyAppearance();

	const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
	const handleChange = () => {
		if (theme === 'system') {
			applyTheme('system');
		}
	};

	mediaQuery.addEventListener('change', handleChange);

	return () => {
		mediaQuery.removeEventListener('change', handleChange);
	};
}
