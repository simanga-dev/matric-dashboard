import { describe, expect, it } from 'vitest';
import {
	hasOverviewState,
	overviewHistoryState,
	parseOverviewState,
	revealOverviewSchool,
	serializeOverviewState
} from './overview-url';
import type { School } from '$lib/types/dashboard';

const key = (province: string, circuit: string) => JSON.stringify([province, circuit]);
const b64url = (text: string) =>
	Buffer.from(text, 'utf8')
		.toString('base64')
		.replace(/\+/g, '-')
		.replace(/\//g, '_')
		.replace(/=+$/, '');

describe('overview URL state', () => {
	it('retains the current workspace for a plain navigation without a history snapshot', () => {
		const current = parseOverviewState('?school=2002&open=Gauteng');
		expect(overviewHistoryState('', undefined, current)).toEqual(current);
	});

	it('prefers an explicit empty history snapshot over a saved workspace', () => {
		const current = parseOverviewState('?school=2002&open=Gauteng');
		const empty = parseOverviewState('');
		expect(overviewHistoryState('', empty, current)).toEqual(empty);
		expect(overviewHistoryState('?school=2002', empty, current)).toEqual(empty);
	});

	it('adopts explicit shared-link state instead of retaining the current workspace', () => {
		const current = parseOverviewState('?school=2002&open=Gauteng');
		expect(overviewHistoryState('?school=1001', undefined, current)).toEqual(
			parseOverviewState('?school=1001')
		);
	});
	it("restores a history entry snapshot over SvelteKit's stale shallow page URL", () => {
		const snapshot = parseOverviewState('?school=2002&open=Gauteng');
		expect(overviewHistoryState('?school=1001', snapshot)).toEqual(snapshot);
		expect(overviewHistoryState('?school=1001')).toEqual(parseOverviewState('?school=1001'));
	});

	it('restores an explicitly empty history entry without reviving a stale selection', () => {
		const empty = parseOverviewState('');
		expect(overviewHistoryState('?school=1001&open=Gauteng', empty)).toEqual(empty);
	});

	it('reveals a reselected school after collapse while preserving unrelated branches', () => {
		const school: School = {
			id: '1001',
			name: 'Example',
			province: 'Gauteng',
			circuit: 'East',
			totalWrote: 10,
			totalPassed: 9,
			passRate: 90,
			totalAchieved: null
		};
		const collapsed = parseOverviewState('?school=1001&open=Limpopo');
		const revealed = revealOverviewSchool(collapsed, school);
		expect(revealed.expandedProvinces).toEqual(['Limpopo', 'Gauteng']);
		expect(revealed.expandedCircuits).toEqual([key('Gauteng', 'East')]);
		expect(revealed.province).toBeNull();
		expect(revealOverviewSchool(revealed, school)).toEqual(revealed);
		expect(collapsed.expandedProvinces).toEqual(['Limpopo']);
		expect(parseOverviewState(serializeOverviewState(revealed))).toEqual(revealed);
	});
	it('round-trips the selected school, province and expanded branches', () => {
		const query = serializeOverviewState({
			school: '1001',
			province: null,
			district: null,
			schools: false,
			expandedProvinces: ['Gauteng'],
			expandedCircuits: [key('Gauteng', 'Johannesburg East')]
		});

		expect(parseOverviewState(query)).toEqual({
			school: '1001',
			province: null,
			district: null,
			schools: false,
			expandedProvinces: ['Gauteng'],
			expandedCircuits: [key('Gauteng', 'Johannesburg East')]
		});
	});

	it('encodes the open state as URL-safe base64 without raw JSON', () => {
		const token = b64url(
			JSON.stringify({ p: ['EASTERN CAPE'], c: [['EASTERN CAPE', 'BUFFALO CITY']] })
		);
		const query = serializeOverviewState({
			school: null,
			province: null,
			district: null,
			schools: false,
			expandedProvinces: ['EASTERN CAPE'],
			expandedCircuits: [key('EASTERN CAPE', 'BUFFALO CITY')]
		});
		expect(query).toBe(`open=${token}`);
		expect(query).not.toMatch(/[{}%:"+\s]/);
		expect(parseOverviewState(query).expandedCircuits).toEqual([
			key('EASTERN CAPE', 'BUFFALO CITY')
		]);
	});

	it('keeps school and province readable', () => {
		const query = serializeOverviewState({
			school: '200201049',
			province: 'Western Cape',
			district: null,
			schools: false,
			expandedProvinces: [],
			expandedCircuits: []
		});
		expect(query).toBe('school=200201049&province=Western%20Cape');
	});

	it('round-trips the open school directory dialog', () => {
		const state = {
			school: null,
			province: null,
			district: null,
			schools: true,
			expandedProvinces: [],
			expandedCircuits: []
		};
		const query = serializeOverviewState(state);
		expect(query).toBe('schools=1');
		expect(parseOverviewState(query)).toEqual(state);
		expect(hasOverviewState(state)).toBe(true);
		expect(parseOverviewState('?schools=1&school=1001').schools).toBe(true);
		expect(parseOverviewState('?schools=0').schools).toBe(false);
	});

	it('keeps the dialog state when revealing a school', () => {
		const school: School = {
			id: '1001',
			name: 'Example',
			province: 'Gauteng',
			circuit: 'East',
			totalWrote: 10,
			totalPassed: 9,
			passRate: 90,
			totalAchieved: null
		};
		expect(revealOverviewSchool(parseOverviewState('?schools=1'), school).schools).toBe(true);
	});

	it('omits empty parameters', () => {
		expect(
			serializeOverviewState({
				school: null,
				province: null,
				district: null,
				schools: false,
				expandedProvinces: [],
				expandedCircuits: []
			})
		).toBe('');
		expect(parseOverviewState('')).toEqual({
			school: null,
			province: null,
			district: null,
			schools: false,
			expandedProvinces: [],
			expandedCircuits: []
		});
	});

	it('still understands the legacy JSON open state', () => {
		const legacy = encodeURIComponent(
			JSON.stringify({ p: ['Gauteng'], c: [['Gauteng', 'Central']] })
		);
		expect(parseOverviewState(`?open=${legacy}`)).toEqual({
			school: null,
			province: null,
			district: null,
			schools: false,
			expandedProvinces: ['Gauteng'],
			expandedCircuits: [key('Gauteng', 'Central')]
		});
	});

	it('still understands the interim friendly open state', () => {
		expect(parseOverviewState('?open=Gauteng,Western%20Cape:Metro%20North')).toEqual({
			school: null,
			province: null,
			district: null,
			schools: false,
			expandedProvinces: ['Gauteng'],
			expandedCircuits: [key('Western Cape', 'Metro North')]
		});
	});

	it('ignores malformed open state instead of throwing', () => {
		const parsed = parseOverviewState('?school=7&open=!!!not-valid!!!');
		expect(parsed.school).toBe('7');
		expect(parsed.expandedCircuits).toEqual([]);
	});

	it('detects whether any overview state is present', () => {
		expect(
			hasOverviewState({
				school: null,
				province: null,
				district: null,
				schools: false,
				expandedProvinces: [],
				expandedCircuits: []
			})
		).toBe(false);
		expect(
			hasOverviewState({
				school: '1001',
				province: null,
				district: null,
				schools: false,
				expandedProvinces: [],
				expandedCircuits: []
			})
		).toBe(true);
		expect(
			hasOverviewState({
				school: null,
				province: null,
				district: null,
				schools: false,
				expandedProvinces: ['Gauteng'],
				expandedCircuits: []
			})
		).toBe(true);
		expect(
			hasOverviewState({
				school: null,
				province: null,
				district: null,
				schools: false,
				expandedProvinces: [],
				expandedCircuits: [key('Gauteng', 'Central')]
			})
		).toBe(true);
	});
});
