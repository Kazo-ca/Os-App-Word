// The OS side of a Kazo Word window (section 2 of doc/en/integration-plan.md).
//
// It creates the iframe that runs WordCraft (wwwroot/host/index.html), hands it one MessagePort
// when it has loaded, and passes the proxy's messages to the window's C# (WordFrame). It decides
// nothing. One call to `create` = one iframe and one port: two windows never share a channel.

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
 * @returns {{ focus(): void, dispose(): void }}
 */
export function create(container, dotnet, options = {}) {
    const iframe = document.createElement("iframe");
    iframe.className = "word-frame-iframe";
    iframe.title = options.title ?? "WordCraft";
    iframe.allow = "fullscreen; clipboard-read; clipboard-write";
    iframe.src = frameUrl(import.meta.url, options);

    let port = null;
    let disposed = false;

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
            default:
                // Messages of the next work items (save, pick, command results) are not used yet.
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
        dispose() {
            disposed = true;
            port?.close();
            iframe.remove();
        },
    };
}
