// The OS side of a Kazo Word window (section 2 of doc/en/integration-plan.md).
//
// It creates the iframe that runs WordCraft (wwwroot/host/index.html), hands it one MessagePort
// when it has loaded, and passes the proxy's messages to the window's C# (WordFrame). It decides
// nothing. One call to `create` = one iframe and one port: two windows never share a channel.
// `request` sends a `command` to the proxy and settles with its `result` (WordCraft's page API).

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
 * @param {{ author?: string, language?: string, webgl?: boolean, title?: string }} options
 * @returns {{ focus(): void, open(name: string, bytes: Uint8Array): boolean, request(method: string, params?: object, timeoutMs?: number): Promise<unknown>, dispose(): void }}
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
            case "save":
                // The bytes cross to .NET as a byte[] (Blazor's binary interop), without base64.
                if (!disposed) {
                    const bytes = message.bytes instanceof ArrayBuffer ? new Uint8Array(message.bytes) : new Uint8Array(0);
                    dotnet.invokeMethodAsync("OnFrameSave", String(message.name ?? ""), message.saveAs === true, bytes).catch(() => {
                        // The window is closing: the save is lost with it, as WordCraft is.
                    });
                }

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
            default:
                // Messages of the next work items (save, pick) are not used yet.
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
            reservedKeys: [],
        }, window.location.origin, [channel.port2]);
    });

    container.replaceChildren(iframe);

    return {
        focus() {
            iframe.contentWindow?.focus();
        },
        /**
         * Sends a document to WordCraft (`open`); its bytes are transferred, not copied again.
         * @param {string} name The file name WordCraft gives the document.
         * @param {Uint8Array} bytes The file's content, from .NET.
         * @returns {boolean} false when WordCraft is not connected.
         */
        open(name, bytes) {
            if (!port || disposed) {
                return false;
            }

            const buffer = bytes.byteOffset === 0 && bytes.byteLength === bytes.buffer.byteLength
                ? bytes.buffer
                : bytes.slice().buffer;
            port.postMessage({ type: "open", name, bytes: buffer }, [buffer]);
            return true;
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
        dispose() {
            disposed = true;
            abandonCommands("the window closed");
            port?.close();
            iframe.remove();
        },
    };
}
