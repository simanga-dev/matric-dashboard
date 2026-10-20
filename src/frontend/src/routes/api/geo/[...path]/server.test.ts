import { describe, expect, it, vi } from 'vitest';
import { GET, fallback } from './+server';
import type { RequestEvent } from './$types';

vi.mock('$lib/config/server', () => ({ SERVER_CONFIG: { API_URL: 'http://backend.test' } }));

function event(path: string, response = new Response('geojson'), rawPath = path) {
	const service = path === 'wms' ? 'WMS' : path.endsWith('wmts') ? 'WMTS' : 'WFS';
	const url = new URL(
		`http://frontend.test/api/geo/${rawPath}?service=${service}&request=GetCapabilities`
	);
	const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(response);
	const request = new Request(url, {
		headers: {
			accept: 'application/json',
			cookie: 'session=private',
			authorization: 'Bearer private',
			'if-none-match': '"cached"',
			'if-modified-since': 'Tue, 13 Oct 2026 00:00:00 GMT',
			'x-forwarded-for': 'spoofed'
		}
	});
	return {
		params: { path },
		url,
		request,
		fetch,
		getClientAddress: vi.fn(() => '203.0.113.1')
	} satisfies Pick<RequestEvent, 'params' | 'url' | 'request' | 'fetch' | 'getClientAddress'>;
}

function invoke(
	handler: (input: ReturnType<typeof event>) => Response | Promise<Response>,
	input: ReturnType<typeof event>
) {
	return handler(input);
}

describe('OGC proxy boundary', () => {
	it.each(['wfs', 'wms', 'ows', 'gwc/service/wmts'])(
		'forwards exact %s endpoint and query',
		async (path) => {
			const input = event(path);
			const response = await invoke(GET, input);
			expect(await response.text()).toBe('geojson');
			expect(input.fetch).toHaveBeenCalledWith(
				`http://backend.test/geoserver/${path}${input.url.search}`,
				{
					headers: expect.any(Headers),
					credentials: 'omit',
					redirect: 'manual',
					signal: input.request.signal
				}
			);
			const headers = new Headers(input.fetch.mock.calls[0]?.[1]?.headers);
			expect([...headers.keys()]).toEqual([
				'accept',
				'if-modified-since',
				'if-none-match',
				'x-forwarded-for'
			]);
		}
	);

	it('passes viewport filters and client cancellation through to the backend', async () => {
		const input = event('wfs');
		input.url.search =
			'?service=WFS&request=GetFeature&typeName=matric:school_footprints&propertyName=emis_number,geom&bbox=28,-26,29,-25,EPSG:4326';
		const controller = new AbortController();
		input.request = new Request(input.url, { signal: controller.signal });
		await invoke(GET, input);
		const [target, options] = input.fetch.mock.calls[0]!;
		expect(target).toBe(`http://backend.test/geoserver/wfs${input.url.search}`);
		expect(options?.signal).toBe(input.request.signal);
		controller.abort();
		expect(options?.signal?.aborted).toBe(true);
	});

	it('forwards distinct server-derived client identities for separate rate buckets', async () => {
		const first = event('wfs');
		const second = event('wfs');
		second.getClientAddress.mockReturnValue('2001:db8::2');
		await invoke(GET, first);
		await invoke(GET, second);
		expect(new Headers(first.fetch.mock.calls[0]?.[1]?.headers).get('x-forwarded-for')).toBe(
			'203.0.113.1'
		);
		expect(new Headers(second.fetch.mock.calls[0]?.[1]?.headers).get('x-forwarded-for')).toBe(
			'2001:db8::2'
		);
	});

	it('ignores spoofed forwarding headers and keeps credentials omitted', async () => {
		const input = event('wfs');
		input.request.headers.set('x-forwarded-for', '198.51.100.99, 127.0.0.1');
		input.request.headers.set('forwarded', 'for=198.51.100.99');
		await invoke(GET, input);
		const options = input.fetch.mock.calls[0]?.[1];
		const headers = new Headers(options?.headers);
		expect(headers.get('x-forwarded-for')).toBe('203.0.113.1');
		expect(headers.has('forwarded')).toBe(false);
		expect(headers.has('cookie')).toBe(false);
		expect(headers.has('authorization')).toBe(false);
		expect(options?.credentials).toBe('omit');
	});

	it.each([
		'../api/auth/login',
		'..%2fapi/auth/login',
		'%252e%252e%252fapi',
		'wfs/../rest',
		'wfs\\rest',
		'/wfs',
		'wfs/',
		'WFS',
		'rest',
		'web',
		'wmts',
		'wfs?anything',
		'wfs#anything',
		'wfs\0',
		'%',
		'gwc/service/wmts/extra'
	])('rejects invalid path %s without fetching', async (path) => {
		const input = event(path);
		const response = await invoke(GET, input);
		expect(response.status).toBe(400);
		expect(response.headers.get('content-type')).toBe('application/problem+json');
		expect(await response.json()).toMatchObject({
			type: 'about:blank',
			title: 'Bad Request',
			status: 400
		});
		expect(input.fetch).not.toHaveBeenCalled();
	});

	it.each(['gwc%2fservice%2fwmts', 'gwc%5cservice%5cwmts'])(
		'rejects decoded separators in %s',
		async (raw) => {
			const input = event('gwc/service/wmts', undefined, raw);
			expect((await invoke(GET, input)).status).toBe(400);
			expect(input.fetch).not.toHaveBeenCalled();
		}
	);

	it('rejects write operations, including duplicate mixed-case parameters', async () => {
		const input = event('wfs');
		input.url.searchParams.append('REQUEST', 'Transaction');
		expect((await invoke(GET, input)).status).toBe(400);
		expect(input.fetch).not.toHaveBeenCalled();
	});

	it.each([
		'request=Transaction',
		'request=GetFeature&REQUEST=GetFeature',
		'service=WMS&request=GetMap',
		'service=WFS&SERVICE=WFS&request=GetFeature',
		'request=',
		'service=WFS'
	])('rejects ambiguous or mismatched OGC queries: %s', async (query) => {
		const input = event('wfs');
		input.url.search = query;
		expect((await invoke(GET, input)).status).toBe(400);
		expect(input.fetch).not.toHaveBeenCalled();
	});

	it('allows inferred WFS service and mixed-case read operations', async () => {
		const input = event('wfs');
		input.url.search = 'REQUEST=gEtFeAtUrE&typeName=schools:school_locations';
		expect((await invoke(GET, input)).status).toBe(200);
		expect(input.fetch).toHaveBeenCalledOnce();
	});

	it.each(['__proto__', 'constructor', 'WPS', 'WMTS'])(
		'denies unknown OWS service %s',
		async (service) => {
			const input = event('ows');
			input.url.search = `service=${service}&request=GetCapabilities`;
			expect((await invoke(GET, input)).status).toBe(400);
			expect(input.fetch).not.toHaveBeenCalled();
		}
	);

	it('preserves only explicit cache/security response headers and streams the body', async () => {
		const upstream = new Response('features', {
			headers: {
				'content-type': 'application/json',
				'cache-control': 'public, max-age=60',
				etag: '"v1"',
				'last-modified': 'Tue, 13 Oct 2026 00:00:00 GMT',
				expires: 'Tue, 13 Oct 2026 00:01:00 GMT',
				'retry-after': '60',
				'x-content-type-options': 'nosniff',
				'content-security-policy': "default-src 'none'",
				'referrer-policy': 'no-referrer',
				'set-cookie': 'admin=private',
				authorization: 'Bearer private',
				server: 'internal',
				'x-internal-secret': 'private',
				'content-encoding': 'gzip',
				'content-length': '999'
			}
		});
		const input = event('wfs', upstream);
		const response = await invoke(GET, input);
		expect(await response.text()).toBe('features');
		for (const name of [
			'set-cookie',
			'authorization',
			'server',
			'x-internal-secret',
			'content-encoding',
			'content-length'
		]) {
			expect(response.headers.has(name)).toBe(false);
		}
		for (const name of [
			'content-type',
			'cache-control',
			'etag',
			'last-modified',
			'expires',
			'retry-after',
			'x-content-type-options',
			'content-security-policy',
			'referrer-policy'
		]) {
			expect(response.headers.get(name)).toBe(upstream.headers.get(name));
		}
	});

	it('preserves 304 and backend ProblemDetails', async () => {
		const cached = await invoke(
			GET,
			event('wfs', new Response(null, { status: 304, headers: { etag: '"v1"' } }))
		);
		expect(cached.status).toBe(304);
		expect(cached.headers.get('etag')).toBe('"v1"');
		const body = { type: 'about:blank', title: 'Unavailable', status: 503 };
		const failed = await invoke(
			GET,
			event(
				'wfs',
				Response.json(body, {
					status: 503,
					headers: { 'content-type': 'application/problem+json' }
				})
			)
		);
		expect(failed.status).toBe(503);
		expect(await failed.json()).toEqual(body);
	});

	it('does not follow redirects or leak their location', async () => {
		const input = event(
			'wfs',
			new Response(null, { status: 302, headers: { location: 'http://internal/admin' } })
		);
		const response = await invoke(GET, input);
		expect(response.status).toBe(502);
		expect(response.headers.has('location')).toBe(false);
	});

	it('returns ProblemDetails on transport errors', async () => {
		const input = event('wfs');
		input.fetch.mockRejectedValue(new Error('private connection details'));
		const response = await invoke(GET, input);
		expect(response.status).toBe(502);
		expect(await response.json()).toMatchObject({
			status: 502,
			detail: 'GeoServer is unavailable.'
		});
	});

	it('converts non-ProblemDetails upstream failures to JSON without leaking their body', async () => {
		const response = await invoke(
			GET,
			event('wfs', new Response('<html>private error</html>', { status: 500 }))
		);
		expect(response.status).toBe(500);
		expect(response.headers.get('content-type')).toBe('application/problem+json');
		expect(await response.json()).toMatchObject({
			status: 500,
			detail: 'GeoServer could not complete the request.'
		});
	});

	it('rejects unsupported methods without fetching', async () => {
		const input = event('wfs');
		const response = await invoke(fallback, input);
		expect(response.status).toBe(405);
		expect(response.headers.get('allow')).toBe('GET, HEAD');
		expect(input.fetch).not.toHaveBeenCalled();
	});
});
