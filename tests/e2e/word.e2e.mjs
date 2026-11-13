// End-to-end runs of Kazo Word inside the Kazo OS host (work item 1 of doc/en/integration-plan.md):
// `npm run test:e2e` after `dotnet build Kazo.Word.slnx -c Release`. Each test opens a fresh page.
//
// Chromium runs with a software WebGPU adapter (SwiftShader), so both of WordCraft's renderers are
// tried: WebGPU by default, WebGL 2 with the `--webgl` launch argument.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { boot, openApp, runInTerminal, startHost } from './host.mjs';

// WordCraft's first start downloads 61 MB of .wasm from the local host: a few seconds.
const READY_TIMEOUT_MS = 90_000;

const launchOptions = {
    args: ['--enable-unsafe-webgpu', '--enable-features=Vulkan', '--use-vulkan=swiftshader', '--use-webgpu-adapter=swiftshader', '--use-angle=vulkan'],
};

let host;
let browser;

before(async () => {
    host = await startHost();
    browser = await chromium.launch(launchOptions);
});

after(async () => {
    await browser?.close();
    host?.stop();
});

/** A fresh page with the OS booted; `renderers` collects the backend each WordCraft chose. */
async function bootPage() {
    const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
    const errors = [];
    const renderers = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => {
        const backend = /wgpu backend (\w+)/.exec(message.text());
        if (backend) {
            renderers.push(backend[1]);
        }
    });
    await boot(page, host.baseUrl);
    return { page, errors, renderers };
}

/** Kazo Word's OS windows, front-most last. */
const wordWindows = page => page.locator('article[data-window-id]', { has: page.locator('.word-window') });

/** The WordCraft frames (iframe pages) of the page, in window order. */
const wordFrames = page => page.frames().filter(frame => frame.url().includes('/_content/Kazo.Word/host/index.html'));

/** Waits until `count` Kazo Word windows show WordCraft. */
async function waitForWordCraft(page, count = 1) {
    await page.locator('.word-frame.running').nth(count - 1).waitFor({ timeout: READY_TIMEOUT_MS });
    assert.equal(await page.locator('.word-screen').count(), 0, 'no loading or error screen is left');
}

/**
 * Starts a blank document in WordCraft and types `text` with the keyboard. WordCraft opens on its
 * start screen (host mode); `file.new` makes the document, Escape closes the start screen, and a
 * click in the page puts the caret there.
 */
async function typeInBlankDocument(page, frame, iframe, text) {
    await frame.evaluate(() => window.wordcraft.execute('file.new', {}));
    const box = await iframe.boundingBox();
    const middle = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
    await page.mouse.click(middle.x, middle.y);
    await page.keyboard.press('Escape');
    await frame.waitForFunction(async () => (await window.wordcraft.uiInspect({})).ui.backstage === false, null, { timeout: 10_000 });
    await page.mouse.click(middle.x, middle.y);
    await page.keyboard.type(text, { delay: 20 });
}

/** The text of the document WordCraft shows, from its page API; empty while no document is open. */
async function documentText(frame) {
    const summary = await frame.evaluate(() => window.wordcraft.docInspect().catch(error => {
        if (/no document is open/.test(error.message)) {
            return { blocks: [] };
        }
        throw error;
    }));
    return summary.blocks.map(block => block.text ?? '').join('\n');
}

test('Kazo Word opens from the Applications menu and WordCraft takes the keyboard', { timeout: 300_000 }, async () => {
    const { page, errors, renderers } = await bootPage();

    await openApp(page, 'Kazo Word');
    await waitForWordCraft(page);

    const [frame] = wordFrames(page);
    assert.match(frame.url(), /\?author=Neo$/, 'the user is WordCraft\'s author');
    await typeInBlankDocument(page, frame, page.locator('iframe.word-frame-iframe'), 'Hello Kazo');
    assert.match(await documentText(frame), /Hello Kazo/);

    assert.deepEqual(renderers, ['BrowserWebGpu']);
    assert.deepEqual(errors, []);
    await page.close();
});

test('the --webgl launch argument starts WordCraft on WebGL 2', { timeout: 300_000 }, async () => {
    const { page, errors, renderers } = await bootPage();

    await runInTerminal(page, 'launch ca.kazo.word --webgl');
    await waitForWordCraft(page);

    const [frame] = wordFrames(page);
    assert.equal(new URL(frame.url()).searchParams.has('webgl'), true);
    await typeInBlankDocument(page, frame, page.locator('iframe.word-frame-iframe'), 'WebGL');
    assert.match(await documentText(frame), /WebGL/);

    assert.deepEqual(renderers, ['Gl']);
    assert.deepEqual(errors, []);
    await page.close();
});

test('two Kazo Word windows run at once, and a click in WordCraft brings its window to the front', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage();

    await openApp(page, 'Kazo Word');
    await waitForWordCraft(page, 1);
    await openApp(page, 'Kazo Word');
    await waitForWordCraft(page, 2);

    const windows = wordWindows(page);
    assert.equal(await windows.count(), 2);
    const [first, second] = wordFrames(page);
    assert.ok(first && second && first !== second, 'each window has its own WordCraft');

    // Each WordCraft keeps its own document: text typed in the front window stays there.
    const firstId = await windows.nth(0).getAttribute('data-window-id');
    const secondId = await windows.nth(1).getAttribute('data-window-id');
    const byId = id => page.locator(`article[data-window-id="${id}"]`);
    await typeInBlankDocument(page, second, byId(secondId).locator('iframe.word-frame-iframe'), 'second');
    assert.match(await documentText(second), /second/);
    assert.doesNotMatch(await documentText(first), /second/);

    // The second window is in front. Move it aside by its title so part of the first one shows,
    // then click inside the first window's WordCraft: that window comes to the front. The OS
    // reorders its windows in the page, so they are found again by id.
    const zOf = id => byId(id).getAttribute('data-window-z').then(Number);
    assert.ok(await zOf(secondId) > await zOf(firstId));

    const title = await byId(secondId).locator('h2').first().boundingBox();
    await page.mouse.move(title.x + 40, title.y + title.height / 2);
    await page.mouse.down();
    await page.mouse.move(title.x + 290, title.y + title.height / 2 + 120, { steps: 8 });
    await page.mouse.up();

    const firstBox = await byId(firstId).locator('iframe.word-frame-iframe').boundingBox();
    await page.mouse.click(firstBox.x + 120, firstBox.y + firstBox.height - 60);
    await page.waitForFunction(
        ([front, back]) => {
            const z = id => Number(document.querySelector(`article[data-window-id="${id}"]`)?.getAttribute('data-window-z'));
            return z(front) > z(back);
        },
        [firstId, secondId],
        { timeout: 10_000 });

    assert.deepEqual(errors, []);
    await page.close();
});

test('a document survives when its window comes to the front, and back behind (decision 14)', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage();

    await openApp(page, 'Kazo Word');
    await waitForWordCraft(page, 1);
    const [frame] = wordFrames(page);
    const firstId = await wordWindows(page).first().getAttribute('data-window-id');
    const byId = id => page.locator(`article[data-window-id="${id}"]`);
    await typeInBlankDocument(page, frame, byId(firstId).locator('iframe.word-frame-iframe'), 'kept');
    assert.match(await documentText(frame), /kept/);

    // A second window opens in front, then the first comes back to the front by a click in its
    // WordCraft. Kazo OS keeps the windows in their opening order in the page (ADR 0244), so the
    // first iframe is never moved: same frame, same WordCraft, same document.
    await openApp(page, 'Kazo Word');
    await waitForWordCraft(page, 2);
    const secondId = await wordWindows(page).nth(1).getAttribute('data-window-id');
    const title = await byId(secondId).locator('h2').first().boundingBox();
    await page.mouse.move(title.x + 40, title.y + title.height / 2);
    await page.mouse.down();
    await page.mouse.move(title.x + 290, title.y + title.height / 2 + 120, { steps: 8 });
    await page.mouse.up();

    const firstBox = await byId(firstId).locator('iframe.word-frame-iframe').boundingBox();
    await page.mouse.click(firstBox.x + 120, firstBox.y + firstBox.height - 60);
    const zOf = id => byId(id).getAttribute('data-window-z').then(Number);
    await page.waitForFunction(
        ([front, back]) => {
            const z = id => Number(document.querySelector(`article[data-window-id="${id}"]`)?.getAttribute('data-window-z'));
            return z(front) > z(back);
        },
        [firstId, secondId],
        { timeout: 10_000 });

    assert.equal(frame.isDetached(), false, 'the first iframe was not reloaded');
    assert.match(await documentText(frame), /kept/);

    // And back behind: the second window comes to the front by a click on the part of its title
    // bar the first window does not cover (its right end, left of the window buttons).
    const secondWindow = await byId(secondId).boundingBox();
    const secondTitle = await byId(secondId).locator('h2').first().boundingBox();
    await page.mouse.click(secondWindow.x + secondWindow.width - 200, secondTitle.y + secondTitle.height / 2);
    await page.waitForFunction(
        ([front, back]) => {
            const z = id => Number(document.querySelector(`article[data-window-id="${id}"]`)?.getAttribute('data-window-z'));
            return z(front) > z(back);
        },
        [secondId, firstId],
        { timeout: 10_000 });
    assert.ok(await zOf(secondId) > await zOf(firstId));
    assert.equal(frame.isDetached(), false, 'the first iframe was not reloaded');
    assert.match(await documentText(frame), /kept/);

    assert.deepEqual(errors, []);
    await page.close();
});
