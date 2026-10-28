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

	it('ignores the key for custom editors and fields that re-dispatch key events', () => {
		const handler = vi.fn();
		const unregister = registerShortcutHandler(ShortcutAction.OpenMapEditor, handler);
		const shortcuts = globalShortcuts(window);

		const editor = document.createElement('div');
		editor.setAttribute('role', 'textbox');
		editor.tabIndex = 0;
		document.body.append(editor);
		editor.focus();
		editor.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyE', bubbles: true }));
		expect(handler).not.toHaveBeenCalled();

		const input = document.createElement('input');
		document.body.append(input);
		input.focus();
		// Even when a control re-dispatches the event from elsewhere, the focused
		// text field still owns the key.
		const redispatched = new KeyboardEvent('keydown', { code: 'KeyE', cancelable: true });
		window.dispatchEvent(redispatched);
		expect(handler).not.toHaveBeenCalled();
		expect(redispatched.defaultPrevented).toBe(false);

		unregister();
		shortcuts.destroy();
	});

	it('still runs page shortcuts while a non-editable control has focus', () => {
		const handler = vi.fn();
		const unregister = registerShortcutHandler(ShortcutAction.OpenMapEditor, handler);
		const shortcuts = globalShortcuts(window);
		const treeItem = document.createElement('div');
		treeItem.setAttribute('role', 'treeitem');
		treeItem.tabIndex = 0;
		document.body.append(treeItem);
		treeItem.focus();

		treeItem.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyE', bubbles: true }));
		expect(handler).toHaveBeenCalledTimes(1);

		unregister();
		shortcuts.destroy();
	});

	it('does not consume the key when no handler is mounted', () => {
		const shortcuts = globalShortcuts(window);
		const event = new KeyboardEvent('keydown', { code: 'KeyE', cancelable: true });

		window.dispatchEvent(event);

		expect(event.defaultPrevented).toBe(false);
		shortcuts.destroy();
	});
});
