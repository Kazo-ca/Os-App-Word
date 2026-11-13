// End-to-end runs of opening and saving (work item 2 of doc/en/integration-plan.md), inside the
// Kazo OS host: `npm run test:e2e` after `dotnet build Kazo.Word.slnx -c Release`.
//
// WordCraft is driven through its page API (window.wordcraft in the iframe) to type and to ask for
// a save, as its ribbon and Ctrl+S do; everything else goes through the OS: its save dialog, its
// message box, the window's title bar and close button, and `xdg-open` in the Terminal, which starts
// the app associated with the file type.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { boot, openApp, runInTerminal, startHost } from './host.mjs';

const READY_TIMEOUT_MS = 90_000;
const STEP_TIMEOUT_MS = 20_000;
const HOME = '/home/neo';

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

async function bootPage() {
    const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await boot(page, host.baseUrl);
    return { page, errors };
}

const wordWindows = page => page.locator('article[data-window-id]', { has: page.locator('.word-window') });
const windowById = (page, id) => page.locator(`article[data-window-id="${id}"]`);

/** Waits for `count` Kazo Word windows to show WordCraft; returns the id of the newest one. */
async function waitForWindow(page, count, known = []) {
    try {
        await page.locator('.word-frame.running').nth(count - 1).waitFor({ timeout: READY_TIMEOUT_MS });
    } catch (error) {
        if (process.env.WORD_E2E_SHOTS) {
            await page.screenshot({ path: `${process.env.WORD_E2E_SHOTS}/window-${count}.png` });
        }
        throw error;
    }
    const ids = await wordWindows(page).evaluateAll(nodes => nodes.map(node => node.getAttribute('data-window-id')));
    assert.equal(ids.length, count);
    return ids.find(id => !known.includes(id));
}

/** The WordCraft frame of a window, through its iframe element. */
async function frameOf(page, id) {
    const handle = await windowById(page, id).locator('iframe.word-frame-iframe').elementHandle();
    return handle.contentFrame();
}

/** Runs a WordCraft command in a frame. */
const execute = (frame, command, params = {}) =>
    frame.evaluate(([name, args]) => window.wordcraft.execute(name, args), [command, params]);

/** The text of the document a frame shows. */
async function documentText(frame) {
    const summary = await frame.evaluate(() => window.wordcraft.docInspect());
    return summary.blocks.map(block => block.text ?? '').join('\n');
}

/** Waits until the document a frame shows contains `pattern`. */
const waitForText = (frame, pattern) => frame.waitForFunction(async source => {
    try {
        return (await window.wordcraft.docInspect()).blocks.some(block => new RegExp(source).test(block.text ?? ''));
    } catch {
        return false;
    }
}, pattern.source, { timeout: STEP_TIMEOUT_MS });

/** Answers the OS save dialog with a file name (in the folder it opens in, the home folder). */
async function answerSaveDialog(page, title, fileName) {
    const dialog = page.locator('section.file-dialog', { hasText: title });
    await dialog.waitFor({ state: 'visible', timeout: STEP_TIMEOUT_MS });
    await dialog.getByLabel('File name').fill(fileName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
}

/** Waits for a window's title bar to read `text`; on failure, says what it reads and the window's message. */
async function expectTitle(page, id, text) {
    try {
        await page.waitForFunction(
            ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
            [id, text],
            { timeout: STEP_TIMEOUT_MS });
    } catch (error) {
        const actual = await windowById(page, id).locator('header.window-chrome h2').first().textContent().catch(() => null);
        const notice = await windowById(page, id).locator('.word-notice').textContent().catch(() => null);
        throw new Error(`title "${actual}" instead of "${text}"; notice: ${notice}`, { cause: error });
    }
}

test('a new document is saved where the user says, saved again in place, and reopens from the OS', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage();

    await openApp(page, 'Kazo Word');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await expectTitle(page, id, 'Document1 — Kazo Word');
    assert.equal((await frame.evaluate(() => window.wordcraft.uiInspect({}))).ui.backstage, false, 'the start screen is closed');

    await execute(frame, 'text.insert', { text: 'Saved by Kazo Word' });
    await expectTitle(page, id, '*Document1 — Kazo Word');

    // The first save of a new document asks where to write it.
    await execute(frame, 'file.save');
    await answerSaveDialog(page, 'Save the document', 'letter.docx');
    await expectTitle(page, id, 'letter.docx — Kazo Word');

    // The next save writes the same file, with no dialog.
    await execute(frame, 'text.insert', { text: ' again' });
    await expectTitle(page, id, '*letter.docx — Kazo Word');
    await execute(frame, 'file.save');
    await expectTitle(page, id, 'letter.docx — Kazo Word');
    assert.equal(await page.locator('section.file-dialog').count(), 0);

    // `xdg-open` starts the app associated with .docx: a second Kazo Word window, on the file.
    await runInTerminal(page, `xdg-open ${HOME}/letter.docx`);
    const second = await waitForWindow(page, 2, [id]);
    await expectTitle(page, second, 'letter.docx — Kazo Word');
    const reopened = await frameOf(page, second);
    await waitForText(reopened, /Saved by Kazo Word again/);
    assert.doesNotMatch(await documentText(frame), /^$/);

    assert.deepEqual(errors, []);
    await page.close();
});

test('Save As binds the document to the new file, and an export is a copy', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage();

    await openApp(page, 'Kazo Word');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await execute(frame, 'text.insert', { text: 'Report' });

    await execute(frame, 'file.saveAs', { path: 'report.odt' });
    await answerSaveDialog(page, 'Save the document', 'report.odt');
    await expectTitle(page, id, 'report.odt — Kazo Word');

    // A PDF is a copy: its own dialog, a message, and the document stays report.odt.
    await execute(frame, 'file.saveAs', { path: 'report.pdf' });
    await answerSaveDialog(page, 'Save a copy', 'report.pdf');
    await windowById(page, id).locator('.word-notice', { hasText: 'Copy saved: report.pdf.' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await expectTitle(page, id, 'report.odt — Kazo Word');

    await runInTerminal(page, `ls ${HOME}`);
    const rows = page.locator('.xterm-rows').last();
    await rows.getByText('report.pdf', { exact: false }).first().waitFor({ timeout: STEP_TIMEOUT_MS });
    assert.match(await rows.innerText(), /report\.odt/);

    assert.deepEqual(errors, []);
    await page.close();
});

test('closing with changes asks; Cancel keeps the window, Yes saves before closing', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage();

    await openApp(page, 'Kazo Word');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await execute(frame, 'text.insert', { text: 'Do not lose me' });
    await expectTitle(page, id, '*Document1 — Kazo Word');

    const close = windowById(page, id).locator('header.window-chrome .chrome-button-close');
    const question = page.locator('.message-box-dialog', { hasText: 'Save the changes' });

    // Cancel: nothing closes, nothing is lost.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Cancel' }).click();
    await question.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);
    assert.match(await documentText(frame), /Do not lose me/);

    // Yes, then Cancel in the save dialog: the window stays open and modified.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    const saveDialog = page.locator('section.file-dialog', { hasText: 'Save the document' });
    await saveDialog.waitFor({ timeout: STEP_TIMEOUT_MS });
    await saveDialog.getByRole('button', { name: 'Cancel' }).click();
    await windowById(page, id).locator('.word-notice', { hasText: 'was not saved' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);
    // WordCraft named the document from its first words when it asked for the save.
    await expectTitle(page, id, '*Do not lose me.docx — Kazo Word');

    // Yes, then a file name: the document is written, then the window closes.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    await answerSaveDialog(page, 'Save the document', 'closing.docx');
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    await runInTerminal(page, `xdg-open ${HOME}/closing.docx`);
    const reopened = await waitForWindow(page, 1);
    await expectTitle(page, reopened, 'closing.docx — Kazo Word');
    await waitForText(await frameOf(page, reopened), /Do not lose me/);

    assert.deepEqual(errors, []);
    await page.close();
});
