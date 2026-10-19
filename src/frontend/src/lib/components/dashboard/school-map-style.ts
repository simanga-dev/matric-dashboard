import { Circle, Fill, Stroke, Style } from 'ol/style';

/** Resolve semantic CSS tokens for OpenLayers canvas styles, just like performance markers. */
export function spatialMapStyle(draft: boolean): Style {
	const tokens =
		typeof document === 'undefined' ? null : getComputedStyle(document.documentElement);
	const color =
		tokens?.getPropertyValue(draft ? '--primary' : '--muted-foreground').trim() || '215 20% 45%';
	const background = tokens?.getPropertyValue('--background').trim() || '0 0% 100%';
	return new Style({
		fill: new Fill({ color: `hsl(${color} / ${draft ? '0.2' : '0.12'})` }),
		stroke: new Stroke({
			color: `hsl(${color})`,
			width: draft ? 3 : 2,
			lineDash: draft ? [8, 4] : undefined
		}),
		image: new Circle({
			radius: 8,
			fill: new Fill({ color: `hsl(${color})` }),
			stroke: new Stroke({ color: `hsl(${background})`, width: 2 })
		})
	});
}
