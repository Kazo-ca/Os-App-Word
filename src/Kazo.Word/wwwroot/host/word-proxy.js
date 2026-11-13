// The proxy of the Kazo Word iframe (sections 2 and 5 of doc/en/integration-plan.md).
//
// It starts WordCraft in page host mode on this page's canvas, and relays, without interpreting
// them, WordCraft's canvas events (wordcraft:ready, failed, save, dirty) to the Kazo Word window
// and the window's messages (open) to WordCraft. The window hands over one MessagePort with its
// first message, `init`; messages that happen before it are kept and sent once the port is there.
// A click anywhere in the page sends `focus`, so the Kazo window comes to the front.
//
// The proxy decides nothing: every decision is in the window's C#.

/** Names of the messages on the port (section 5 of the integration plan). */
export const MessageTypes = Object.freeze({
    init: "init",
    ready: "ready",
    failed: "failed",
    open: "open",
    save: "save",
    dirty: "dirty",
    focus: "focus",
});

/** Path of WordCraft's provenance file, relative to this page; it names the glue and the .wasm. */
export const BuildManifestPath = "../wordcraft/wordcraft.json";

/**
 * Loads WordCraft: reads the provenance file, imports the wasm-bindgen glue it names, and starts
 * the .wasm. WordCraft finds the canvas itself (id wordcraft_canvas, data-host="page").
 * @param {URL} manifestUrl Address of wordcraft.json.
 * @returns {Promise<void>} Settles when WordCraft's init returned.
 */
export async function loadWordCraft(manifestUrl) {
    const response = await fetch(manifestUrl);
    if (!response.ok) {
        throw new Error(`${manifestUrl.pathname}: HTTP ${response.status}`);
    }

    const build = await response.json();
    const glue = await import(new URL(build.glue, manifestUrl).href);
    await glue.default({ module_or_path: new URL(build.wasm, manifestUrl) });
}

/**
 * Starts the proxy.
 * @param {object} options
 * @param {EventTarget} options.page The page's window: receives `init`, and clicks.
 * @param {EventTarget} options.canvas WordCraft's canvas, where its events arrive.
 * @param {() => Promise<void>} options.start Starts WordCraft (loadWordCraft in the browser).
 * @param {string} options.origin The only origin `init` is accepted from (this page's own).
 * @param {object|null} [options.parent] The window allowed to send `init` (the page's parent).
 * @returns {{ stopped: Promise<void> }} `stopped` settles when the start attempt ended.
 */
export function startProxy({ page, canvas, start, origin, parent = null }) {
    let port = null;
    let settled = false;
    const outbox = [];

    const send = (message, transfer = []) => {
        if (port) {
            port.postMessage(message, transfer);
        } else {
            outbox.push([message, transfer]);
        }
    };

    // WordCraft → window. Added before WordCraft starts: it announces `ready` during its init.
    canvas.addEventListener("wordcraft:ready", (event) => {
        settled = true;
        send({ type: MessageTypes.ready, appVersion: String(event.detail?.appVersion ?? "") });
    });
    canvas.addEventListener("wordcraft:failed", (event) => {
        settled = true;
        send({ type: MessageTypes.failed, error: String(event.detail?.error ?? "unknown error") });
    });
    canvas.addEventListener("wordcraft:dirty", (event) => {
        send({ type: MessageTypes.dirty, dirty: event.detail?.dirty === true });
    });
    canvas.addEventListener("wordcraft:save", (event) => {
        const { name, saveAs, bytes } = event.detail ?? {};
        const transfer = bytes instanceof ArrayBuffer ? [bytes] : [];
        send({ type: MessageTypes.save, name: String(name ?? ""), saveAs: saveAs === true, bytes }, transfer);
    });

    // Window → WordCraft.
    const onPortMessage = (event) => {
        const message = event.data;
        if (message?.type === MessageTypes.open) {
            canvas.dispatchEvent(new CustomEvent("wordcraft:open", {
                detail: { name: message.name, bytes: message.bytes },
            }));
        }
    };

    // The first message from the window, with the port. Any other sender, or a second `init`, is
    // ignored: the port is this window's alone.
    page.addEventListener("message", (event) => {
        if (port || event.data?.type !== MessageTypes.init || event.origin !== origin) {
            return;
        }

        if (parent !== null && event.source !== parent) {
            return;
        }

        const [received] = event.ports ?? [];
        if (!received) {
            return;
        }

        port = received;
        port.onmessage = onPortMessage;
        for (const [message, transfer] of outbox.splice(0)) {
            port.postMessage(message, transfer);
        }
    });

    // A click in WordCraft: the Kazo window comes to the front (capture: WordCraft may stop it).
    page.addEventListener("pointerdown", () => send({ type: MessageTypes.focus }), true);

    const stopped = Promise.resolve()
        .then(start)
        .catch((error) => {
            // WordCraft reports its own start errors with wordcraft:failed; this one is ours (the
            // provenance file, the glue or the .wasm could not be loaded).
            if (!settled) {
                settled = true;
                send({ type: MessageTypes.failed, error: String(error?.message ?? error) });
            }
        });

    return { stopped };
}

// In the iframe page: start at once, so WordCraft downloads while the window gets ready.
if (typeof document !== "undefined" && document.getElementById("wordcraft_canvas")) {
    startProxy({
        page: window,
        canvas: document.getElementById("wordcraft_canvas"),
        start: () => loadWordCraft(new URL(BuildManifestPath, import.meta.url)),
        origin: window.location.origin,
        parent: window.parent,
    });
}
