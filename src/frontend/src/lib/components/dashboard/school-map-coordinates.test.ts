import { describe, expect, it } from 'vitest';
import { parseMapCoordinates } from './school-map-coordinates';

describe('pasted map coordinates', () => {
	it.each([
		['-30.86536, 29.85241', -30.86536, 29.85241],
		['-30.86536,29.85241', -30.86536, 29.85241],
		['  -26.2041 28.0473  ', -26.2041, 28.0473],
		['https://www.google.com/maps/place/Example/@-30.86536,29.85241,17z', -30.86536, 29.85241],
		['https://www.google.com/maps/place/Example/data=!4m2!3d-26.2041!4d28.0473', -26.2041, 28.0473],
		['https://maps.google.com/?q=-30.86536,29.85241', -30.86536, 29.85241]
	])('reads %s', (input, latitude, longitude) => {
		expect(parseMapCoordinates(input)).toEqual({ latitude, longitude });
	});

	it.each([
		'',
		'maps.app.goo.gl/abc123',
		'https://maps.app.goo.gl/abc123',
		'-91, 29',
		'-30, 181',
		'not coordinates',
		'30.123'
	])('rejects %s', (input) => {
		expect(parseMapCoordinates(input)).toBeNull();
	});
});
