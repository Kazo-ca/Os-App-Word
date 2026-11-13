// The address of the iframe page built by the OS side (wwwroot/js/word-host.js).
import { test } from "node:test";
import assert from "node:assert/strict";
import { frameUrl } from "../../src/Kazo.Word/wwwroot/js/word-host.js";

const base = "https://os.example/_content/Kazo.Word/js/word-host.js";

test("the iframe page is the app's host page", () => {
    assert.equal(frameUrl(base), "https://os.example/_content/Kazo.Word/host/index.html");
});

test("the author goes on the address, encoded", () => {
    assert.equal(
        frameUrl(base, { author: "Zoé & Neo" }),
        "https://os.example/_content/Kazo.Word/host/index.html?author=Zo%C3%A9+%26+Neo");
});

test("webgl forces WordCraft's WebGL 2 renderer", () => {
    const url = new URL(frameUrl(base, { author: "Neo", webgl: true }));
    assert.equal(url.searchParams.get("author"), "Neo");
    assert.equal(url.searchParams.has("webgl"), true);
});
