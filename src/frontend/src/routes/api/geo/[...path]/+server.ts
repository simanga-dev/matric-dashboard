import type { RequestEvent, RequestHandler } from './$types';
import { SERVER_CONFIG } from '$lib/config/server';

const OGC_PATHS = new Set(['wfs', 'wms', 'ows', 'gwc/service/wmts']);
const READ_OPERATIONS = new Map<string, readonly string[]>([
	[
		'wfs',
		[
			'getcapabilities',
			'describefeaturetype',
			'getfeature',
			'getpropertyvalue',
			'liststoredqueries',
			'describestoredqueries'
		]
	],
	[
		'wms',
		[
			'getcapabilities',
			'getmap',
			'getfeatureinfo',
			'getlegendgraphic',
			'describelayer',
			'getstyles'
		]
	],
	['wmts', ['getcapabilities', 'gettile', 'getfeatureinfo']]
]);
const REQUEST_HEADERS = ['accept', 'if-none-match', 'if-modified-since'];
const RESPONSE_HEADERS = [
	'content-type',
	'cache-control',
	'etag',
	'last-modified',
	'expires',
	'retry-after',
	'x-content-type-options',
	'x-frame-options',
	'permissions-policy',
	'strict-transport-security',
	'content-security-policy',
	'content-security-policy-report-only',
	'referrer-policy'
];

function problem(status: number, title: string, detail: string, instance: string): Response {
	return Response.json(
		{ type: 'about:blank', title, status, detail, instance },
		{ status, headers: { 'content-type': 'application/problem+json', 'cache-control': 'no-store' } }
	);
}

/** Only exact read-only OGC endpoints may reach the backend. No credentials cross this proxy. */
export const GET = (async ({
	params,
	url,
	fetch,
	request,
	getClientAddress
}: Pick<RequestEvent, 'params' | 'url' | 'fetch' | 'request' | 'getClientAddress'>) => {
	// Params are decoded by SvelteKit. Reject residual encoding as well as any
	// decoded separator or traversal variation by matching the entire path.
	if (!OGC_PATHS.has(params.path) || !url.pathname.endsWith(`/api/geo/${params.path}`)) {
		return problem(400, 'Bad Request', 'Invalid OGC endpoint.', url.pathname);
	}
	const values = (key: string) =>
		[...url.searchParams]
			.filter(([name]) => name.toLowerCase() === key)
			.map(([, value]) => value.toLowerCase());
	const services = values('service');
	const operations = values('request');
	const endpoint = params.path === 'gwc/service/wmts' ? 'wmts' : params.path;
	const service = services[0] || (endpoint !== 'ows' ? endpoint : '');
	if (
		services.length > 1 ||
		operations.length !== 1 ||
		(endpoint !== 'ows' && endpoint !== service) ||
		(endpoint === 'ows' && service === 'wmts') ||
		!READ_OPERATIONS.get(service)?.includes(operations[0] ?? '')
	) {
		return problem(
			400,
			'Bad Request',
			'A single read-only OGC service and request are required.',
			url.pathname
		);
	}
	const target = `${SERVER_CONFIG.API_URL}/geoserver/${params.path}${url.search}`;
	const headers = new Headers();
	for (const name of REQUEST_HEADERS) {
		const value = request.headers.get(name);
		if (value) headers.set(name, value);
	}
	// SvelteKit resolves this from the socket or its trusted proxy configuration.
	// Never copy the caller's X-Forwarded-For header into the rate-limit identity.
	headers.set('x-forwarded-for', getClientAddress());
	try {
		const response = await fetch(target, { headers, credentials: 'omit', redirect: 'manual' });
		if (response.status >= 300 && response.status < 400 && response.status !== 304) {
			return problem(502, 'Bad Gateway', 'Unexpected GeoServer redirect.', url.pathname);
		}
		if (
			response.status >= 400 &&
			!response.headers.get('content-type')?.startsWith('application/problem+json')
		) {
			return problem(
				response.status,
				'GeoServer proxy error',
				'GeoServer could not complete the request.',
				url.pathname
			);
		}
		const responseHeaders = new Headers();
		for (const name of RESPONSE_HEADERS) {
			const value = response.headers.get(name);
			if (value) responseHeaders.set(name, value);
		}
		return new Response(response.body, { status: response.status, headers: responseHeaders });
	} catch {
		return problem(502, 'Bad Gateway', 'GeoServer is unavailable.', url.pathname);
	}
}) satisfies RequestHandler;

/** State-changing methods are never forwarded. */
export const fallback = (({ url }: Pick<RequestEvent, 'url'>) => {
	const response = problem(
		405,
		'Method Not Allowed',
		'Only GET and HEAD are allowed.',
		url.pathname
	);
	response.headers.set('allow', 'GET, HEAD');
	return response;
}) satisfies RequestHandler;
