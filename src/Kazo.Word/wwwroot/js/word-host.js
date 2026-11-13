// The OS side of a Kazo Word window (section 2 of doc/en/integration-plan.md).
//
// It creates the iframe that runs WordCraft (wwwroot/host/index.html), hands it one MessagePort
// when it has loaded, and passes the proxy's messages to the window's C# (WordFrame). It decides
// nothing. One call to `create` = one iframe and one port: two windows never share a channel.
// `request` sends a `command` to the proxy and settles with its `result` (WordCraft's page API).
// Work item 3: the chords C# reserves are replayed on the OS page (target "os") or passed to C#
// (target "app"); file picks and setting writes go to C#; the OS color scheme is watched.

/** Address of the iframe page, served by the app's Razor library next to this module. */
export const FramePagePath = "../host/index.html";

/**
 * Builds the iframe address: `author` names the user for WordCraft's comments and tracked
 * changes, `webgl` forces WordCraft's WebGL 2 renderer.
 * @param {string} base Address of this module.
 * @param {{ author?: string, webgl?: boolean }} options
 * @returns {string}
 */
export function frameUrl(base, { author = "", webgl = false } = {}) {
    const url = new URL(FramePagePath, base);
    if (author) {
        url.searchParams.set("author", author);
    }

    if (webgl) {
        url.searchParams.set("webgl", "");
    }

    return url.href;
}

/**
 * Creates the iframe in `container` and connects it.
 * @param {HTMLElement} container Element of the window that holds the iframe.
 * @param {{ invokeMethodAsync(name: string, ...args: unknown[]): Promise<unknown> }} dotnet The WordFrame.
 * @param {{ author?: string, language?: string, webgl?: boolean, title?: string,
 *           reservedKeys?: object[], settings?: Record<string, string> | null }} options
 * @returns {{ focus(): void, request(method: string, params?: object, timeoutMs?: number): Promise<unknown>,
 *             answerPick(id: number, files: { name: string, type: string, bytes: Uint8Array }[] | null): void,
 *             colorScheme(): string, dispose(): void }}
 */
export function create(container, dotnet, options = {}) {
    const iframe = document.createElement("iframe");
    iframe.className = "word-frame-iframe";
    iframe.title = options.title ?? "WordCraft";
    iframe.allow = "fullscreen; clipboard-read; clipboard-write";
    iframe.src = frameUrl(import.meta.url, options);

    let port = null;
    let disposed = false;
    let nextCommandId = 1;
    // Commands sent and not answered yet, by id: { resolve, reject }.
    const pending = new Map();
    // The user's settings as WordCraft last wrote them: a reloaded page starts from them.
    const settings = options.settings ? { ...options.settings } : null;

    // Every command still waiting fails: the page reloaded (its WordCraft is a new one) or the
    // window is closing.
    const abandonCommands = (reason) => {
        for (const { reject } of pending.values()) {
            reject(new Error(reason));
        }

        pending.clear();
    };

    const deliver = (type, value) => {
        if (!disposed) {
            dotnet.invokeMethodAsync("OnFrameMessage", type, value ?? null).catch(() => {
                // The window is closing: nobody listens any more.
            });
        }
    };

    const onMessage = (event) => {
        const message = event.data ?? {};
        switch (message.type) {
            case "ready":
                deliver("ready", message.appVersion);
                break;
            case "failed":
                deliver("failed", message.error);
                break;
            case "dirty":
                deliver("dirty", message.dirty ? "true" : "false");
                break;
            case "focus":
                // The window's frame activates it on pointerdown; a click inside the iframe never
                // reaches the OS document, so the same event is replayed on the iframe element.
                iframe.dispatchEvent(new PointerEvent("pointerdown", { bubbles: true, cancelable: true }));
                break;
            case "result": {
                const waiting = pending.get(message.id);
                if (waiting) {
                    pending.delete(message.id);
                    if (message.ok) {
                        waiting.resolve(message.value ?? null);
                    } else {
                        waiting.reject(new Error(String(message.error ?? "unknown error")));
                    }
                }

                break;
            }
            case "key":
                if (message.target === "os") {
                    replayKey(iframe, message);
                } else if (message.phase === "down") {
                    deliver("key", JSON.stringify(message));
                }

                break;
            case "pick":
                deliver("pick", JSON.stringify({ id: message.id, accept: message.accept, multiple: message.multiple }));
                break;
            case "setting":
                if (settings) {
                    if (message.value === null) {
                        delete settings[message.key];
                    } else {
                        settings[message.key] = message.value;
                    }
                }

                deliver("setting", JSON.stringify({ key: message.key, value: message.value }));
                break;
            default:
                // Messages of work item 2 (save) are handled there.
                break;
        }
    };

    // Each load of the page gets a new port. The page loads once, unless the iframe element is
    // moved in the document (a moved iframe reloads): WordCraft then starts again and is connected
    // again, and the old port is closed so nothing late arrives from it.
    iframe.addEventListener("load", () => {
        if (disposed) {
            return;
        }

        port?.close();
        abandonCommands("WordCraft restarted");
        const channel = new MessageChannel();
        port = channel.port1;
        port.onmessage = onMessage;
        iframe.contentWindow.postMessage({
            type: "init",
            author: options.author ?? "",
            language: options.language ?? "en",
            reservedKeys: options.reservedKeys ?? [],
            settings,
        }, window.location.origin, [channel.port2]);
    });

    container.replaceChildren(iframe);

    // The OS theme: its color scheme is a CSS variable of the theme the window sits in. A theme
    // change rewrites data-theme (or data-kos-theme) on an ancestor; C# hears the new scheme.
    let scheme = readColorScheme(container);
    const themeObserver = new MutationObserver(() => {
        const next = readColorScheme(container);
        if (next !== scheme) {
            scheme = next;
            deliver("scheme", next);
        }
    });
    themeObserver.observe(document.documentElement, {
        attributes: true,
        subtree: true,
        attributeFilter: ["data-theme", "data-kos-theme"],
    });

    return {
        focus() {
            iframe.contentWindow?.focus();
        },
        request(method, params = {}, timeoutMs = 0) {
            if (!port || disposed) {
                return Promise.reject(new Error("WordCraft is not connected"));
            }

            const id = nextCommandId++;
            return new Promise((resolve, reject) => {
                pending.set(id, { resolve, reject });
                port.postMessage({ type: "command", id, method, params, timeoutMs });
            });
        },
        answerPick(id, files) {
            if (!port || disposed) {
                return;
            }

            if (!files) {
                port.postMessage({ type: "picked", id, cancelled: true });
                return;
            }

            // Each file's bytes are copied once into their own buffer, then moved to the iframe.
            const sent = files.map((file) => ({ name: file.name, type: file.type, bytes: file.bytes.slice().buffer }));
            port.postMessage({ type: "picked", id, files: sent }, sent.map((file) => file.bytes));
        },
        colorScheme() {
            return scheme;
        },
        dispose() {
            disposed = true;
            themeObserver.disconnect();
            abandonCommands("the window closed");
            port?.close();
            iframe.remove();
        },
    };
}

/**
 * The color scheme of the OS theme around `element`: "light" or "dark" (Kazo OS themes set
 * --kos-color-scheme; dark when none is set, as the OS default theme).
 * @param {Element} element
 * @returns {string}
 */
export function readColorScheme(element) {
    const value = getComputedStyle(element).getPropertyValue("--kos-color-scheme").trim().toLowerCase();
    return value === "light" ? "light" : "dark";
}

/**
 * Replays a key the iframe gave back on the OS page, where the window manager listens (capture on
 * the document). Sent from the iframe element, so it starts inside the window that owns it.
 * @param {HTMLIFrameElement} iframe
 * @param {{ phase: string, key: string, code: string, ctrl: boolean, alt: boolean, shift: boolean, meta: boolean, repeat: boolean }} message
 */
export function replayKey(iframe, message) {
    iframe.dispatchEvent(new KeyboardEvent(message.phase === "up" ? "keyup" : "keydown", {
        key: message.key,
        code: message.code,
        ctrlKey: message.ctrl,
        altKey: message.alt,
        shiftKey: message.shift,
        metaKey: message.meta,
        repeat: message.repeat,
        bubbles: true,
        cancelable: true,
        composed: true,
    }));
}
