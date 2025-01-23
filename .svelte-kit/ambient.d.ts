
// this file is generated — do not edit it


/// <reference types="@sveltejs/kit" />

/**
 * Environment variables [loaded by Vite](https://vitejs.dev/guide/env-and-mode.html#env-files) from `.env` files and `process.env`. Like [`$env/dynamic/private`](https://kit.svelte.dev/docs/modules#$env-dynamic-private), this module cannot be imported into client-side code. This module only includes variables that _do not_ begin with [`config.kit.env.publicPrefix`](https://kit.svelte.dev/docs/configuration#env) _and do_ start with [`config.kit.env.privatePrefix`](https://kit.svelte.dev/docs/configuration#env) (if configured).
 * 
 * _Unlike_ [`$env/dynamic/private`](https://kit.svelte.dev/docs/modules#$env-dynamic-private), the values exported from this module are statically injected into your bundle at build time, enabling optimisations like dead code elimination.
 * 
 * ```ts
 * import { API_KEY } from '$env/static/private';
 * ```
 * 
 * Note that all environment variables referenced in your code should be declared (for example in an `.env` file), even if they don't have a value until the app is deployed:
 * 
 * ```
 * MY_FEATURE_FLAG=""
 * ```
 * 
 * You can override `.env` values from the command line like so:
 * 
 * ```bash
 * MY_FEATURE_FLAG="enabled" npm run dev
 * ```
 */
declare module '$env/static/private' {
	export const SHELL: string;
	export const COLORTERM: string;
	export const NVM_INC: string;
	export const WSL2_GUI_APPS_ENABLED: string;
	export const TERM_PROGRAM_VERSION: string;
	export const npm_package_devDependencies_eslint_plugin_svelte: string;
	export const NVIM: string;
	export const WSL_DISTRO_NAME: string;
	export const ZSH_CACHE_DIR: string;
	export const TMUX: string;
	export const npm_config_resolution_mode: string;
	export const _P9K_TTY: string;
	export const NODE: string;
	export const npm_package_devDependencies_tslib: string;
	export const npm_config_ignore_scripts: string;
	export const npm_package_devDependencies__types_cookie: string;
	export const npm_package_scripts_check_watch: string;
	export const P9K_TTY: string;
	export const OPENAI_API_KEY: string;
	export const npm_config_argv: string;
	export const NVIM_LOG_FILE: string;
	export const npm_config_bin_links: string;
	export const NNN_FIFO: string;
	export const EDITOR: string;
	export const npm_package_scripts_test_unit: string;
	export const MASON: string;
	export const PMSPEC: string;
	export const NAME: string;
	export const PWD: string;
	export const npm_config_save_prefix: string;
	export const npm_package_devDependencies_vite: string;
	export const LOGNAME: string;
	export const npm_package_readmeFilename: string;
	export const npm_package_devDependencies__typescript_eslint_parser: string;
	export const PNPM_HOME: string;
	export const npm_package_scripts_build: string;
	export const _: string;
	export const npm_package_devDependencies_prettier: string;
	export const npm_package_devDependencies_eslint_config_prettier: string;
	export const HOME: string;
	export const npm_config_version_git_tag: string;
	export const LANG: string;
	export const WSL_INTEROP: string;
	export const npm_package_devDependencies_typescript: string;
	export const npm_config_init_license: string;
	export const npm_package_version: string;
	export const KEYTIMEOUT: string;
	export const npm_package_devDependencies__typescript_eslint_eslint_plugin: string;
	export const WAYLAND_DISPLAY: string;
	export const NNN_PLUG: string;
	export const npm_config_version_commit_hooks: string;
	export const npm_package_scripts_test_integration: string;
	export const npm_package_devDependencies_prettier_plugin_svelte: string;
	export const INIT_CWD: string;
	export const npm_package_scripts_format: string;
	export const npm_package_scripts_preview: string;
	export const npm_lifecycle_script: string;
	export const npm_package_description: string;
	export const NVM_DIR: string;
	export const npm_config_version_tag_prefix: string;
	export const YARN_WRAP_OUTPUT: string;
	export const ZPFX: string;
	export const npm_package_devDependencies_svelte_check: string;
	export const TERM: string;
	export const npm_package_name: string;
	export const npm_package_type: string;
	export const USER: string;
	export const npm_package_devDependencies_vitest: string;
	export const TMUX_PANE: string;
	export const DISPLAY: string;
	export const npm_lifecycle_event: string;
	export const SHLVL: string;
	export const npm_config_version_git_sign: string;
	export const NVM_CD_FLAGS: string;
	export const npm_config_version_git_message: string;
	export const npm_package_devDependencies_eslint: string;
	export const _P9K_SSH_TTY: string;
	export const npm_config_user_agent: string;
	export const npm_package_scripts_lint: string;
	export const npm_package_devDependencies__fontsource_fira_mono: string;
	export const PRETTIERD_DEFAULT_CONFIG: string;
	export const npm_execpath: string;
	export const npm_package_devDependencies__sveltejs_adapter_auto: string;
	export const npm_package_devDependencies_svelte: string;
	export const npm_package_scripts_test: string;
	export const XDG_RUNTIME_DIR: string;
	export const MYVIMRC: string;
	export const npm_config_strict_ssl: string;
	export const DEBUGINFOD_URLS: string;
	export const WSLENV: string;
	export const P9K_SSH: string;
	export const npm_package_scripts_dev: string;
	export const npm_package_scripts_check: string;
	export const PATH: string;
	export const npm_package_devDependencies__neoconfetti_svelte: string;
	export const npm_package_devDependencies__sveltejs_kit: string;
	export const NNN_SSHFS: string;
	export const DBUS_SESSION_BUS_ADDRESS: string;
	export const npm_package_devDependencies__playwright_test: string;
	export const NVM_BIN: string;
	export const npm_config_registry: string;
	export const HOSTTYPE: string;
	export const npm_config_ignore_optional: string;
	export const PULSE_SERVER: string;
	export const npm_node_execpath: string;
	export const npm_config_engine_strict: string;
	export const OLDPWD: string;
	export const TERM_PROGRAM: string;
	export const npm_config_init_version: string;
	export const NODE_ENV: string;
}

/**
 * Similar to [`$env/static/private`](https://kit.svelte.dev/docs/modules#$env-static-private), except that it only includes environment variables that begin with [`config.kit.env.publicPrefix`](https://kit.svelte.dev/docs/configuration#env) (which defaults to `PUBLIC_`), and can therefore safely be exposed to client-side code.
 * 
 * Values are replaced statically at build time.
 * 
 * ```ts
 * import { PUBLIC_BASE_URL } from '$env/static/public';
 * ```
 */
declare module '$env/static/public' {
	
}

/**
 * This module provides access to runtime environment variables, as defined by the platform you're running on. For example if you're using [`adapter-node`](https://github.com/sveltejs/kit/tree/master/packages/adapter-node) (or running [`vite preview`](https://kit.svelte.dev/docs/cli)), this is equivalent to `process.env`. This module only includes variables that _do not_ begin with [`config.kit.env.publicPrefix`](https://kit.svelte.dev/docs/configuration#env) _and do_ start with [`config.kit.env.privatePrefix`](https://kit.svelte.dev/docs/configuration#env) (if configured).
 * 
 * This module cannot be imported into client-side code.
 * 
 * ```ts
 * import { env } from '$env/dynamic/private';
 * console.log(env.DEPLOYMENT_SPECIFIC_VARIABLE);
 * ```
 * 
 * > In `dev`, `$env/dynamic` always includes environment variables from `.env`. In `prod`, this behavior will depend on your adapter.
 */
declare module '$env/dynamic/private' {
	export const env: {
		SHELL: string;
		COLORTERM: string;
		NVM_INC: string;
		WSL2_GUI_APPS_ENABLED: string;
		TERM_PROGRAM_VERSION: string;
		npm_package_devDependencies_eslint_plugin_svelte: string;
		NVIM: string;
		WSL_DISTRO_NAME: string;
		ZSH_CACHE_DIR: string;
		TMUX: string;
		npm_config_resolution_mode: string;
		_P9K_TTY: string;
		NODE: string;
		npm_package_devDependencies_tslib: string;
		npm_config_ignore_scripts: string;
		npm_package_devDependencies__types_cookie: string;
		npm_package_scripts_check_watch: string;
		P9K_TTY: string;
		OPENAI_API_KEY: string;
		npm_config_argv: string;
		NVIM_LOG_FILE: string;
		npm_config_bin_links: string;
		NNN_FIFO: string;
		EDITOR: string;
		npm_package_scripts_test_unit: string;
		MASON: string;
		PMSPEC: string;
		NAME: string;
		PWD: string;
		npm_config_save_prefix: string;
		npm_package_devDependencies_vite: string;
		LOGNAME: string;
		npm_package_readmeFilename: string;
		npm_package_devDependencies__typescript_eslint_parser: string;
		PNPM_HOME: string;
		npm_package_scripts_build: string;
		_: string;
		npm_package_devDependencies_prettier: string;
		npm_package_devDependencies_eslint_config_prettier: string;
		HOME: string;
		npm_config_version_git_tag: string;
		LANG: string;
		WSL_INTEROP: string;
		npm_package_devDependencies_typescript: string;
		npm_config_init_license: string;
		npm_package_version: string;
		KEYTIMEOUT: string;
		npm_package_devDependencies__typescript_eslint_eslint_plugin: string;
		WAYLAND_DISPLAY: string;
		NNN_PLUG: string;
		npm_config_version_commit_hooks: string;
		npm_package_scripts_test_integration: string;
		npm_package_devDependencies_prettier_plugin_svelte: string;
		INIT_CWD: string;
		npm_package_scripts_format: string;
		npm_package_scripts_preview: string;
		npm_lifecycle_script: string;
		npm_package_description: string;
		NVM_DIR: string;
		npm_config_version_tag_prefix: string;
		YARN_WRAP_OUTPUT: string;
		ZPFX: string;
		npm_package_devDependencies_svelte_check: string;
		TERM: string;
		npm_package_name: string;
		npm_package_type: string;
		USER: string;
		npm_package_devDependencies_vitest: string;
		TMUX_PANE: string;
		DISPLAY: string;
		npm_lifecycle_event: string;
		SHLVL: string;
		npm_config_version_git_sign: string;
		NVM_CD_FLAGS: string;
		npm_config_version_git_message: string;
		npm_package_devDependencies_eslint: string;
		_P9K_SSH_TTY: string;
		npm_config_user_agent: string;
		npm_package_scripts_lint: string;
		npm_package_devDependencies__fontsource_fira_mono: string;
		PRETTIERD_DEFAULT_CONFIG: string;
		npm_execpath: string;
		npm_package_devDependencies__sveltejs_adapter_auto: string;
		npm_package_devDependencies_svelte: string;
		npm_package_scripts_test: string;
		XDG_RUNTIME_DIR: string;
		MYVIMRC: string;
		npm_config_strict_ssl: string;
		DEBUGINFOD_URLS: string;
		WSLENV: string;
		P9K_SSH: string;
		npm_package_scripts_dev: string;
		npm_package_scripts_check: string;
		PATH: string;
		npm_package_devDependencies__neoconfetti_svelte: string;
		npm_package_devDependencies__sveltejs_kit: string;
		NNN_SSHFS: string;
		DBUS_SESSION_BUS_ADDRESS: string;
		npm_package_devDependencies__playwright_test: string;
		NVM_BIN: string;
		npm_config_registry: string;
		HOSTTYPE: string;
		npm_config_ignore_optional: string;
		PULSE_SERVER: string;
		npm_node_execpath: string;
		npm_config_engine_strict: string;
		OLDPWD: string;
		TERM_PROGRAM: string;
		npm_config_init_version: string;
		NODE_ENV: string;
		[key: `PUBLIC_${string}`]: undefined;
		[key: `${string}`]: string | undefined;
	}
}

/**
 * Similar to [`$env/dynamic/private`](https://kit.svelte.dev/docs/modules#$env-dynamic-private), but only includes variables that begin with [`config.kit.env.publicPrefix`](https://kit.svelte.dev/docs/configuration#env) (which defaults to `PUBLIC_`), and can therefore safely be exposed to client-side code.
 * 
 * Note that public dynamic environment variables must all be sent from the server to the client, causing larger network requests — when possible, use `$env/static/public` instead.
 * 
 * ```ts
 * import { env } from '$env/dynamic/public';
 * console.log(env.PUBLIC_DEPLOYMENT_SPECIFIC_VARIABLE);
 * ```
 */
declare module '$env/dynamic/public' {
	export const env: {
		[key: `PUBLIC_${string}`]: string | undefined;
	}
}
