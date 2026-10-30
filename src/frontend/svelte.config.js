import adapter from '@sveltejs/adapter-node';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
const config = {
	preprocess: vitePreprocess(),

	kit: {
		adapter: adapter(),

		csp: {
			mode: 'nonce',
			directives: {
				'default-src': ['self'],
				'script-src': ['self', 'nonce', 'https://challenges.cloudflare.com'],
				'style-src': ['self', 'unsafe-inline'],
				'img-src': ['self', 'https:', 'data:', 'blob:'],
				'font-src': ['self'],
				'connect-src': ['self'],
				'worker-src': ['self', 'blob:'],
				// blob: lets the past paper preview show a PDF fetched through the API.
				'frame-src': ['https://challenges.cloudflare.com', 'blob:'],
				'frame-ancestors': ['none'],
				'base-uri': ['self'],
				'form-action': ['self'],
				'object-src': ['none']
			}
		}
	}
};

export default config;
