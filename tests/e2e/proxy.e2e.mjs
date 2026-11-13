// End-to-end runs of the complete proxy (work item 3 of doc/en/integration-plan.md), in the Kazo OS
// debug host: `npm run test:e2e` after `dotnet build Kazo.Word.slnx -c Release`. Each test opens a
// fresh page, and fails if the browser's own file chooser ever opens.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { boot, openApp, startHost } from './host.mjs';

const READY_TIMEOUT_MS = 90_000;
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
// A small PNG of the repository (the Store icon), put in the user's home through File Explorer.
const picture = path.join(root, 'store', 'icon.png');

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

/** A fresh page with the OS booted; `choosers` counts the browser file choosers that opened. */
async function bootPage() {
    const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
    const errors = [];
    const choosers = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('filechooser', chooser => choosers.push(chooser));
    await boot(page, host.baseUrl);
    return { page, errors, choosers };
}

const wordWindows = page => page.locator('article[data-window-id]', { has: page.locator('.word-window') });
const wordFrames = page => page.frames().filter(frame => frame.url().includes('/_content/Kazo.Word/host/index.html'));

/** Opens Kazo Word and waits until WordCraft shows; returns its frame and its iframe element. */
async function openWord(page, count = 1) {
    await openApp(page, 'Kazo Word');
    await page.locator('.word-frame.running').nth(count - 1).waitFor({ timeout: READY_TIMEOUT_MS });
    const frames = wordFrames(page);
    return { frame: frames[frames.length - 1], iframe: page.locator('iframe.word-frame-iframe').nth(count - 1) };
}

/** A blank document, its start screen closed, the caret in the page. */
async function blankDocument(page, frame, iframe) {
    await frame.evaluate(() => window.wordcraft.execute('file.new', {}));
    const box = await iframe.boundingBox();
    const middle = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
    await page.mouse.click(middle.x, middle.y);
    await page.keyboard.press('Escape');
    await frame.waitForFunction(async () => (await window.wordcraft.uiInspect({})).ui.backstage === false, null, { timeout: 10_000 });
    await page.mouse.click(middle.x, middle.y);
}

/** The OS file dialog in front. */
const fileDialog = page => page.locator('section.file-dialog[role="dialog"]').last();

test('a picture from the Kazo files goes into the document through the OS dialog', { timeout: 300_000 }, async () => {
    const { page, errors, choosers } = await bootPage();

    // The picture in ~ (File Explorer's Upload button imports host files into the shown folder).
    await openApp(page, 'File Explorer');
    await page.locator('#fileInput').setInputFiles(picture);
    await page.getByText('icon.png', { exact: true }).first().waitFor({ timeout: 30_000 });

    const { frame, iframe } = await openWord(page);
    await blankDocument(page, frame, iframe);
    // Insert › Pictures, run by its command id (no user activation: rfd shows its card, claimed as it appears).
    await frame.evaluate(() => { window.wordcraft.execute('insert.picture', {}); });

    const dialog = fileDialog(page);
    await dialog.getByRole('heading', { name: 'Insert a picture' }).waitFor({ timeout: 15_000 });
    await dialog.getByText('icon.png', { exact: true }).dblclick();
    await dialog.waitFor({ state: 'detached', timeout: 10_000 });

    await frame.waitForFunction(async () => {
        const summary = await window.wordcraft.docInspect();
        return /"(picture|image|drawing)"/i.test(JSON.stringify(summary));
    }, null, { timeout: 20_000 });
    assert.equal(await frame.evaluate(() => document.getElementById('rfd-overlay')), null, 'rfd\'s overlay is gone');
    assert.equal(choosers.length, 0, 'the browser\'s file chooser never opened');
    assert.deepEqual(errors, []);
    await page.close();
});

test('Ctrl+O in WordCraft shows the OS dialog, and cancelling it leaves WordCraft running', { timeout: 300_000 }, async () => {
    const { page, errors, choosers } = await bootPage();
    const { frame, iframe } = await openWord(page);
    await blankDocument(page, frame, iframe);

    // A key press is a user activation: rfd clicks its input at once, and the click is caught.
    await page.keyboard.press('Control+o');
    const dialog = fileDialog(page);
    await dialog.getByRole('heading', { name: 'Open a document' }).waitFor({ timeout: 15_000 });
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await dialog.waitFor({ state: 'detached', timeout: 10_000 });

    const inspect = await frame.evaluate(() => window.wordcraft.uiInspect({}));
    assert.equal(inspect.documents.length, 1, 'WordCraft still runs with its document');
    assert.equal(await wordWindows(page).count(), 1, 'nothing opened');
    assert.equal(choosers.length, 0);
    assert.deepEqual(errors, []);
    await page.close();
});

test('Alt+Q, the window manager\'s Close outside full screen, typed in the document closes the Kazo Word window', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage();
    const { frame, iframe } = await openWord(page);
    await blankDocument(page, frame, iframe);

    await page.keyboard.press('Alt+q');

    await wordWindows(page).first().waitFor({ state: 'detached', timeout: 15_000 });
    assert.deepEqual(errors, []);
    await page.close();
});

test('Ctrl+N in WordCraft opens another Kazo Word window', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage();
    const { frame, iframe } = await openWord(page);
    await blankDocument(page, frame, iframe);

    await page.keyboard.press('Control+n');

    await page.locator('.word-frame.running').nth(1).waitFor({ timeout: READY_TIMEOUT_MS });
    assert.equal(await wordWindows(page).count(), 2);
    const inspect = await frame.evaluate(() => window.wordcraft.uiInspect({}));
    assert.equal(inspect.documents.length, 1, 'no second document in the first window');
    assert.deepEqual(errors, []);
    await page.close();
});

test('WordCraft follows the OS theme, light or dark', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage();
    const { frame } = await openWord(page);
    const themeIs = value => frame.waitForFunction(
        async expected => (await window.wordcraft.request('ui.theme', {})).theme === expected, value, { timeout: 10_000 });

    await themeIs('dark');
    // The shell retheming: a light theme on the theme scope, then the dark one again.
    await page.evaluate(() => document.querySelector('.theme-scope[data-theme]').setAttribute('data-theme', 'windows-10'));
    await themeIs('light');
    await page.evaluate(() => document.querySelector('.theme-scope[data-theme]').setAttribute('data-theme', 'kazoos'));
    await themeIs('dark');

    assert.deepEqual(errors, []);
    await page.close();
});

test('WordCraft\'s settings belong to the Kazo user: a new window starts with them, the browser storage stays clean', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage();
    const first = await openWord(page);

    await first.frame.evaluate(() => window.wordcraft.request('ui.statusCount', { value: 'characters' }));
    // The window writes the file half a second after WordCraft's last write.
    await page.waitForTimeout(1_500);
    const second = await openWord(page, 2);

    const count = await second.frame.evaluate(() => window.wordcraft.request('ui.statusCount', {}));
    assert.equal(JSON.stringify(count).includes('characters'), true, JSON.stringify(count));
    assert.equal(await page.evaluate(() => localStorage.getItem('wordcraft.ui')), null, 'nothing in the OS page\'s storage');
    assert.deepEqual(errors, []);
    await page.close();
});
