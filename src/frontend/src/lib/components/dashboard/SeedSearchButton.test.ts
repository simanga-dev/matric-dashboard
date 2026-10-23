// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SeedSearchButton from './SeedSearchButton.svelte';

const auth = vi.hoisted(() => ({ user: null as { id: string } | null }));
const seed = vi.hoisted(() => vi.fn());
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));

vi.mock('$app/state', () => ({
	page: {
		get data() {
			return { user: auth.user };
		}
	}
}));
vi.mock('$lib/api/search-index', () => ({ seedSchoolSearchIndex: seed }));
vi.mock('$lib/components/ui/sonner', () => ({ toast }));

let component: ReturnType<typeof mount> | undefined;

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.innerHTML = '';
});

beforeEach(() => {
	auth.user = null;
	seed.mockReset();
	toast.success.mockReset();
	toast.error.mockReset();
});

describe('SeedSearchButton', () => {
	it('stays hidden for signed-out visitors', () => {
		component = mount(SeedSearchButton, { target: document.body });

		expect(document.querySelector('button')).toBeNull();
	});

	it('seeds the index for a signed-in user', async () => {
		auth.user = { id: 'user-1' };
		seed.mockResolvedValue({
			data: { indexed: 8237, removed: 4, durationMs: 900 },
			error: undefined,
			response: new Response(null, { status: 200 })
		});
		component = mount(SeedSearchButton, { target: document.body });

		const button = document.querySelector('button');
		expect(button).not.toBeNull();
		expect(button?.getAttribute('aria-label')).toBe('Seed search index');

		button?.click();
		await tick();
		await Promise.resolve();

		expect(seed).toHaveBeenCalledOnce();
		expect(toast.success).toHaveBeenCalledWith('Search index seeded with 8,237 schools.');
	});

	it('reports a failed seed without throwing', async () => {
		auth.user = { id: 'user-1' };
		seed.mockRejectedValue(new Error('offline'));
		component = mount(SeedSearchButton, { target: document.body });

		document.querySelector('button')?.click();
		await tick();
		await Promise.resolve();

		expect(toast.error).toHaveBeenCalledWith('Could not seed the search index.');
	});
});
