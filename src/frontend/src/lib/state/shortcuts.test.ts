// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
	ShortcutAction,
	globalShortcuts,
	isOverlayOpen,
	isShortcutTargetBlocked,
	registerShortcutHandler,
	shortcutsState
} from './shortcuts.svelte';

beforeEach(() => {
	shortcutsState.isHelpOpen = false;
	shortcutsState.isCommandPaletteOpen = false;
	document.body.replaceChildren();
});

describe('shortcut target guards', () => {
	it('blocks controls that own Enter, Space, or letter keys', () => {
		for (const tag of ['input', 'textarea', 'select', 'button', 'summary', 'a']) {
			const element = document.createElement(tag);
			if (tag === 'a') element.setAttribute('href', '/schools');
			expect(isShortcutTargetBlocked(element)).toBe(true);
		}
		const treeItem = document.createElement('div');
		treeItem.setAttribute('role', 'treeitem');
		expect(isShortcutTargetBlocked(treeItem)).toBe(true);
		const nested = document.createElement('span');
		const button = document.createElement('button');
		button.append(nested);
		expect(isShortcutTargetBlocked(nested)).toBe(true);
		expect(isShortcutTargetBlocked(document.body)).toBe(false);
		expect(isShortcutTargetBlocked(null)).toBe(false);
	});

	it('reports an open dialog even when focus sits on the body', () => {
		expect(isOverlayOpen()).toBe(false);
		const dialog = document.createElement('div');
		dialog.setAttribute('role', 'dialog');
		dialog.setAttribute('data-state', 'open');
		document.body.append(dialog);
		expect(isOverlayOpen()).toBe(true);
		dialog.setAttribute('data-state', 'closed');
		expect(isOverlayOpen()).toBe(false);
		shortcutsState.isHelpOpen = true;
		expect(isOverlayOpen()).toBe(true);
	});
});

describe('component shortcut handlers', () => {
	it('runs a registered handler on the key and stops after unregistering', () => {
		const handler = vi.fn();
		const unregister = registerShortcutHandler(ShortcutAction.OpenMapEditor, handler);
		const shortcuts = globalShortcuts(window);

		window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyE' }));
		expect(handler).toHaveBeenCalledTimes(1);

		unregister();
		window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyE' }));
		expect(handler).toHaveBeenCalledTimes(1);
		shortcuts.destroy();
	});

	it('ignores the key while an input owns focus', () => {
		const handler = vi.fn();
		const unregister = registerShortcutHandler(ShortcutAction.OpenMapEditor, handler);
		const shortcuts = globalShortcuts(window);
		const input = document.createElement('input');
		document.body.append(input);

		input.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyE', bubbles: true }));
		expect(handler).not.toHaveBeenCalled();

		unregister();
		shortcuts.destroy();
	});
});
