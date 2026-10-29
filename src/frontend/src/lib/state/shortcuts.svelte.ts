import { SvelteMap } from 'svelte/reactivity';
import { IS_MAC } from '$lib/utils/platform';
import * as m from '$lib/paraglide/messages';

// --- State ---

class ShortcutsState {
	isHelpOpen = $state(false);
	isCommandPaletteOpen = $state(false);
}

export const shortcutsState = new ShortcutsState();

// --- Configuration ---

export const ShortcutAction = {
	CommandPalette: 'commandPalette',
	Settings: 'settings',
	Logout: 'logout',
	Help: 'help',
	ToggleSidebar: 'toggleSidebar',
	OpenMapEditor: 'openMapEditor'
} as const;

export type ShortcutActionType = (typeof ShortcutAction)[keyof typeof ShortcutAction];

export interface ShortcutConfig {
	/** KeyboardEvent.code to match (e.g. "KeyK", "Comma") */
	code: string;
	/** Require platform modifier (Cmd on Mac, Ctrl on others) */
	mod?: boolean;
	/** Require Shift modifier */
	shift?: boolean;
	action: ShortcutActionType;
	description: () => string;
	/** Platform-aware display label */
	display: () => string;
	/** Fire even when an input/textarea is focused */
	allowInInput?: boolean;
}

const SHORTCUTS: ShortcutConfig[] = [
	{
		code: 'KeyK',
		mod: true,
		action: ShortcutAction.CommandPalette,
		description: m.shortcuts_commandPalette,
		display: () => (IS_MAC ? '⌘ K' : 'Ctrl+K'),
		allowInInput: true
	},
	{
		code: 'Comma',
		mod: true,
		action: ShortcutAction.Settings,
		description: m.shortcuts_settings,
		display: () => (IS_MAC ? '⌘ ,' : 'Ctrl+,')
	},
	{
		code: 'KeyL',
		mod: true,
		shift: true,
		action: ShortcutAction.Logout,
		description: m.shortcuts_logout,
		display: () => (IS_MAC ? '⌘ ⇧ L' : 'Ctrl+Shift+L')
	},
	{
		code: 'BracketLeft',
		mod: true,
		action: ShortcutAction.ToggleSidebar,
		description: m.shortcuts_toggleSidebar,
		display: () => (IS_MAC ? '⌘ [' : 'Ctrl+[')
	},
	{
		code: 'Slash',
		shift: true,
		action: ShortcutAction.Help,
		description: m.shortcuts_help,
		display: () => (IS_MAC ? '⇧ ?' : 'Shift+?')
	},
	{
		code: 'KeyE',
		action: ShortcutAction.OpenMapEditor,
		description: m.shortcuts_editMap,
		display: () => 'E'
	}
];

export function getAllShortcuts(): ShortcutConfig[] {
	return SHORTCUTS;
}

export function getShortcutSymbol(action: ShortcutActionType): string {
	const config = SHORTCUTS.find((s) => s.action === action);
	return config?.display() ?? '';
}

// --- Action ---

export type ShortcutHandlers = Partial<Record<ShortcutActionType, () => void>>;

const registeredHandlers = new SvelteMap<ShortcutActionType, () => void>();

/**
 * Registers a shortcut handler owned by a mounted component instead of the root
 * layout. The shortcut stays listed in the help dialog. Returns an unregister
 * function.
 */
export function registerShortcutHandler(
	action: ShortcutActionType,
	handler: () => void
): () => void {
	registeredHandlers.set(action, handler);
	return () => {
		if (registeredHandlers.get(action) === handler) registeredHandlers.delete(action);
	};
}

/**
 * Elements that own Enter, Space, or letter keys themselves, so page shortcuts
 * must not act while focus is inside them.
 */
const INTERACTIVE_SELECTOR = [
	'input',
	'textarea',
	'select',
	'button',
	'a[href]',
	'summary',
	'[contenteditable]:not([contenteditable="false"])',
	'[role="dialog"]',
	'[role="alertdialog"]',
	'[role="menu"]',
	'[role="menubar"]',
	'[role="listbox"]',
	'[role="tree"]',
	'[role="treeitem"]',
	'[role="option"]',
	'[role="tablist"]',
	'[role="tab"]',
	'[role="slider"]',
	'[role="switch"]',
	'[role="checkbox"]',
	'[role="radio"]',
	'[role="textbox"]',
	'[role="combobox"]',
	'[role="grid"]'
].join(', ');

/** True when a key press belongs to a focused control rather than a page shortcut. */
export function isShortcutTargetBlocked(target: EventTarget | null): boolean {
	return target instanceof Element && target.closest(INTERACTIVE_SELECTOR) !== null;
}

/**
 * True when a modal surface owns the keyboard. Checks the document rather than
 * only the focus target so a dialog with focus on the body still wins.
 */
export function isOverlayOpen(): boolean {
	if (shortcutsState.isHelpOpen || shortcutsState.isCommandPaletteOpen) return true;
	if (typeof document === 'undefined') return false;
	return (
		document.querySelector(
			'[role="dialog"][data-state="open"], [role="alertdialog"][data-state="open"]'
		) !== null
	);
}

/**
 * Editable controls that own printable keys. This is narrower than
 * `isShortcutTargetBlocked`: page shortcuts may still run while a tree row or
 * toolbar button has focus, because those do not accept text entry.
 */
const TEXT_ENTRY_SELECTOR = [
	'input',
	'textarea',
	'select',
	'[contenteditable]:not([contenteditable="false"])',
	'[role="textbox"]',
	'[role="searchbox"]',
	'[role="combobox"]'
].join(', ');

function isTextEntry(target: EventTarget | null): boolean {
	return target instanceof Element && target.closest(TEXT_ENTRY_SELECTOR) !== null;
}

/**
 * The deepest element in the event path. Using the composed path unwraps
 * shadow-DOM retargeting, where `event.target` is the host rather than the
 * control that actually received the key.
 */
function eventTargetElement(event: KeyboardEvent): EventTarget | null {
	const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
	return path.find((node) => node instanceof Element) ?? event.target;
}

/**
 * True when the key press belongs to an editable control. The event target
 * covers the common case; the active element also covers controls that
 * re-dispatch keys from the body and inputs whose events are retargeted.
 */
function ownsKeyboardInput(event: KeyboardEvent): boolean {
	return isTextEntry(eventTargetElement(event)) || isTextEntry(document.activeElement);
}

/** True when the action has something to run, so keys are not swallowed in vain. */
function hasHandler(action: ShortcutActionType, handlers: ShortcutHandlers): boolean {
	if (action === ShortcutAction.Help || action === ShortcutAction.CommandPalette) return true;
	return Boolean(handlers[action] ?? registeredHandlers.get(action));
}

function matchShortcut(event: KeyboardEvent): ShortcutConfig | undefined {
	const mod = IS_MAC ? event.metaKey : event.ctrlKey;

	for (const sc of SHORTCUTS) {
		if (event.code !== sc.code) continue;
		if (sc.mod && !mod) continue;
		if (!sc.mod && mod) continue;
		if (sc.shift && !event.shiftKey) continue;
		if (!sc.shift && event.shiftKey) continue;
		// Block if Alt is pressed (avoid interfering with AltGr combos)
		if (event.altKey) continue;
		return sc;
	}
	return undefined;
}

export function globalShortcuts(node: Window, handlers: ShortcutHandlers = {}) {
	let currentHandlers = handlers;

	function onKeydown(event: KeyboardEvent) {
		// IME composition owns its keys; treating them as shortcuts would drop
		// characters mid-word for input methods that emit keydown events.
		if (event.defaultPrevented || event.isComposing || event.keyCode === 229) return;
		const sc = matchShortcut(event);
		if (!sc) return;
		if (!sc.allowInInput && ownsKeyboardInput(event)) return;
		// Single-key shortcuts must not block typing where no handler is mounted
		// (for example E on a page without the school map editor).
		if (!hasHandler(sc.action, currentHandlers)) return;
		event.preventDefault();
		executeAction(sc.action, currentHandlers);
	}

	node.addEventListener('keydown', onKeydown, { capture: true });

	return {
		update(newHandlers: ShortcutHandlers) {
			currentHandlers = newHandlers;
		},
		destroy() {
			node.removeEventListener('keydown', onKeydown, { capture: true });
		}
	};
}

function executeAction(action: ShortcutActionType, handlers: ShortcutHandlers) {
	if (action === ShortcutAction.Help) {
		shortcutsState.isHelpOpen = !shortcutsState.isHelpOpen;
		return;
	}
	if (action === ShortcutAction.CommandPalette) {
		shortcutsState.isCommandPaletteOpen = !shortcutsState.isCommandPaletteOpen;
		return;
	}
	const handler = handlers[action] ?? registeredHandlers.get(action);
	handler?.();
}
