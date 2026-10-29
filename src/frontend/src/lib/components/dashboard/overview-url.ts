import { circuitKey, circuitParts } from './school-hierarchy';
import type { School } from '$lib/types/dashboard';

/**
 * URL (de)serialization for the overview workspace.
 *
 * The overview is a shareable view: the selected school, the selected province
 * and the expanded tree branches all live in the query string so a copied link
 * reproduces the same state for another viewer.
 *
 * Parameters:
 * - `school`   selected school id (EMIS number)
 * - `province` selected province name
 * - `district` selected district (circuit) name, scoped by province
 * - `schools`  `1` while the school directory dialog is open, so closing or
 *              sharing the dialog survives a reload
 * - `open`     base64url of `{ p: string[], c: [province, circuit][] }`, the
 *              expanded provinces and circuits. Base64url keeps the value
 *              compact and free of query-breaking characters.
 *
 * Older links may still use the previous JSON (`{...}`) or the interim
 * `province,province:circuit` format; both are still understood.
 */
export interface OverviewUrlState {
	school: string | null;
	province: string | null;
	district: string | null;
	schools: boolean;
	expandedProvinces: string[];
	expandedCircuits: string[];
}

/** History snapshots win, including empty ones. Plain links retain the current workspace. */
export function overviewHistoryState(
	search: string,
	snapshot?: OverviewUrlState,
	fallback?: OverviewUrlState
): OverviewUrlState {
	if (snapshot) return snapshot;
	const parsed = parseOverviewState(search);
	return hasOverviewState(parsed) ? parsed : (fallback ?? parsed);
}

/** Header search reveals even the current school without forcing its branch to stay open. */
export function revealOverviewSchool(state: OverviewUrlState, school: School): OverviewUrlState {
	return {
		school: school.id,
		province: null,
		district: null,
		schools: state.schools,
		expandedProvinces: [...new Set([...state.expandedProvinces, school.province])],
		expandedCircuits: [
			...new Set([...state.expandedCircuits, circuitKey(school.province, school.circuit)])
		]
	};
}

interface OpenPayload {
	p: string[];
	c: [string, string][];
}

function encodeBase64Url(text: string): string {
	const bytes = new TextEncoder().encode(text);
	let binary = '';
	for (const byte of bytes) binary += String.fromCharCode(byte);
	return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function decodeBase64Url(token: string): string | null {
	try {
		const base64 = token.replace(/-/g, '+').replace(/_/g, '/');
		const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
		const binary = atob(padded);
		const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
		return new TextDecoder().decode(bytes);
	} catch {
		return null;
	}
}

function decode(value: string): string {
	try {
		return decodeURIComponent(value);
	} catch {
		return value;
	}
}

/** Read a raw (still-encoded) query parameter without decoding it as a whole. */
function rawParam(search: string, key: string): string | null {
	const query = search.startsWith('?') ? search.slice(1) : search;
	for (const pair of query.split('&')) {
		if (!pair) continue;
		const equals = pair.indexOf('=');
		const name = equals === -1 ? pair : pair.slice(0, equals);
		if (name === key) return equals === -1 ? '' : pair.slice(equals + 1);
	}
	return null;
}

function stringArray(value: unknown): string[] {
	return Array.isArray(value)
		? value.filter((item): item is string => typeof item === 'string')
		: [];
}

function pairArray(value: unknown): [string, string][] {
	if (!Array.isArray(value)) return [];
	const pairs: [string, string][] = [];
	for (const item of value) {
		if (Array.isArray(item) && typeof item[0] === 'string' && typeof item[1] === 'string') {
			pairs.push([item[0], item[1]]);
		}
	}
	return pairs;
}

function asPayload(value: unknown): OpenPayload | null {
	if (!value || typeof value !== 'object') return null;
	const record = value as { p?: unknown; c?: unknown };
	if (!('p' in record) && !('c' in record)) return null;
	return { p: stringArray(record.p), c: pairArray(record.c) };
}

/** Legacy interim format: `open=Gauteng,Western%20Cape:Metro%20North`. */
function parseFriendlyOpen(raw: string): OpenPayload {
	const p: string[] = [];
	const c: [string, string][] = [];
	for (const entry of raw.split(',')) {
		if (!entry) continue;
		const separator = entry.indexOf(':');
		if (separator === -1) p.push(decode(entry));
		else c.push([decode(entry.slice(0, separator)), decode(entry.slice(separator + 1))]);
	}
	return { p, c };
}

function parseOpen(raw: string): OpenPayload {
	// Legacy JSON format (the value may be percent-encoded as a whole).
	const decodedRaw = decode(raw);
	if (decodedRaw.trimStart().startsWith('{')) {
		try {
			const payload = asPayload(JSON.parse(decodedRaw));
			if (payload) return payload;
		} catch {
			// fall through
		}
	}
	// Current base64url format.
	const decoded = decodeBase64Url(raw);
	if (decoded !== null) {
		try {
			const payload = asPayload(JSON.parse(decoded));
			if (payload) return payload;
		} catch {
			// fall through
		}
	}
	// Interim friendly format.
	return parseFriendlyOpen(raw);
}

/** Parse overview state from a query string (with or without a leading `?`). */
export function parseOverviewState(search: string): OverviewUrlState {
	const open = rawParam(search, 'open');
	const payload = open ? parseOpen(open) : { p: [], c: [] };

	const school = rawParam(search, 'school');
	const province = rawParam(search, 'province');
	const district = rawParam(search, 'district');

	return {
		school: school === null ? null : decode(school),
		province: province === null ? null : decode(province),
		district: district === null ? null : decode(district),
		schools: rawParam(search, 'schools') === '1',
		expandedProvinces: payload.p,
		expandedCircuits: payload.c.map(([prov, circuit]) => circuitKey(prov, circuit))
	};
}

/** Whether the state carries anything worth restoring or sharing. */
export function hasOverviewState(state: OverviewUrlState): boolean {
	return Boolean(
		state.school ||
		state.province ||
		state.district ||
		state.schools ||
		state.expandedProvinces.length ||
		state.expandedCircuits.length
	);
}

/** Serialize overview state to a query string, omitting empty values. */
export function serializeOverviewState(state: OverviewUrlState): string {
	const parts: string[] = [];
	if (state.school) parts.push(`school=${encodeURIComponent(state.school)}`);
	if (state.province) parts.push(`province=${encodeURIComponent(state.province)}`);
	if (state.district) parts.push(`district=${encodeURIComponent(state.district)}`);

	if (state.expandedProvinces.length || state.expandedCircuits.length) {
		const circuits: [string, string][] = [];
		for (const key of state.expandedCircuits) {
			const pair = circuitParts(key);
			if (pair) circuits.push(pair);
		}
		const payload: OpenPayload = { p: state.expandedProvinces, c: circuits };
		parts.push(`open=${encodeBase64Url(JSON.stringify(payload))}`);
	}

	if (state.schools) parts.push('schools=1');

	return parts.join('&');
}
