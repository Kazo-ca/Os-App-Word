// The Kazo OS Store's screenshots (store/screenshots/), taken by install-and-open.mjs when it is
// given a screenshots folder, in the OS where Kazo Word was just installed. The documents are
// WordCraft's own templates (its original text), so no third-party content appears.

const STEP_MS = 60_000;

/** egui paints on input: move the pointer over the document so the last frame is drawn. */
async function repaint(page, id, windowById) {
    const box = await windowById(page, id).locator('iframe.word-frame-iframe').boundingBox();
    for (let step = 0; step < 6; step++) {
        await page.mouse.move(box.x + box.width / 2 + step * 7, box.y + box.height - 40 - step * 5);
        await page.waitForTimeout(400);
    }
}

/**
 * Shows one of WordCraft's templates in the window and saves it under `fileName` in the home
 * folder, through the Kazo OS save dialog, so the title bar names the document shown.
 */
async function saveTemplateAs({ page, id, frame, execute, windowById }, template, fileName) {
    await execute(frame, 'file.new', { template });
    await frame.waitForFunction(async () => {
        try {
            return (await window.wordcraft.docInspect()).blocks.length > 3;
        } catch {
            return false;
        }
    }, null, { timeout: STEP_MS });
    await execute(frame, 'file.saveAs', { path: fileName });
    const dialog = page.locator('section.file-dialog', { hasText: 'Save the document' });
    await dialog.waitFor({ state: 'visible', timeout: STEP_MS });
    await dialog.getByLabel('File name').fill(fileName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_MS });
    await page.waitForFunction(
        ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
        [id, `${fileName} — Kazo Word`],
        { timeout: STEP_MS });
    await repaint(page, id, windowById);
}

export async function takeScreenshots({ page, second, reopened, execute, shoot, windowById, openApp }) {
    const context = { page, id: second, frame: reopened, execute, windowById };
    const chrome = windowById(page, second).locator('header.window-chrome').first();

    // The File Explorer of the journey is reopened at the end, listing the new files.
    for (const close of await page.locator('article[data-window-id]', { hasText: 'File Explorer' }).locator('header.window-chrome .chrome-button-close').all()) {
        await close.click();
    }

    // 1. A report, the window maximized.
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await saveTemplateAs(context, 'report', 'Report.docx');
    await shoot(page, '1.png');

    // 2. A letter, in a window of the desktop.
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await saveTemplateAs(context, 'letter', 'Letter.docx');
    await shoot(page, '2.png');

    // 3. The File Explorer in front: the documents open in Kazo Word with a double-click.
    await openApp(page, 'File Explorer');
    await page.locator('tr[data-item-path="/home/neo/Letter.docx"]').first().waitFor({ timeout: STEP_MS });
    await page.locator('tr[data-item-path="/home/neo/Letter.docx"]').first().click({ position: { x: 40, y: 10 } });
    await shoot(page, '3.png');
}
