export const themePresets = [
	{ id: 'zinc', name: 'Zinc', colors: ['#52525b'] },
	{ id: 'slate', name: 'Slate', colors: ['#64748b'] },
	{ id: 'stone', name: 'Stone', colors: ['#78716c'] },
	{ id: 'gray', name: 'Gray', colors: ['#6b7280'] },
	{ id: 'neutral', name: 'Neutral', colors: ['#737373'] },
	{ id: 'red', name: 'Red', colors: ['#dc2626'] },
	{ id: 'rose', name: 'Rose', colors: ['#be185d', '#fce7f3', '#fff1f2'] },
	{ id: 'sunset', name: 'Orange', colors: ['#c2410c', '#ffedd5', '#fff7ed'] },
	{ id: 'forest', name: 'Green', colors: ['#047857', '#d1fae5', '#f0fdf4'] },
	{ id: 'ocean', name: 'Blue', colors: ['#2563eb', '#dbeafe', '#eff6ff'] },
	{ id: 'yellow', name: 'Yellow', colors: ['#ca8a04'] },
	{ id: 'iris', name: 'Violet', colors: ['#7c3aed', '#ede9fe', '#faf5ff'] },
	{ id: 'amber', name: 'Amber', colors: ['#d97706'] },
	{ id: 'purple', name: 'Purple', colors: ['#9333ea'] },
	{ id: 'teal', name: 'Teal', colors: ['#0d9488'] }
] as const;

export type ThemePreset = (typeof themePresets)[number]['id'];
export const defaultThemePreset: ThemePreset = 'ocean';

export function isThemePreset(value: unknown): value is ThemePreset {
	return themePresets.some((preset) => preset.id === value);
}

export const appearanceOptions = {
	radius: ['none', 'small', 'medium', 'large'],
	shadow: ['none', 'small', 'medium', 'large'],
	font: ['small', 'default'],
	chart: ['default', 'palette', 'sapphire', 'ruby', 'emerald', 'daylight', 'midnight']
} as const;

export type Appearance = {
	[K in keyof typeof appearanceOptions]: (typeof appearanceOptions)[K][number];
};

export const defaultAppearance: Appearance = {
	radius: 'medium',
	shadow: 'small',
	font: 'default',
	chart: 'default'
};

export function parseAppearance(value: unknown): Appearance {
	const result = { ...defaultAppearance };
	if (!value || typeof value !== 'object') return result;
	for (const key of Object.keys(appearanceOptions) as (keyof Appearance)[]) {
		const candidate = Reflect.get(value, key);
		if (appearanceOptions[key].some((option) => option === candidate)) {
			Object.assign(result, { [key]: candidate });
		}
	}
	return result;
}
