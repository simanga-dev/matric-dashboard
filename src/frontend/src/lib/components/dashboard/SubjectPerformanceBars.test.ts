// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mount, tick, unmount } from 'svelte';
import SubjectPerformanceBars from './SubjectPerformanceBars.svelte';
import type { SubjectBar } from './subject-bar';

vi.mock('$lib/components/common/InfoTooltip.svelte', () => ({ default: () => {} }));

let component: ReturnType<typeof mount> | undefined;

async function render(subjects: SubjectBar[]) {
	const target = document.createElement('div');
	document.body.append(target);
	component = mount(SubjectPerformanceBars, { target, props: { subjects } });
	await tick();
	return target;
}

afterEach(async () => {
	if (component) await unmount(component);
	component = undefined;
	document.body.replaceChildren();
});

describe('subject performance bars', () => {
	it('links each bar to its subject page when an href is given', async () => {
		const target = await render([
			{
				name: 'Mathematics',
				totalWrote: 85,
				percentAchieved: 60,
				href: '/subjects/mathematics?year=2024'
			}
		]);
		const link = target.querySelector<HTMLAnchorElement>('[data-subject-bar] a');
		expect(link?.getAttribute('href')).toBe('/subjects/mathematics?year=2024');
		expect(link?.getAttribute('aria-label')).toContain('Mathematics');
	});

	it('renders a button when no href is given', async () => {
		const target = await render([{ name: 'History', totalWrote: 71, percentAchieved: 90 }]);
		expect(target.querySelector('[data-subject-bar] a')).toBeNull();
		expect(target.querySelector('[data-subject-bar] button')).not.toBeNull();
	});

	it('keeps every bar at the sidebar menu item height so long names cannot stretch it', async () => {
		const target = await render([
			{
				name: 'English First Additional Language with a very long display name',
				totalWrote: 156,
				percentAchieved: 98
			}
		]);
		const bar = target.querySelector('[data-subject-bar] .rounded-md.bg-muted\\/25');
		expect(bar?.classList.contains('h-9')).toBe(true);
		expect(bar?.querySelector('.truncate')?.textContent?.trim()).toContain('English First');
	});
});
